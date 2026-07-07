using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 具身智能物理沙盒 - 本地漫步探索反射。
///
/// 只做一件事：大脑决定"现在该探索了"之后，把具体"怎么走"这件纯机械的事交给这里本地处理——
/// 不联网、不经过大脑，大脑只需要在 plan_steps 里下发一个 EXPLORE 原语，剩下的路怎么走、
/// 什么时候拐弯、别老在同一片地方打转，全部由这里自主决定。
///
/// 🌟 设计动机：探索这件事被拆成了两层——"要不要探索、为了找什么"是战略决策，理应留在大脑
/// （通过 interrupt_anchor_type 表达）；"具体往哪一步走"是纯粹的局部机械行为，不需要推理，
/// 跟本能反射决定"往哪个方向逃跑"性质上是一回事。旧方案里大脑要手写 15-20 步具体坐标模拟
/// "随便走走"，既浪费网络请求，走出来的路线也很生硬（LLM 没有真实地图记忆，纯粹瞎编坐标）。
///
/// 🌟 "聪明"体现在三点，思路跟 InstinctReflex.ComputeFleeDirection() 的避障采样一脉相承：
/// 1. 换方向时会在当前方向附近多个候选角度里，用"射线探测清不清空"给方向打分，避免闷头撞墙；
/// 2. 额外记一份"最近走过的位置"轨迹，候选方向离轨迹越远打分越高，鼓励去没去过的地方，
///    而不是在同一片开阔地里绕圈子——这个轨迹record只需要记最近十几个点，不追求覆盖整张地图；
/// 3. 候选方向如果指向雷达范围内的敌人，会被重重扣分——不然漫步系统对危险一无所知，
///    很容易出现"本能刚把身体从狼的地盘逃出来，漫步系统下一秒又把它带回去"这种死循环。
///    这里不需要复刻 InstinctReflex 的完整危险度公式，只需要知道"那个方向有没有敌人、离多远"就够了。
/// </summary>
[RequireComponent(typeof(CharacterActuator))]
public class WanderReflex : MonoBehaviour
{
    private CharacterActuator actuator;

    /// <summary>供 LocalMotorController 判断漫步是否正在进行</summary>
    public bool IsWandering { get; private set; }

    private Vector3 currentDirection = Vector3.forward;
    private float directionTimer = 0f;

    private readonly List<Vector3> recentTrail = new List<Vector3>();
    private float trailSampleTimer = 0f;

    private readonly Collider[] threatScanBuffer = new Collider[16];
    private readonly List<Vector3> nearbyThreatPositions = new List<Vector3>();

    // 🌟 排障诊断：定期打印速度/位移快照，用来判断"走得慢"到底是力度不够、频繁被判定受阻
    // 换方向、还是候选方向评分本身有问题——不猜，直接看数据。
    private float diagnosticLogTimer = 0f;
    private Vector3 lastLoggedPosition;

    void Awake()
    {
        actuator = GetComponent<CharacterActuator>();
    }

    /// <summary>由 LocalMotorController 在接到 EXPLORE 原语时调用，开始本地漫步</summary>
    public void BeginWandering()
    {
        IsWandering = true;
        directionTimer = 0f; // 立刻在这一帧挑一个新方向，不用等
        recentTrail.Clear();
        diagnosticLogTimer = 0f;
        lastLoggedPosition = transform.position;
        Debug.Log("<color=#33CCCC>[漫步反射] 🚶 开始本地自主探索。</color>");
    }

    /// <summary>由 LocalMotorController 在任何中断路径（锚点唤醒/本能打断）里调用，停止漫步</summary>
    public void StopWandering()
    {
        if (!IsWandering) return;
        IsWandering = false;
        Debug.Log("<color=#33CCCC>[漫步反射] 🛑 本地探索结束，交还身体控制权。</color>");
    }

    void FixedUpdate()
    {
        if (!IsWandering) return;

        SampleTrail();

        directionTimer -= Time.fixedDeltaTime;
        float aheadClearDistance = GetClearDistance(currentDirection);
        bool aheadBlocked = aheadClearDistance < WanderProtocolConfig.ObstacleLookahead * 0.5f;

        if (directionTimer <= 0f || aheadBlocked)
        {
            string reason = aheadBlocked ? $"前方受阻(畅通距离{aheadClearDistance:F2}m)" : "计时器到期";
            Vector3 oldDirection = currentDirection;
            currentDirection = PickNewDirection();
            directionTimer = Random.Range(WanderProtocolConfig.MinDirectionHold, WanderProtocolConfig.MaxDirectionHold);

            Debug.Log($"<color=#33CCCC>[漫步反射] 🔄 换方向 | 原因: {reason} | 旧方向: {oldDirection:F2} → 新方向: {currentDirection:F2} | 维持 {directionTimer:F1}s</color>");
        }

        actuator.ApplyInstinctForce(currentDirection, WanderProtocolConfig.WanderForce, WanderProtocolConfig.MaxWanderSpeed);

        // 🌟 排障诊断：每隔 1 秒打一次速度/位移快照
        diagnosticLogTimer += Time.fixedDeltaTime;
        if (diagnosticLogTimer >= 1f)
        {
            diagnosticLogTimer = 0f;
            float currentSpeed = new Vector3(actuator.CurrentVelocity.x, 0f, actuator.CurrentVelocity.z).magnitude;
            float displacedSinceLast = Vector3.Distance(transform.position, lastLoggedPosition);
            lastLoggedPosition = transform.position;

            Debug.Log($"<color=#227777>[漫步反射诊断] 🔍 当前速度: {currentSpeed:F2} m/s (上限 {WanderProtocolConfig.MaxWanderSpeed:F1}) | 过去1s位移: {displacedSinceLast:F2}m | 当前方向: {currentDirection:F2} | 前方畅通距离: {aheadClearDistance:F2}/{WanderProtocolConfig.ObstacleLookahead:F1}</color>");
        }
    }

    private void SampleTrail()
    {
        trailSampleTimer += Time.fixedDeltaTime;
        if (trailSampleTimer < WanderProtocolConfig.TrailSampleInterval) return;
        trailSampleTimer = 0f;

        recentTrail.Add(transform.position);
        if (recentTrail.Count > WanderProtocolConfig.TrailMaxLength)
            recentTrail.RemoveAt(0);
    }

    // 🌟 候选角度以当前方向为基准左右展开，越靠前越接近"继续走原方向"——
    // 优先保持连贯，不是每次都无缘无故猛拐，走起来才自然
    private static readonly float[] CandidateAngleOffsets =
    {
        0f, -30f, 30f, -60f, 60f, -90f, 90f, -120f, 120f, -150f, 150f, 180f
    };

    private Vector3 PickNewDirection()
    {
        ScanNearbyThreats();

        Vector3 fallback = Random.insideUnitSphere;
        fallback.y = 0f;
        fallback = fallback.sqrMagnitude > 0.0001f ? fallback.normalized : transform.forward;

        Vector3 bestDirection = fallback;
        float bestScore = float.MinValue;
        bool foundValidCandidate = false;

        foreach (float offset in CandidateAngleOffsets)
        {
            Vector3 candidate = Quaternion.Euler(0f, offset, 0f) * currentDirection;
            // 加一点随机扰动，别每次都死板地只在固定的这些角度里选，走出来的路线会更自然
            candidate = Quaternion.Euler(0f, Random.Range(-WanderProtocolConfig.DirectionJitterDegrees, WanderProtocolConfig.DirectionJitterDegrees), 0f) * candidate;
            candidate.y = 0f;
            candidate.Normalize();

            float clearDistance = GetClearDistance(candidate);
            if (clearDistance < WanderProtocolConfig.ObstacleLookahead * WanderProtocolConfig.ObstacleRejectRatio)
                continue; // 太近就直接排除，不值得纳入候选

            float noveltyScore = ComputeNoveltyScore(candidate);
            float threatAvoidanceScore = ComputeThreatAvoidanceScore(candidate);
            float score = clearDistance
                + noveltyScore * WanderProtocolConfig.NoveltyWeight
                + threatAvoidanceScore * WanderProtocolConfig.ThreatAvoidanceWeight;

            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = candidate;
                foundValidCandidate = true;
            }
        }

        // 所有候选方向都太堵（比如被墙角+障碍物团团围住），退化成随便挑一个方向，好过站着不动
        return foundValidCandidate ? bestDirection : fallback;
    }

    /// <summary>
    /// 扫描雷达范围内的敌人位置，供换方向时避开。故意用跟 InstinctReflex 一样的感知半径
    /// （危险感知范围本来就该是同一件事，不用另起一个不一致的数字）。
    /// </summary>
    private void ScanNearbyThreats()
    {
        nearbyThreatPositions.Clear();

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, InstinctProtocolConfig.DangerSenseRadius, threatScanBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            var col = threatScanBuffer[i];
            if (col.gameObject == this.gameObject) continue;

            SemanticObject semanticObj = col.GetComponent<SemanticObject>();
            if (semanticObj != null && semanticObj.semanticType == SemanticType.Enemy)
                nearbyThreatPositions.Add(col.transform.position);
        }
    }

    /// <summary>
    /// 某个候选方向的落点离最近的敌人有多远——越远分数越高。权重明显高于新鲜度打分
    /// （见 WanderProtocolConfig.ThreatAvoidanceWeight），避开危险应该比"去没去过的地方"优先得多，
    /// 不然漫步系统很容易把刚从本能反射里逃出来的身体，原路带回危险区域。
    /// </summary>
    private float ComputeThreatAvoidanceScore(Vector3 direction)
    {
        if (nearbyThreatPositions.Count == 0) return 0f;

        Vector3 probePoint = transform.position + direction * WanderProtocolConfig.NoveltyProbeDistance;
        float minDistanceToThreat = float.MaxValue;

        foreach (var threatPos in nearbyThreatPositions)
        {
            float d = Vector3.Distance(probePoint, threatPos);
            if (d < minDistanceToThreat) minDistanceToThreat = d;
        }

        return minDistanceToThreat;
    }

    /// <summary>
    /// 某个候选方向的落点离"最近走过的轨迹"有多远——越远说明这个方向越"新"，
    /// 鼓励漫步倾向于往没去过的地方走，而不是在同一片开阔区域里原地打转。
    /// </summary>
    private float ComputeNoveltyScore(Vector3 direction)
    {
        if (recentTrail.Count == 0) return 0f;

        Vector3 probePoint = transform.position + direction * WanderProtocolConfig.NoveltyProbeDistance;
        float minDistanceToTrail = float.MaxValue;

        foreach (var point in recentTrail)
        {
            float d = Vector3.Distance(probePoint, point);
            if (d < minDistanceToTrail) minDistanceToTrail = d;
        }

        return minDistanceToTrail;
    }

    /// <summary>
    /// 某个方向上还有多远才会撞到东西（自己身上的碰撞体和手里正拿着的东西不算数）。
    /// 没撞到任何东西就视为在探测距离内完全畅通。
    /// </summary>
    private float GetClearDistance(Vector3 direction)
    {
        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, WanderProtocolConfig.ObstacleLookahead))
        {
            GameObject hitObject = hit.collider.gameObject;
            bool isSelfOrHeld = hitObject == this.gameObject
                || hitObject == actuator.LeftHandObject
                || hitObject == actuator.RightHandObject;

            if (!isSelfOrHeld) return hit.distance;
        }

        return WanderProtocolConfig.ObstacleLookahead;
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying || !IsWandering) return;

        // 当前漫步方向：青绿色箭头线，方便肉眼确认漫步系统在往哪走
        UnityEditor.Handles.color = new Color(0.2f, 0.8f, 0.8f, 0.9f);
        UnityEditor.Handles.DrawLine(transform.position, transform.position + currentDirection * WanderProtocolConfig.ObstacleLookahead);
#endif
    }
}
