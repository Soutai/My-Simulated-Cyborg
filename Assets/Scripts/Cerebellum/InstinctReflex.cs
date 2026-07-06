using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 具身智能物理沙盒 - 本能反射系统。
///
/// 生物学类比：大脑（AIBrainController）负责联网深思熟虑，但网络延迟决定了它在
/// "命悬一线的瞬间"这个时间尺度上完全靠不住。这里模拟的是不经过大脑的脊髓反射弧：
/// 感知到危险 -> 瞬间物理反应，跟"叫大脑重新想"并行发生，不等网络结果。
///
/// 两级阈值：
/// - dangerThreshold（低）：危险变化率超过它，打断当前计划、让大脑重新联网思考（原有机制，从 AIBrainController 搬过来）
/// - reflexThreshold（高）：危险变化率超过它，额外触发一次不经大脑的本能反射动作
///
/// 反射动作分两种，取决于这个威胁是"渐进逼近"还是"突然出现"：
/// - 渐进逼近（这个威胁上一帧就已经在追踪列表里，这一帧危险值持续升高）→ 逃跑反射：朝威胁反方向本能后退
/// - 突然出现（这个威胁上一帧完全不在感知范围/追踪列表里，这一帧突然以高危险值出现）→ 惊跳反射：
///     手上有武器（且武器效果本来就是 SweepAttack）→ 朝不精确的方向本能乱挥
///     手上空空 → 朝随机方向踉跄后退
/// </summary>
[RequireComponent(typeof(CharacterActuator))]
[RequireComponent(typeof(PerceptionRadar))]
public class InstinctReflex : MonoBehaviour
{
    private CharacterActuator actuator;
    private PerceptionRadar radar;
    private AIBrainController brain;

    [Header("危险感知阈值")]
    [Tooltip("危险变化率超过此值 -> 打断当前计划，让大脑重新联网思考")]
    public float dangerThreshold = 2.5f;
    [Tooltip("危险变化率超过此值（应明显高于上面那个）-> 额外触发不经大脑的本能反射动作")]
    public float reflexThreshold = 6.0f;

    [Header("逃跑反射（渐进逼近的已知威胁）")]
    public float fleeImpulseForce = 20f;

    [Header("惊跳反射（突然出现的威胁）")]
    public float startleImpulseForce = 15f;
    [Tooltip("手持武器乱挥时，相对精确朝向的随机偏转角度范围")]
    public float startleSwingAngleJitter = 45f;
    [Tooltip("空手被吓到之后的短暂僵直冷却时间，避免同一次惊吓连续触发多次反射")]
    public float stunnedDuration = 1.0f;

    private float lastDangerDensity = 0f;
    private readonly Collider[] dangerOverlapBuffer = new Collider[32];
    private HashSet<Collider> knownThreats = new HashSet<Collider>();
    private bool isStunned = false;

    void Awake()
    {
        actuator = GetComponent<CharacterActuator>();
        radar = GetComponent<PerceptionRadar>();
        brain = GetComponent<AIBrainController>();
    }

    void FixedUpdate()
    {
        if (isStunned) return;

        Collider primaryThreat;
        bool isNewThreat;
        float currentDangerDensity = CalculateEnvironmentalDangerDensity(out primaryThreat, out isNewThreat);

        float dDanger_dt = (currentDangerDensity - lastDangerDensity) / Time.fixedDeltaTime;
        lastDangerDensity = currentDangerDensity;

        if (dDanger_dt > reflexThreshold && primaryThreat != null)
        {
            // 先让大脑那边刹车、清空计划、准备重新思考（这一步会把速度清零），
            // 再施加反射冲量，顺序不能反——不然冲量会被这里的"停止移动"直接吃掉。
            NotifyBrainToRethink(dDanger_dt);

            if (isNewThreat)
                TriggerStartleReflex(primaryThreat);
            else
                TriggerFleeReflex(primaryThreat);
        }
        else if (dDanger_dt > dangerThreshold)
        {
            NotifyBrainToRethink(dDanger_dt);
        }
    }

    private void NotifyBrainToRethink(float dDanger_dt)
    {
        if (brain == null) return;

        Debug.LogError($"<color=red>🚨 [本能反射] 检测到环境危险熵突变率 {dDanger_dt:F2}！触发本能中断，交还大脑重新思考！</color>");

        brain.InterruptAndClearGoal();
        brain.RequestImmediateThink();
    }

    /// <summary>
    /// 计算危险密度，同时找出贡献最大的"主要威胁"，并判断它是渐进逼近的已知威胁还是刚刚出现的新威胁。
    /// </summary>
    private float CalculateEnvironmentalDangerDensity(out Collider primaryThreat, out bool isNewThreat)
    {
        primaryThreat = null;
        isNewThreat = false;

        if (radar == null) return 0f;

        float maxSingleDanger = 0f;
        float totalDanger = 0f;
        var currentThreats = new HashSet<Collider>();

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, radar.perceptionRadius, dangerOverlapBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            var col = dangerOverlapBuffer[i];
            if (col.gameObject == this.gameObject) continue;

            // 🌟 跳过 NPC 自己正主动接近的目标：这是我自己冲过去的，不是它冲过来的，不该被当成突发威胁
            if (actuator.CurrentApproachTarget != null && col.gameObject == actuator.CurrentApproachTarget) continue;

            SemanticObject semanticObj = col.GetComponent<SemanticObject>();
            if (semanticObj == null) continue;

            float omega = 0f;
            if (semanticObj.semanticType == SemanticType.Enemy) omega = 2.5f;     // 狼高危
            else if (semanticObj.semanticType == SemanticType.Weapon) omega = 0.1f;

            Rigidbody targetRb = col.GetComponent<Rigidbody>();
            Vector3 velocityVec = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
            float velocity = velocityVec.magnitude;

            // 如果物体有速度，判断它是在接近我还是远离我；背离我（被打飞或逃跑）威胁度归零
            if (velocity > 0.1f)
            {
                Vector3 toMe = (transform.position - col.transform.position).normalized;
                float dot = Vector3.Dot(velocityVec.normalized, toMe);
                if (dot <= 0f) continue;
            }

            float distance = Vector3.Distance(transform.position, col.transform.position);
            if (distance < 0.5f) distance = 0.5f;

            float singleDanger = (velocity * omega) / (distance * distance);
            if (singleDanger <= 0f) continue;

            totalDanger += singleDanger;
            currentThreats.Add(col);

            if (singleDanger > maxSingleDanger)
            {
                maxSingleDanger = singleDanger;
                primaryThreat = col;
                isNewThreat = !knownThreats.Contains(col);
            }
        }

        knownThreats = currentThreats;
        return totalDanger;
    }

    /// <summary>
    /// 逃跑反射：这个威胁早就在雷达上、只是持续逼近，属于"眼看着它靠近"，是有意识的躲避，朝反方向明确后退一把。
    /// </summary>
    private void TriggerFleeReflex(Collider threat)
    {
        Debug.Log($"<color=#FF3333>[本能反射] 🏃 感知到 {threat.gameObject.name} 持续逼近，触发逃跑反射！</color>");

        Vector3 fleeDirection = transform.position - threat.transform.position;
        fleeDirection.y = 0f;
        fleeDirection.Normalize();

        actuator.ApplyInstinctImpulse(fleeDirection * fleeImpulseForce);
    }

    /// <summary>
    /// 惊跳反射：这个威胁刚刚才出现在感知范围内（比如转身瞬间突然扫到），是猝不及防的一惊，
    /// 反应不是精准躲避，而是原始的应激：有家伙就乱挥，没家伙就被吓得踉跄后退。
    /// </summary>
    private void TriggerStartleReflex(Collider threat)
    {
        GameObject heldItem = actuator.CurrentGrabbedObject;
        SemanticObject heldSemantic = heldItem != null ? heldItem.GetComponent<SemanticObject>() : null;
        bool isArmedWithSweepWeapon = heldSemantic != null &&
            PhysicsProtocolConfig.GetUseEffect(heldSemantic.semanticType).kind == PhysicsProtocolConfig.UseEffectKind.SweepAttack;

        if (isArmedWithSweepWeapon)
        {
            Debug.Log($"<color=#FF9900>[本能反射] 😱 猝不及防撞见 {threat.gameObject.name}，手里有家伙，本能地朝它乱挥了一下！</color>");
            actuator.InstinctFlailSwing(startleSwingAngleJitter);
        }
        else
        {
            Debug.Log($"<color=#FF9900>[本能反射] 😱 猝不及防撞见 {threat.gameObject.name}，手无寸铁被吓得一个踉跄！</color>");

            Vector2 randomDir2D = Random.insideUnitCircle.normalized;
            Vector3 stumbleDirection = new Vector3(randomDir2D.x, 0f, randomDir2D.y);
            actuator.ApplyInstinctImpulse(stumbleDirection * startleImpulseForce);

            StartCoroutine(StunCooldown());
        }
    }

    /// <summary>
    /// 短暂的反射冷却：防止同一次惊吓在接下来几帧里被反复重新触发。
    /// </summary>
    private IEnumerator StunCooldown()
    {
        isStunned = true;
        yield return new WaitForSeconds(stunnedDuration);
        isStunned = false;
    }
}
