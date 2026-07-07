using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 具身智能物理沙盒 - 本能反射系统（简化版）。
///
/// 只做一件事：身体当前空闲（没有在执行大脑下发的计划）时，如果危险变化率超过阈值，
/// 就持续朝"威胁反方向"移动，直到危险解除为止——不联网、不经过大脑，也不需要大脑批准。
/// 一旦大脑随后下发了新计划（身体变得不空闲），这里会自动让路，不会跟正常指令抢控制权。
///
/// 多个威胁同时出现时，取各自"远离该威胁"方向的合向量（相当于取夹角平分线）；
/// 如果算出来的逃跑方向正对着障碍物（墙之类没有 SemanticObject 的东西），
/// 会额外混入"远离障碍物表面"的方向，避免一头撞上去。
/// </summary>
[RequireComponent(typeof(CharacterActuator))]
[RequireComponent(typeof(PerceptionRadar))]
public class InstinctReflex : MonoBehaviour
{
    private CharacterActuator actuator;
    private PerceptionRadar radar;
    private LocalMotorController smallBrain;

    [Header("危险感知")]
    [Tooltip("危险变化率超过此值才会触发逃跑反射")]
    public float dangerThreshold = 2.5f;
    [Tooltip("危险度降到此值以下视为已脱离危险，停止逃跑")]
    public float safeDangerDensity = 0.2f;
    [Tooltip("敌人进入此距离内，无论它自己是否在动都视为贴身威胁")]
    public float meleeDangerRange = 2f;
    [Tooltip("贴身威胁的固定危险值，需明显高于 dangerThreshold 才能可靠触发")]
    public float meleeDangerValue = 5f;

    [Header("逃跑反射")]
    public float fleeForce = 12f;
    public float maxFleeSpeed = 6f;

    [Header("撞墙规避")]
    [Tooltip("往逃跑方向探测多远，撞到障碍物就混入'远离障碍物'的方向")]
    public float wallCheckDistance = 2f;

    private float lastDangerDensity = 0f;
    private bool isFleeing = false;

    private readonly Collider[] dangerOverlapBuffer = new Collider[32];
    private readonly List<Vector3> threatAwayDirections = new List<Vector3>();

    void Awake()
    {
        actuator = GetComponent<CharacterActuator>();
        radar = GetComponent<PerceptionRadar>();
        smallBrain = GetComponent<LocalMotorController>();
    }

    void FixedUpdate()
    {
        float currentDangerDensity = CalculateDangerDensity();
        float dDanger_dt = (currentDangerDensity - lastDangerDensity) / Time.fixedDeltaTime;
        lastDangerDensity = currentDangerDensity;

        bool isIdle = smallBrain == null || !smallBrain.IsBusy;

        if (!isFleeing)
        {
            // 触发条件：身体空闲 + 危险正在明显上升
            if (isIdle && dDanger_dt > dangerThreshold)
            {
                isFleeing = true;
                Debug.LogWarning($"<color=red>🚨 [本能反射] 检测到环境危险熵突变率 {dDanger_dt:F2}！身体空闲，本能开始远离威胁！</color>");
            }
        }
        else
        {
            // 解除条件：大脑重新接管了身体，或者危险已经降到安全水位以下
            if (!isIdle || currentDangerDensity <= safeDangerDensity)
            {
                isFleeing = false;
                Debug.Log("<color=#33CC33>[本能反射] 😌 已脱离危险，本能逃跑结束。</color>");
            }
        }

        if (isFleeing)
        {
            Vector3 fleeDirection = ComputeFleeDirection();
            actuator.ApplyInstinctForce(fleeDirection, fleeForce, maxFleeSpeed);
        }
    }

    /// <summary>
    /// 计算当前危险密度，同时顺手记下每个威胁"远离它"的方向，供逃跑方向合成使用。
    /// </summary>
    private float CalculateDangerDensity()
    {
        threatAwayDirections.Clear();
        if (radar == null) return 0f;

        float totalDanger = 0f;

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, radar.perceptionRadius, dangerOverlapBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            var col = dangerOverlapBuffer[i];
            if (col.gameObject == this.gameObject) continue;

            // 🌟 跳过 NPC 自己正主动接近的目标：这是我自己冲过去的，不是它冲过来的
            if (actuator.CurrentApproachTarget != null && col.gameObject == actuator.CurrentApproachTarget) continue;

            SemanticObject semanticObj = col.GetComponent<SemanticObject>();
            if (semanticObj == null) continue;

            Vector3 toMe = transform.position - col.transform.position;
            toMe.y = 0f;
            Vector3 toMeDir = toMe.normalized;
            float distance = Mathf.Max(toMe.magnitude, 0.5f);

            float singleDanger;

            // 🌟 贴身威胁：敌人已经欺近到近战距离，不管它这一刻自己动没动都算危险——
            // 比如正停下来咬你的狼，攻击时会把自己的速度清零，但显然不代表它不再是威胁。
            // 只靠"它有没有在移动"来判断危险，会漏掉"已经欺身、正在原地啃你"这种最危险的情况。
            if (semanticObj.semanticType == SemanticType.Enemy && distance <= meleeDangerRange)
            {
                singleDanger = meleeDangerValue;
            }
            else
            {
                float omega = 0f;
                if (semanticObj.semanticType == SemanticType.Enemy) omega = 2.5f;     // 狼高危
                else if (semanticObj.semanticType == SemanticType.Weapon) omega = 0.1f;

                Rigidbody targetRb = col.GetComponent<Rigidbody>();
                Vector3 velocityVec = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
                float velocity = velocityVec.magnitude;

                // 如果物体有速度，判断它是在接近我还是远离我；背离我（被打飞或逃跑）威胁度归零
                if (velocity > 0.1f)
                {
                    float dot = Vector3.Dot(velocityVec.normalized, toMeDir);
                    if (dot <= 0f) continue;
                }

                singleDanger = (velocity * omega) / (distance * distance);
            }

            if (singleDanger <= 0f) continue;

            totalDanger += singleDanger;
            threatAwayDirections.Add(toMeDir);
        }

        return totalDanger;
    }

    /// <summary>
    /// 多个威胁的"远离方向"取合向量（夹角平分线）；如果这个方向正对着障碍物，混入"远离障碍物表面"的方向。
    /// </summary>
    private Vector3 ComputeFleeDirection()
    {
        Vector3 combined = Vector3.zero;
        foreach (var dir in threatAwayDirections)
            combined += dir;

        if (combined.sqrMagnitude < 0.0001f)
        {
            // 多个威胁方向完全抵消（比如被夹在正中间），退化成随便挑一个威胁的方向逃，好过站着不动
            combined = threatAwayDirections.Count > 0 ? threatAwayDirections[0] : transform.forward;
        }

        combined.y = 0f;
        combined.Normalize();

        // 撞墙规避：往算好的方向探一下，撞到"没有 SemanticObject 的东西"（墙/障碍物）就混入远离它表面的方向
        if (Physics.Raycast(transform.position, combined, out RaycastHit hit, wallCheckDistance))
        {
            if (hit.collider.gameObject != this.gameObject && hit.collider.GetComponent<SemanticObject>() == null)
            {
                Vector3 awayFromWall = hit.normal;
                awayFromWall.y = 0f;

                if (awayFromWall.sqrMagnitude > 0.0001f)
                {
                    combined = (combined + awayFromWall.normalized).normalized;
                }
            }
        }

        return combined;
    }
}
