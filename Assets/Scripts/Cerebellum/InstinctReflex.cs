using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 具身智能物理沙盒 - 本能反射系统（简化版）。
///
/// 只做一件事：如果危险变化率超过阈值，就持续朝"威胁反方向"移动，直到危险解除为止——
/// 不联网、不经过大脑，也不需要大脑批准。
///
/// 🌟 本能优先于决策：哪怕大脑当前正在执行一个长程计划（比如盲目的探索/搜寻路线），
/// 只要真实危险出现，也会强行打断该计划、抢回身体控制权——不会像旧版那样等身体"空闲"才敢介入，
/// 因为大脑的长程计划本身可能对眼前的危险一无所知（比如探索计划的中断锚点只关心"武器"，不关心"敌人贴脸"）。
/// 危险解除后交还控制权，如果大脑期间攒了新计划会无缝接续执行。
///
/// 多个威胁同时出现时，取各自"远离该威胁"方向的合向量（相当于取夹角平分线）；
/// 如果算出来的逃跑方向正对着障碍物（墙、水果、木棍——只要不是自己或手里正拿着的东西），
/// 会额外混入"远离障碍物表面"的方向，避免被夹在障碍物和威胁之间动弹不得。
///
/// 所有调参数值集中在 InstinctProtocolConfig，这里只管调用逻辑本身。
/// </summary>
[RequireComponent(typeof(CharacterActuator))]
public class InstinctReflex : MonoBehaviour
{
    /// <summary>单个威胁被分到的危险等级——贴身 > 接近 > 远处公式兜底，等级越高越优先。</summary>
    private enum ThreatTier
    {
        None,
        Formula,
        Approaching,
        Melee
    }

    private CharacterActuator actuator;
    private LocalMotorController smallBrain;
    private AIBrainController brain;
    private WanderReflex wanderReflex;

    // 🌟 供外部（LocalMotorController）判断"身体现在是不是被本能反射占用"，
    // 占用期间大脑新下发的计划一律锁进后台缓冲区，不能抢过来打断本能。
    public bool IsFleeing => isFleeing;

    private float lastDangerDensity = 0f;
    private bool isFleeing = false;

    private readonly Collider[] dangerOverlapBuffer = new Collider[32];
    private readonly List<Vector3> threatAwayDirections = new List<Vector3>();

    private float diagnosticLogTimer = 0f;
    private Vector3 lastLoggedPosition;

    private float punchCooldownTimer = 0f;
    private bool hasMeleeThreatThisFrame = false;

    void Awake()
    {
        actuator = GetComponent<CharacterActuator>();
        smallBrain = GetComponent<LocalMotorController>();
        brain = GetComponent<AIBrainController>();
        wanderReflex = GetComponent<WanderReflex>();
    }

    void FixedUpdate()
    {
        float currentDangerDensity = CalculateDangerDensity();
        float dDanger_dt = (currentDangerDensity - lastDangerDensity) / Time.fixedDeltaTime;
        lastDangerDensity = currentDangerDensity;

        if (!isFleeing)
        {
            TryEnterFleeing(currentDangerDensity, dDanger_dt);
        }
        else
        {
            TryExitFleeing(currentDangerDensity);
        }

        if (punchCooldownTimer > 0f) punchCooldownTimer -= Time.fixedDeltaTime;

        if (isFleeing)
        {
            RunFleeingBehaviour(currentDangerDensity);
        }
        else
        {
            diagnosticLogTimer = 0f;
        }
    }

    /// <summary>
    /// 触发条件：危险正在明显上升，或者危险本身已经处于高位——不再要求身体先空闲。
    /// 🌟 只看变化率会漏掉"危险已经稳定在高位、但这一帧没有新的突变"的情况——比如贴身近战
    /// 覆写让危险度稳定在高位，如果上一次逃跑因为一帧抖动被误判为"已脱离"而解除，后续危险度
    /// 不再有新的尖峰，就再也没有机会重新触发，NPC 会站着挨打。加上绝对值兜底后，只要危险度
    /// 本身仍然够高，哪怕没有新尖峰也能立刻重新触发。
    /// </summary>
    private void TryEnterFleeing(float currentDangerDensity, float dDanger_dt)
    {
        bool dangerRising = dDanger_dt > InstinctProtocolConfig.DangerThreshold;
        bool dangerAlreadyHigh = currentDangerDensity > InstinctProtocolConfig.DangerThreshold;

        if (!dangerRising && !dangerAlreadyHigh) return;

        isFleeing = true;
        // 🌟 重置诊断基准点：不重置的话，如果距离上次逃跑已经隔了一段时间（期间可能因为漫步等
        // 走出去很远），第一条位移诊断日志会拿"现在的位置"和"很久以前的位置"作差，
        // 算出一个物理上不可能的巨大"位移"，误导排障（不是真的瞬移，纯粹是基准点过期了）。
        lastLoggedPosition = transform.position;
        diagnosticLogTimer = 0f;

        Debug.LogWarning($"<color=red>🚨 [本能反射] 危险熵突变率 {dDanger_dt:F2}，当前危险度 {currentDangerDensity:F2}！本能开始远离威胁！</color>");

        // 🌟 本能优先于决策：大脑当前的计划可能对这个危险一无所知（比如长程探索计划
        // 只在雷达扫到"武器"时才会被打断），身体正忙也要强行抢回控制权，不能干等它空闲。
        //
        // 🌟 这里不能只看 smallBrain.IsBusy——锚点唤醒引入了"软性打断"之后，身体有可能处于
        // isBusy=false 但 WanderReflex 仍在漫步（等大脑回应）的中间状态。如果只看 isBusy，
        // 这种情况下危险来了却不会真的抢控制权，本能的推力会跟漫步的推力在刚体上同时生效、互相打架。
        bool bodyOccupied = (smallBrain != null && smallBrain.IsBusy) || (wanderReflex != null && wanderReflex.IsWandering);
        if (bodyOccupied && brain != null)
        {
            Debug.LogWarning("<color=red>⚡ [本能反射] 身体正在忙于跟危险无关的事，强行打断，本能抢回身体控制权！</color>");
            brain.InterruptAndClearGoal(); // hardStopMovement 默认 true，会连漫步一起硬停
        }
    }

    /// <summary>
    /// 解除条件：只看危险本身有没有真正解除，不再因为"大脑想拿回身体"就提前让路——
    /// 本能优先于决策，这里不受大脑是否忙碌影响。
    /// </summary>
    private void TryExitFleeing(float currentDangerDensity)
    {
        if (currentDangerDensity > InstinctProtocolConfig.SafeDangerDensity) return;

        isFleeing = false;
        Debug.Log("<color=#33CC33>[本能反射] 😌 已脱离危险，本能逃跑结束，交还身体控制权。</color>");

        // 🌟 本能只负责"脱险"，不负责"脱险之后干什么"——那是决策层面的事。如果期间大脑没攒下
        // 新计划（没有排队的后台缓冲区），身体不该就这么僵在原地干等下一次常规思考（最长可能等
        // 20 秒），而是应该立刻叫醒大脑，让它根据刚刚脱险后的最新处境重新决策，不留空窗期。
        bool resumedFromBackBuffer = smallBrain != null && smallBrain.TryResumeFromBackBuffer();
        if (!resumedFromBackBuffer)
        {
            Debug.Log("<color=#33CC33>[本能反射] 🧠 没有攒好的后续计划，立刻叫醒大脑重新思考当前处境。</color>");
            brain?.RequestImmediateThink();
        }
    }

    private void RunFleeingBehaviour(float currentDangerDensity)
    {
        Vector3 fleeDirection = ComputeFleeDirection(out float clearDistance);
        actuator.ApplyInstinctForce(fleeDirection, InstinctProtocolConfig.FleeForce, InstinctProtocolConfig.MaxFleeSpeed);

        // 🌟 排障诊断 + 卡死急救：逃跑期间每隔一段时间检查一次实际位移。
        // 射线判定"方向畅通"不代表身体真的能走得动——缝隙可能比身体本身的胶囊体还窄，
        // 持续的平稳推力会被摩擦力/挤压反力完全吃掉，表现为"方向选对了、力也在推，但位置纹丝不动"。
        // 这时候需要跳出"温和推"的逻辑，直接给一次远超日常强度的瞬间冲量把身体弹开。
        diagnosticLogTimer += Time.fixedDeltaTime;
        if (diagnosticLogTimer < InstinctProtocolConfig.DiagnosticLogInterval) return;

        diagnosticLogTimer = 0f;
        Vector3 currentPosition = new Vector3(transform.position.x, 0f, transform.position.z);
        float displacedSinceLast = Vector3.Distance(currentPosition, lastLoggedPosition);
        lastLoggedPosition = currentPosition;

        Debug.Log($"<color=#FF8800>[本能反射诊断] 🔍 持续逃跑中 | 危险度: {currentDangerDensity:F2} | 逃跑方向: {fleeDirection:F2} | 该方向畅通距离: {clearDistance:F2}/{InstinctProtocolConfig.WallCheckDistance:F2} | 过去{InstinctProtocolConfig.DiagnosticLogInterval:F1}s位移: {displacedSinceLast:F2}m</color>");

        if (displacedSinceLast >= InstinctProtocolConfig.StuckDisplacementThreshold) return;

        Debug.LogWarning($"<color=#FF0000>⚠️ [本能反射] 检测到物理卡死（{InstinctProtocolConfig.DiagnosticLogInterval:F1}s 内仅位移 {displacedSinceLast:F2}m），给出急救冲量强行脱困！</color>");
        actuator.ApplyInstinctUnstickImpulse(fleeDirection, InstinctProtocolConfig.UnstickImpulseForce);

        // 🌟 赤手空拳反击：贴身、逃不掉、又双手空空——干脆朝威胁方向反击一拳，把敌人本身推开，
        // 往往比只推自己更能解开"贴身对峙"这种死结。有武器的话交给大脑用 USE_ITEM 决定要不要挥，
        // 这里只管手无寸铁时的本能兜底。
        if (hasMeleeThreatThisFrame && actuator.CurrentGrabbedObject == null && punchCooldownTimer <= 0f)
        {
            Debug.LogWarning("<color=#FF00FF>👊 [本能反射] 贴身逃不掉，双手空空，本能反击一拳！</color>");
            actuator.ApplyInstinctPunch(-fleeDirection);
            punchCooldownTimer = InstinctProtocolConfig.PunchCooldown;
        }
    }

    /// <summary>
    /// 计算当前危险密度，同时顺手记下每个威胁"远离它"的方向，供逃跑方向合成使用。
    /// </summary>
    private float CalculateDangerDensity()
    {
        threatAwayDirections.Clear();
        hasMeleeThreatThisFrame = false;

        float totalDanger = 0f;

        // 🌟 危险感知走自己独立的半径，不再借用 PerceptionRadar.perceptionRadius——
        // 视觉、听觉、危险感知是三套不同的生物学机制，理应各自独立可调
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, InstinctProtocolConfig.DangerSenseRadius, dangerOverlapBuffer);
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

            float singleDanger = ClassifyThreatDanger(col, semanticObj, distance, toMeDir);
            if (singleDanger <= 0f) continue;

            totalDanger += singleDanger;
            threatAwayDirections.Add(toMeDir);
        }

        return totalDanger;
    }

    /// <summary>
    /// 给单个威胁物体分级并算出对应危险值，等级从高到低依次尝试：
    /// 贴身覆写（不看速度/视野，欺近到近战距离就是最高等级）→ 接近威胁（正主动朝我靠近）→
    /// 平方反比公式兜底（远处威胁，数值通常微不足道）。背离我的移动物体直接判定无威胁。
    /// 顺带把"本帧是否存在贴身威胁"记到 hasMeleeThreatThisFrame，供赤手空拳反击判断使用。
    /// </summary>
    private float ClassifyThreatDanger(Collider col, SemanticObject semanticObj, float distance, Vector3 toMeDir)
    {
        bool isEnemy = semanticObj.semanticType == SemanticType.Enemy;

        ThreatTier tier = ClassifyTier(col, semanticObj, isEnemy, distance, toMeDir, out float velocity);

        switch (tier)
        {
            case ThreatTier.None:
                return 0f;

            case ThreatTier.Melee:
                hasMeleeThreatThisFrame = true;
                return InstinctProtocolConfig.MeleeDangerValue;

            case ThreatTier.Approaching:
                return InstinctProtocolConfig.ApproachingThreatDangerValue;

            case ThreatTier.Formula:
            default:
                float omega = 0f;
                if (isEnemy) omega = InstinctProtocolConfig.EnemyDangerOmega;
                else if (semanticObj.semanticType == SemanticType.Weapon) omega = InstinctProtocolConfig.WeaponDangerOmega;

                return (velocity * omega) / (distance * distance);
        }
    }

    private ThreatTier ClassifyTier(Collider col, SemanticObject semanticObj, bool isEnemy, float distance, Vector3 toMeDir,
        out float velocity)
    {
        // 🌟 贴身威胁：敌人已经欺近到近战距离，不管它这一刻自己动没动都算危险——
        // 比如正停下来咬你的狼，攻击时会把自己的速度清零，但显然不代表它不再是威胁。
        //
        // 🌟 滞后缓冲：贴身对峙时两个刚体互相挤压，实际距离会在 MeleeDangerRange 边界内外
        // 来回抖动几厘米——已经在逃跑状态时，要求敌人明显跑得更远才算真正脱离贴身范围，
        // 避免几厘米的物理抖动导致危险度在高位和接近 0 之间逐帧反复横跳。
        float effectiveMeleeRange = isFleeing
            ? InstinctProtocolConfig.MeleeDangerRange * InstinctProtocolConfig.MeleeHysteresisMultiplier
            : InstinctProtocolConfig.MeleeDangerRange;

        velocity = 0f;

        if (isEnemy && distance <= effectiveMeleeRange)
        {
            return ThreatTier.Melee;
        }

        Rigidbody targetRb = col.GetComponent<Rigidbody>();
        Vector3 velocityVec = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
        velocity = velocityVec.magnitude;
        bool hasVelocity = velocity > 0.1f;
        bool isClosingIn = hasVelocity && Vector3.Dot(velocityVec.normalized, toMeDir) > 0f;

        // 有速度但在远离我（被打飞、自己逃跑）——威胁度直接归零
        if (hasVelocity && !isClosingIn) return ThreatTier.None;

        // 🌟 接近威胁：还没贴脸，但正主动朝我靠近的敌人——平方反比公式在稍远距离算出来的值
        // 微不足道，不符合"感觉到掠食者冲过来就该跑"的直觉，所以单独给一档。
        //
        // ⚠️ 临时方案（2026-07-07 决定）：这一档故意不要求"看得见"（不再检查视觉扇形）。
        // 之前用视觉扇形做门槛时，出现了一个死循环：本能逃跑的方向就是背对威胁的方向，而
        // facingDirection 又是跟着移动方向走的，于是"开始逃跑"这个动作本身会让敌人立刻掉出
        // 视野，导致危险判定在"看得见/看不见"之间逐帧反复横跳、表现为动作疯狂抽搐。
        // 改成全向判定（复用 dangerSenseRadius 的扫描范围，不额外限定角度）后问题消失，
        // 但这也意味着"没看见也能反应"——跟贴身覆写一样不够严谨。以后要重新引入方向性
        // 判断的话，需要先解决"逃跑方向 vs 朝向"的耦合问题（比如给朝向一个独立于移动的状态），
        // 否则同样的死循环还会重演。
        if (isEnemy && isClosingIn)
        {
            return ThreatTier.Approaching;
        }

        return ThreatTier.Formula;
    }

    // 🌟 在理想方向左右这些角度上采样候选方向，越靠前越优先（越接近理想逃跑方向）。
    // 覆盖到 180°是为了应对"正对墙角、理想方向完全被堵死"的极端情况，好过原地不动。
    private static readonly float[] FleeAngleOffsets =
    {
        0f, -30f, 30f, -60f, 60f, -90f, 90f, -120f, 120f, -150f, 150f, 180f
    };

    /// <summary>
    /// 多个威胁的"远离方向"取合向量（夹角平分线），作为"理想逃跑方向"；
    /// 再在这个方向附近扫一圈找一条实际没被堵死的路——单纯"撞到障碍物就反弹一次"在墙角这种
    /// 两面障碍夹角的场景下不够用（弹开第一面墙，可能正好对着第二面墙），需要主动找一条畅通的路。
    /// </summary>
    private Vector3 ComputeFleeDirection(out float achievedClearDistance)
    {
        Vector3 idealDirection = Vector3.zero;
        foreach (var dir in threatAwayDirections)
            idealDirection += dir;

        if (idealDirection.sqrMagnitude < 0.0001f)
        {
            // 多个威胁方向完全抵消（比如被夹在正中间），退化成随便挑一个威胁的方向逃，好过站着不动
            idealDirection = threatAwayDirections.Count > 0 ? threatAwayDirections[0] : transform.forward;
        }

        idealDirection.y = 0f;
        idealDirection.Normalize();

        Vector3 bestDirection = idealDirection;
        float bestClearDistance = -1f;

        foreach (float angleOffset in FleeAngleOffsets)
        {
            Vector3 candidate = Quaternion.Euler(0f, angleOffset, 0f) * idealDirection;
            float clearDistance = GetClearDistance(candidate);

            if (clearDistance > bestClearDistance)
            {
                bestClearDistance = clearDistance;
                bestDirection = candidate;
            }

            // 已经找到完全畅通的方向，FleeAngleOffsets 又是按"离理想方向的接近程度"排的，
            // 没必要再找更远的角度去替代一个已经完全没问题的选择
            if (bestClearDistance >= InstinctProtocolConfig.WallCheckDistance) break;
        }

        achievedClearDistance = bestClearDistance;
        return bestDirection;
    }

    /// <summary>
    /// 某个方向上还有多远才会撞到东西（自己身上的碰撞体和手里正拿着的东西不算数）。
    /// 没撞到任何东西就视为在探测距离内完全畅通。
    /// </summary>
    private float GetClearDistance(Vector3 direction)
    {
        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, InstinctProtocolConfig.WallCheckDistance))
        {
            GameObject hitObject = hit.collider.gameObject;
            bool isSelfOrHeld = hitObject == this.gameObject
                || hitObject == actuator.LeftHandObject
                || hitObject == actuator.RightHandObject;

            if (!isSelfOrHeld) return hit.distance;
        }

        return InstinctProtocolConfig.WallCheckDistance;
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        // 贴身近战覆写范围：深红色圈。DangerSenseRadius 只是内部扫描半径的实现细节，
        // 不是一个需要盯着调的"感官范围"，故意不画；接近威胁是全向判定，也不需要额外画圈。
        UnityEditor.Handles.color = new Color(0.6f, 0f, 0f, 0.5f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, InstinctProtocolConfig.MeleeDangerRange);
#endif
    }
}
