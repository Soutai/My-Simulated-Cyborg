using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using EmbodiedAI.DTO;

[RequireComponent(typeof(Rigidbody))]
public class CharacterActuator : MonoBehaviour
{
    [Header("物理参数")]
    public float forceMultiplier = 4f;
    public float maxHorizontalSpeed = 6f;
    public float brakeForce = 8f;

    [Header("朝向参数")]
    [Tooltip("低于此水平速度不再更新朝向，防止静止时抖动")]
    public float minSpeedToUpdateFacing = 0.15f;

    [Header("挥击动画")]
    [Tooltip("绕手持物体本地哪根轴摆动——选错轴会变成棍子绕着自己的杆身自转，画面上几乎看不出在挥。" +
        "比如棍子模型本身是沿本地 Y 轴竖直建模的，选 Y 轴摆动就相当于原地拧棍子；这种情况应该选 X 或 Z。" +
        "可以在 Play 模式下直接改这个字段试，立刻能看到哪个轴才是真的\"挥出去\"的效果，不用改代码。")]
    public Vector3 swingAxisLocal = Vector3.right;
    [Tooltip("横向挥砍的摆动角度")]
    public float swingAngle = 80f;
    [Tooltip("挥出去用多久")]
    public float swingOutDuration = 0.12f;
    [Tooltip("收回来用多久，比挥出去慢一点看起来更自然")]
    public float swingBackDuration = 0.18f;

    private Rigidbody rb;

    // 🌟 纯逻辑朝向：只记录数据供 USE_ITEM 挥击判定方向使用，不用物理旋转身体。
    // 之前用 rb.MoveRotation 真的转身会莫名其妙拖慢移动速度，而胶囊体本身又没有方向标记，
    // 视觉上根本看不出转身效果，所以物理旋转纯属白白负担，直接砍掉。
    private Vector3 facingDirection = Vector3.forward;

    private GameObject leftHandObject = null;
    private GameObject rightHandObject = null;
    private bool isExecuting = false;

    public event System.Action<GameObject, string> OnGrabSuccess;
    // 🌟 新增：整个动作序列执行完毕的事件通知
    public event System.Action OnSequenceFinished;

    public GameObject LeftHandObject => leftHandObject;
    public GameObject RightHandObject => rightHandObject;
    public GameObject CurrentGrabbedObject => rightHandObject ?? leftHandObject;

    // 🌟 供视觉扇形等外部系统读取当前"正面"朝向，本质就是这具身体上一次的移动方向
    public Vector3 FacingDirection => facingDirection;

    // 🌟 专注系统：NPC 当前正在交战/追求的目标。由 LocalMotorController 在每次真正开始执行
    // 一份新的前台计划时刷新（见 UpdateFocusFromPlan），覆盖 APPROACH→USE_ITEM 全程，也覆盖
    // 两轮计划之间等待大脑网络回复的空窗期——只要大脑还没有换目标（或换成无目标的 EXPLORE），
    // 就一直算"还在专注这个目标"，不会因为一小段 2 步计划刚好执行完就重新暴露给本能。
    // 供本能反射系统甄别"这是我自己主动选择要打/要接近的目标"和"这是它自己冲过来的"，
    // 避免自己主动接近、甚至已经动手交战的目标，被误判成遭到偷袭而强行打断计划逃跑。
    public GameObject CurrentFocusTarget { get; private set; }

    // 🌟 这份专注是什么时候建立的，配合 InstinctProtocolConfig.FocusDecayTimeout 给专注设一个
    // 有效期——不能让它无限期悬空生效（比如敌人卡在攻击距离外一点点打不中人，又刚好赶上网络
    // 请求连续超时，"下一份计划开始执行"这个唯一的续期条件迟迟不会发生，NPC 会对贴脸的敌人
    // 视而不见、僵持在原地不动）。专注只会因为这里的超时、或者大脑主动换目标而解除——
    // 不会因为挨打就打断（见 UpdateFocusFromPlan 的说明）。
    private float focusSetTime = -999f;

    // 🌟 供 InstinctReflex 判断专注是否仍然有效——目标存在且没有超过有效期才算数，
    // 过期后即使 CurrentFocusTarget 还没被清空，也不再豁免它的危险贡献。
    public bool IsFocusActive =>
        CurrentFocusTarget != null && (Time.time - focusSetTime) < InstinctProtocolConfig.FocusDecayTimeout;

    // 🌟 是否真正静止（水平速度低于朝向更新阈值）。只有这时候 UpdateFacingDirection 才不会
    // 每帧覆盖朝向，HearingReflex 之类的本能转向系统才能安全地直接设置 facingDirection，
    // 不会跟"移动方向决定朝向"这条规则打架。
    public bool IsNearlyStationary => new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude < minSpeedToUpdateFacing;

    // 🌟 只读地暴露当前真实速度，供本地反射系统（诊断日志等）读取，不用各自额外持有 Rigidbody 引用
    public Vector3 CurrentVelocity => rb.linearVelocity;

    // 🌟 供 UI 面板显示"当前执行的动作"——序列外（空闲/漫步中）时为空字符串
    public string CurrentActionDescription { get; private set; } = "";

    // 🌟 供 UI 面板显示"队列里还没轮到执行的动作"——当前正在执行的那一步已经从这个列表里摘出去了
    public IReadOnlyList<PlanStep> RemainingQueuedSteps => remainingQueuedSteps;
    private readonly List<PlanStep> remainingQueuedSteps = new List<PlanStep>();

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.linearDamping = 2f;
        rb.angularDamping = 5f;
    }

    void FixedUpdate()
    {
        UpdateFacingDirection();
    }

    /// <summary>
    /// 🌟 只更新逻辑朝向，不触碰刚体旋转。USE_ITEM 挥击判定用这个方向而非 transform.forward，
    /// 这样身体一直是"哪边走得多就代表朝哪边"，不需要真的转动物理刚体。
    ///
    /// 🌟 有专注目标时例外：朝向直接锁定目标方向，不再跟随移动速度。贴身近战时两个刚体
    /// 互相挤压/分离经常会产生瞬间的反弹速度，这部分"非自愿"的速度如果也被当成"我要转向
    /// 这边"的信号，会导致 NPC 明明还在交战，却因为一次物理碰撞的瞬间反弹就"转身背对敌人"，
    /// 看起来像是撞了一下就不打了。专注状态下朝向的语义就是"我一直盯着这个目标"，不该被
    /// 物理噪声打断。
    /// </summary>
    private void UpdateFacingDirection()
    {
        if (CurrentFocusTarget != null)
        {
            Vector3 toTarget = CurrentFocusTarget.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                facingDirection = toTarget.normalized;
                return;
            }
        }

        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (horizontalVelocity.magnitude < minSpeedToUpdateFacing) return;

        facingDirection = horizontalVelocity.normalized;
    }

    /// <summary>
    /// 🌟 本能转向专用：不经过大脑、不移动身体，只是把"正面"直接转向指定方向（听到声音时用）。
    /// 只应在 IsNearlyStationary 为 true 时调用，否则下一帧就会被 UpdateFacingDirection 用真实移动方向覆盖掉。
    /// </summary>
    public void TurnFacingTowards(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            facingDirection = direction.normalized;
    }

    public void StopAllPhysicalMovement()
    {
        StopAllCoroutines();
        rb.linearVelocity = Vector3.zero;
        isExecuting = false;
        CurrentFocusTarget = null; // 协程被强制打断，不会走到 SequenceRoutine 自己收尾的清理代码，这里补上
        CurrentActionDescription = "";
        remainingQueuedSteps.Clear();
        // 确保被中断时也能通知小脑解锁
        OnSequenceFinished?.Invoke();
    }

    public void ExecutePrimitiveSequence(List<PlanStep> commands)
    {
        if (commands == null || commands.Count == 0)
        {
            OnSequenceFinished?.Invoke();
            return;
        }
        // 只有开启新序列时才会主动清理一次旧动作
        StopAllCoroutines();
        StartCoroutine(SequenceRoutine(commands));
    }

    private IEnumerator SequenceRoutine(List<PlanStep> commands)
    {
        isExecuting = true;
        remainingQueuedSteps.Clear();
        remainingQueuedSteps.AddRange(commands); // 一开始整份计划都算"排队中"

        foreach (var cmd in commands)
        {
            if (cmd == null || string.IsNullOrEmpty(cmd.arrival_op)) continue;
            if (!isExecuting) yield break;

            // 🌟 这一步要开始执行了，从"队列"里摘出来，同时记下"当前正在执行的动作"供 UI 显示
            remainingQueuedSteps.Remove(cmd);
            CurrentActionDescription = !string.IsNullOrEmpty(cmd.description) ? cmd.description : cmd.arrival_op;

            string opType = cmd.arrival_op.ToUpper().Trim();
            string TargetHand = (!string.IsNullOrEmpty(cmd.hand) && cmd.hand.ToUpper().Trim() == "LEFT") ? "LEFT" : "RIGHT";

            Debug.Log($"<color=yellow>[物理流水线] ⚙️ 开始串行执行原子动作: {opType}</color>");

            switch (opType)
            {
                case "APPLY_FORCE":
                    yield return StartCoroutine(ApplyForceSafe(cmd.arg_x, cmd.arg_z));
                    break;

                case "APPROACH":
                    if (!string.IsNullOrEmpty(cmd.target_id))
                        yield return StartCoroutine(ApproachTargetRoutine(cmd.target_id, cmd.strength));
                    break;

                case "MOVE_DIRECTION":
                    yield return StartCoroutine(MoveDirectionRoutine(cmd.arg_x, cmd.arg_z, cmd.strength));
                    break;

                case "GRAB":
                    yield return StartCoroutine(PerformGrab(cmd.target_id, TargetHand));
                    break;

                case "RELEASE":
                    PerformRelease(TargetHand);
                    yield return new WaitForSeconds(0.2f);
                    break;

                case "USE_ITEM":
                    yield return StartCoroutine(UseItemRoutine(TargetHand));
                    break;
            }
            StabilizeMovement();
        }
        isExecuting = false;
        CurrentActionDescription = "";
        // 🌟 这里不再顺手清空 CurrentFocusTarget——专注解不解除由下一份即将执行的计划决定
        // （见 UpdateFocusFromPlan），不能因为这一小段 2 步计划恰好跑完了，就在等待
        // 大脑下一轮网络回复的空窗期里让刚打过的目标突然重新暴露成"贴身威胁"。
        // 🌟 核心修复：全套动作做完了，通知小脑解开 busy 锁
        OnSequenceFinished?.Invoke();
    }

    /// <summary>
    /// 🌟 供 LocalMotorController 在每次真正开始执行一份新的前台计划前调用（无论接下来走
    /// EXPLORE 还是具体原语序列）：扫描计划里第一个点名了 target_id 的步骤（通常是 APPROACH），
    /// 解析成真正的 GameObject 作为"当前专注目标"；没有任何步骤带 target_id（比如 EXPLORE、
    /// 纯 MOVE_DIRECTION）就清空为 null，代表大脑已经不再针对某个具体物体行动。
    ///
    /// 🌟 专注只由这里（换目标/换成无目标）和 FocusDecayTimeout 超时两条规则解除——挨打不会
    /// 打断专注。大脑目标是交战时就该打到分出结果，不会因为对方还手了一下就本能性地弃战逃跑。
    /// </summary>
    public void UpdateFocusFromPlan(List<PlanStep> commands)
    {
        CurrentFocusTarget = ResolveFocusTarget(commands);
        focusSetTime = Time.time; // 每次有新计划真正开始执行，专注有效期就重新起算

        // 🌟 排障诊断：专注系统这套跨多个组件的时序（脱险→接续 backBuffer→刷新专注→开始执行）
        // 只靠读代码很难确认"这一刻专注到底有没有真的生效"，直接打日志比推演时序快得多。
        Debug.Log($"<color=#8888FF>[专注系统] 🎯 专注目标刷新为: {(CurrentFocusTarget != null ? CurrentFocusTarget.name : "无")}</color>");
    }

    /// <summary>
    /// 🌟 公开出来单纯给 LocalMotorController 只读探测用（比如"排队中的 backBuffer 计划点名的
    /// 是谁"），不会顺带修改 CurrentFocusTarget——那个字段只应该反映"真正在执行"的计划。
    /// </summary>
    public GameObject ResolveFocusTarget(List<PlanStep> commands)
    {
        foreach (var cmd in commands)
        {
            if (cmd == null || string.IsNullOrEmpty(cmd.target_id)) continue;
            GameObject resolved = WorldObjectRegistry.Find(cmd.target_id) ?? WorldObjectRegistry.FindFuzzy(cmd.target_id);
            if (resolved != null) return resolved;
        }
        return null;
    }

    /// <summary>
    /// 🌟 两个物体之间真正"贴近程度"的判断：用目标碰撞体表面最近点的距离，而不是两个
    /// Transform 原点之间的直线距离。细长/pivot 不在几何中心的物体（手持武器一类的道具经常
    /// 把 pivot 放在末端，方便贴到手上）用直线距离判断"够不够近"会有系统性偏差——身体明明
    /// 已经贴到物体表面了，两个 pivot 之间的距离读数依然很大，导致配置的停止线/抓取距离
    /// 永远摸不到、物理上却已经顶到头了（表现成 ApproachTargetRoutine 里的"检测到物理地形
    /// 死锁"，以及 GRAB 反复因为"距离过远"失败）。目标没有 Collider 时退化回原来的直线距离，
    /// 不引入新的失败模式。
    /// </summary>
    private float GetSurfaceDistance(GameObject target)
    {
        Collider targetCollider = target.GetComponent<Collider>();
        if (targetCollider == null) return Vector3.Distance(transform.position, target.transform.position);

        Vector3 closestPoint = targetCollider.ClosestPoint(transform.position);
        return Vector3.Distance(transform.position, closestPoint);
    }

    private IEnumerator ApproachTargetRoutine(string targetId, float strength = 1f)
    {
        GameObject target = WorldObjectRegistry.Find(targetId) ?? WorldObjectRegistry.FindFuzzy(targetId);
        if (target == null)
        {
            Debug.LogWarning($"[APPROACH] 找不到目标: {targetId}");
            yield break;
        }

        // ==================== 【新配置系统】优先读取 ====================
        // 找不到 SemanticObject 时不再瞎猜类型，直接用中性的默认停止距离
        float desiredDistance = 0.65f;
        var semantic = target.GetComponent<SemanticObject>();

        if (semantic != null)
        {
            desiredDistance = semantic.GetDesiredApproachDistance();
        }

        float maxTime = (strength > 1.2f) ? 4.0f : 5.0f;
        float timer = 0f;
        float lastDistance = float.MaxValue;
        float stuckTimer = 0f;

        Debug.Log($"<color=orange>[APPROACH] ⚙️ 通用物理推进启动 → 目标: {targetId} | 配置停止线: {desiredDistance:F2}m | 最大时间: {maxTime}s</color>");

        while (timer < maxTime && isExecuting && target != null)
        {
            float currentDistance = GetSurfaceDistance(target);
            float distanceToGap = currentDistance - desiredDistance;
            float currentSpeed = rb.linearVelocity.magnitude;

            // 诊断日志
            //if (timer % 0.25f < Time.deltaTime * 1.05f)
            //{
            //    Debug.Log($"<color=white>[APPROACH] 实时状态 | 时间:{timer:F2}s | 距离:{currentDistance:F2}m | Gap:{distanceToGap:F2}m | 速度:{currentSpeed:F2} | 配置距离:{desiredDistance:F2}</color>");
            //}

            // 退出条件（使用配置的距离 + 合理容差）
            if (currentDistance <= desiredDistance + 0.18f && currentSpeed < 1.3f)
            {
                Debug.Log($"<color=green>[APPROACH] ✅ 满足送达条件 {targetId}（实际 {currentDistance:F2}m，配置 {desiredDistance:F2}m，耗时 {timer:F1}s）</color>");
                break;
            }

            if (currentDistance <= desiredDistance ||
                (distanceToGap <= 0.22f && currentSpeed < 1.1f))
            {
                Debug.Log($"<color=green>[APPROACH] ✅ 精确满足停止条件 {targetId}（{currentDistance:F2}m）</color>");
                break;
            }

            // 防卡死检测
            if (currentDistance > 1.2f && Mathf.Abs(currentDistance - lastDistance) < 0.025f)
            {
                stuckTimer += Time.deltaTime;
            }
            else
            {
                stuckTimer = 0f;
            }

            if (stuckTimer > 0.9f)
            {
                Debug.LogWarning($"<color=yellow>[APPROACH] ⚠️ 检测到物理地形死锁，强制结束 → {targetId}</color>");
                break;
            }

            lastDistance = currentDistance;

            // ====================== 你原有的物理逻辑（完全保留）======================
            Vector3 direction = (target.transform.position - transform.position).normalized;
            direction.y = 0f;

            float dynamicForce = forceMultiplier * 4.2f * strength;

            if (distanceToGap < 3.0f)
            {
                float slowdownFactor = Mathf.SmoothStep(0.2f, 1.0f, distanceToGap / 3.0f);
                dynamicForce *= slowdownFactor;

                if (currentSpeed < 2.0f && distanceToGap > 0.01f)
                {
                    float compMultiplier = Mathf.Clamp01(distanceToGap / 1.5f);
                    dynamicForce += (2.0f - currentSpeed) * forceMultiplier * 2.5f * compMultiplier;
                }
            }

            if (dynamicForce > 0.05f)
            {
                rb.AddForce(direction * dynamicForce, ForceMode.Force);
            }

            LimitHorizontalSpeed();

            if (strength < 1.2f)
            {
                float safeArrivalSpeed = Mathf.Clamp(distanceToGap * 3.0f, 0.2f, maxHorizontalSpeed);
                if (distanceToGap < 1.5f && currentSpeed > safeArrivalSpeed)
                {
                    rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, direction * safeArrivalSpeed, brakeForce * Time.deltaTime * 1.5f);
                }
            }
            // =====================================================================

            timer += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (timer >= maxTime)
        {
            float finalDist = GetSurfaceDistance(target);
            Debug.LogWarning($"<color=red>[APPROACH] ⚠️ 达到 {maxTime}s 安全帽强制结束 → {targetId}，最终距离: {finalDist:F2}m</color>");
        }
        else
        {
            Debug.Log($"<color=green>[APPROACH] 正常结束 → {targetId}，总耗时 {timer:F1}s</color>");
        }
    }

    private IEnumerator MoveDirectionRoutine(float argX, float argZ, float strength = 1f)
    {
        Vector3 direction = new Vector3(argX, 0f, argZ).normalized;
        float duration = 1.0f;
        float timer = 0f;

        while (timer < duration)
        {
            if (!isExecuting) yield break;

            Vector3 force = direction * forceMultiplier * 3f * strength;
            rb.AddForce(force, ForceMode.Force);

            LimitHorizontalSpeed();
            timer += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, 0.85f);
    }

    private IEnumerator ApplyForceSafe(float argX, float argZ)
    {
        Vector3 direction = new Vector3(argX, 0f, argZ).normalized;
        float strength = Mathf.Clamp(Mathf.Sqrt(argX * argX + argZ * argZ), 0f, 5f);

        Vector3 impulse = direction * strength * forceMultiplier;
        rb.AddForce(impulse, ForceMode.Impulse);

        float timer = 0.4f;
        while (timer > 0f)
        {
            LimitHorizontalSpeed();
            timer -= Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, 0.6f);
        StabilizeMovement();
    }

    private void LimitHorizontalSpeed()
    {
        Vector3 vel = rb.linearVelocity;
        Vector3 horizontalVel = new Vector3(vel.x, 0, vel.z);
        if (horizontalVel.magnitude > maxHorizontalSpeed)
        {
            Vector3 limitedVel = horizontalVel.normalized * maxHorizontalSpeed;
            rb.linearVelocity = new Vector3(limitedVel.x, vel.y, limitedVel.z);
        }
    }

    private void StabilizeMovement()
    {
        Vector3 vel = rb.linearVelocity;
        if (vel.y > 2f) vel.y = 2f;
        rb.linearVelocity = vel;
    }

    /// <summary>
    /// 🌟 USE_ITEM 的执行入口。是否要在本地反复重复完全由物品自己的配置
    /// （PhysicsProtocolConfig.ItemUseEffect.isContinuousUse）决定，这里不针对"是不是武器"
    /// 写 if-else——不连续的效果（比如吃东西）行为跟以前完全一样，触发一次就结束；
    /// 连续的效果（比如挥棍子）按配置的节奏反复触发，直到分出结果、目标够不着、或者超时，
    /// 期间不需要大脑每挥一下都重新决策一次。
    /// </summary>
    private IEnumerator UseItemRoutine(string hand)
    {
        string sanitizedHand = (hand ?? "").ToUpper().Trim();
        GameObject activeObject = (sanitizedHand == "LEFT") ? leftHandObject : rightHandObject;

        if (activeObject == null)
        {
            Debug.LogWarning($"[USE_ITEM] 失败：【{sanitizedHand}手】空无一物，必须先 GRAB 才能使用");
            yield break;
        }

        SemanticObject semantic = activeObject.GetComponent<SemanticObject>();
        if (semantic == null) yield break;

        PhysicsProtocolConfig.ItemUseEffect effect = PhysicsProtocolConfig.GetUseEffect(semantic.semanticType);

        if (!effect.isContinuousUse)
        {
            ApplyUseEffect(effect, activeObject, sanitizedHand);
            yield return new WaitForSeconds(0.4f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < effect.continuousMaxDuration && isExecuting)
        {
            // 🌟 每一轮都重新确认——物品可能中途被松开，专注目标可能已经消失（打死了）
            // 或者跑出了判定范围（够不着了，交回给大脑基于最新处境重新规划）。
            activeObject = (sanitizedHand == "LEFT") ? leftHandObject : rightHandObject;
            if (activeObject == null) yield break;

            GameObject target = CurrentFocusTarget;
            if (target == null || !target.activeInHierarchy) yield break;

            if (GetSurfaceDistance(target) > effect.effectRadius) yield break;

            ApplyUseEffect(effect, activeObject, sanitizedHand);

            float waitTimer = effect.continuousInterval;
            while (waitTimer > 0f)
            {
                if (!isExecuting) yield break;
                waitTimer -= Time.deltaTime;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    /// <summary>
    /// 🌟 通用 USE_ITEM 效果分发器：只认配置中心下发的效果类型和参数，
    /// 不针对任何具体 SemanticType 写 if-else —— 物体是什么效果，完全由 PhysicsProtocolConfig 决定。
    /// </summary>
    private void ApplyUseEffect(PhysicsProtocolConfig.ItemUseEffect effect, GameObject target, string hand)
    {
        switch (effect.kind)
        {
            case PhysicsProtocolConfig.UseEffectKind.SweepAttack:
                Debug.Log($"<color=yellow>[物理交互] 原始人挥舞了【{hand}手】的{target.name}！</color>");
                PerformSweepAttack(effect, ResolveAttackDirection());
                StartCoroutine(SwingAnimationRoutine(target));
                break;

            case PhysicsProtocolConfig.UseEffectKind.Consume:
                NPCAttributes attr = GetComponent<NPCAttributes>();
                if (attr) attr.RestoreSatiety(target, effect.satietyRestore);

                if (leftHandObject == target) leftHandObject = null;
                else if (rightHandObject == target) rightHandObject = null;

                Destroy(target);
                break;

            case PhysicsProtocolConfig.UseEffectKind.None:
            default:
                break;
        }
    }

    /// <summary>
    /// 🌟 挥击方向优先瞄准当前专注目标（我正在交战的对象），不用 facingDirection——APPROACH
    /// 停下来的那一刻，身体朝向只是"刚才走路走出来的方向"的副产品，不保证正对着一个会动的目标
    /// （尤其目标本身在靠近/游荡时更容易对不上），会导致横扫判定球心偏离目标实际位置、白挥一下。
    /// 没有专注目标时（比如本能反射的赤手反击，没有明确交战对象）才退回 facingDirection。
    /// </summary>
    private Vector3 ResolveAttackDirection()
    {
        if (CurrentFocusTarget != null)
        {
            Vector3 toTarget = CurrentFocusTarget.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f) return toTarget.normalized;
        }
        return facingDirection;
    }

    /// <summary>
    /// 🌟 纯视觉层的挥砍动画：只旋转手持物体自己的 localRotation，不碰判定逻辑——
    /// PerformSweepAttack 的命中判定依然是瞬间的 OverlapSphere，这里只是让"挥了一下"这件事
    /// 在画面上真正看得见，而不是棍子一动不动地举在手上。GRAB 时物体的静止姿态是
    /// localRotation = identity，挥完会自动收回这个姿态，不会打飞角度。
    /// </summary>
    private IEnumerator SwingAnimationRoutine(GameObject item)
    {
        if (item == null) yield break;

        Quaternion restRotation = item.transform.localRotation;
        Quaternion swungRotation = restRotation * Quaternion.AngleAxis(swingAngle, swingAxisLocal);

        float t = 0f;
        while (t < swingOutDuration)
        {
            if (item == null) yield break; // 挥砍过程中物体可能被松开/摧毁
            t += Time.deltaTime;
            item.transform.localRotation = Quaternion.Slerp(restRotation, swungRotation, t / swingOutDuration);
            yield return null;
        }

        t = 0f;
        while (t < swingBackDuration)
        {
            if (item == null) yield break;
            t += Time.deltaTime;
            item.transform.localRotation = Quaternion.Slerp(swungRotation, restRotation, t / swingBackDuration);
            yield return null;
        }

        item.transform.localRotation = restRotation;
    }

    /// <summary>
    /// 横扫判定的共享实现：只认方向参数，不管这个方向是精确朝向（正常 USE_ITEM）
    /// 还是带随机抖动的乱挥方向（本能反射的惊跳反应）。
    /// </summary>
    private void PerformSweepAttack(PhysicsProtocolConfig.ItemUseEffect effect, Vector3 direction)
    {
        Vector3 sphereCenter = transform.position + direction * effect.forwardOffset;
        Collider[] hits = Physics.OverlapSphere(sphereCenter, effect.effectRadius);

        bool hitAnyTaggedTarget = false;
        foreach (var h in hits)
        {
            if (h.CompareTag(effect.affectedTag))
            {
                hitAnyTaggedTarget = true;
                Rigidbody targetRb = h.GetComponent<Rigidbody>();
                if (targetRb)
                {
                    targetRb.AddForce((h.transform.position - transform.position).normalized * effect.knockbackForce, ForceMode.Impulse);

                    // 🌟 直接同步上报这次冲击的等效速度（冲量 / 目标质量），不等下一帧再被动
                    // 读取 rb.linearVelocity——目标身上如果还有别的脚本在同一物理帧内改它的
                    // 速度（比如敌人的追击限速），被动读取会被"抢跑"，导致明明打中了却检测
                    // 不到冲击，见 UniversalPhysicsEntity.ReportDirectImpact 的说明。
                    UniversalPhysicsEntity physicsEntity = h.GetComponent<UniversalPhysicsEntity>();
                    if (physicsEntity != null)
                    {
                        float impactSpeed = effect.knockbackForce / targetRb.mass;
                        physicsEntity.ReportDirectImpact(impactSpeed);
                    }
                }
            }
        }

        // 🌟 排障诊断：命中判定失败时直接说清楚是"判定球范围内空无一物"还是"扫到了东西但
        // Tag 不对"，不用再靠猜——两种失败原因对应完全不同的排查方向（前者是方位/距离问题，
        // 后者是预制体 Tag/Collider 挂载层级配置问题）。
        if (!hitAnyTaggedTarget)
        {
            if (hits.Length == 0)
            {
                Debug.Log($"<color=#FF8800>[物理交互诊断] 🔍 横扫判定球范围内没有扫到任何碰撞体（球心: {sphereCenter:F2}，半径: {effect.effectRadius:F2}）</color>");
            }
            else
            {
                string hitList = string.Join(", ", System.Array.ConvertAll(hits, h => $"{h.gameObject.name}(Tag={h.tag})"));
                Debug.Log($"<color=#FF8800>[物理交互诊断] 🔍 横扫判定球扫到了 {hits.Length} 个碰撞体，但没有一个 Tag 是 \"{effect.affectedTag}\"：{hitList}</color>");
            }
        }
    }

    /// <summary>
    /// 🌟 本地反射专用：不经过大脑指令、持续朝某个方向施力。通用的"给个方向和力度，
    /// 身体就一直往那边走"接口，逃跑用的 InstinctReflex 和漫步探索用的 WanderReflex 共用这一个方法。
    /// </summary>
    public void ApplyInstinctForce(Vector3 direction, float force, float maxSpeed)
    {
        rb.AddForce(direction * force, ForceMode.Force);

        Vector3 vel = rb.linearVelocity;
        Vector3 horizontalVel = new Vector3(vel.x, 0f, vel.z);
        if (horizontalVel.magnitude > maxSpeed)
        {
            Vector3 limited = horizontalVel.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(limited.x, vel.y, limited.z);
        }
    }

    /// <summary>
    /// 🌟 本能反射专用：卡死急救冲量。持续的平稳推力（ApplyInstinctForce）在身体被障碍物
    /// 挤压卡死时会被摩擦力/挤压反力完全吃掉，纹丝不动——这时候需要一次远超日常强度的瞬间冲量，
    /// 才有机会把身体从卡死的缝隙里"弹"出去，而不是继续无效地慢慢推。
    /// </summary>
    public void ApplyInstinctUnstickImpulse(Vector3 direction, float impulse)
    {
        rb.AddForce(direction * impulse, ForceMode.Impulse);
    }

    /// <summary>
    /// 🌟 本能反射专用：赤手空拳反击。被逼到贴身且逃不掉时的最后手段，
    /// 复用武器横扫同一套物理击退机制，只是力度明显弱于武器（见 PhysicsProtocolConfig.UnarmedPunchEffect）。
    /// </summary>
    public void ApplyInstinctPunch(Vector3 direction)
    {
        PerformSweepAttack(PhysicsProtocolConfig.UnarmedPunchEffect, direction);
    }

    private void PerformRelease(string hand)
    {
        string sanitizedHand = (hand ?? "").ToUpper().Trim();
        GameObject activeObject = (sanitizedHand == "LEFT") ? leftHandObject : rightHandObject;

        if (activeObject != null)
        {
            var targetRb = activeObject.GetComponent<Rigidbody>();
            if (targetRb != null)
                targetRb.isKinematic = false;

            activeObject.transform.SetParent(null);

            Debug.Log($"<color=cyan>[物理原语] 松开【{sanitizedHand}手】物体: {activeObject.name}</color>");

            if (sanitizedHand == "LEFT") leftHandObject = null;
            else rightHandObject = null;
        }
    }

    private IEnumerator PerformGrab(string targetId, string hand)
    {
        string sanitizedHand = (hand ?? "").ToUpper().Trim();
        GameObject activeObject = (sanitizedHand == "LEFT") ? leftHandObject : rightHandObject;
        bool hasExplicitTarget = !string.IsNullOrEmpty(targetId);

        // ==================== 诊断日志 ====================
        Debug.Log($"<color=cyan>[GRAB 开始诊断] 尝试用【{sanitizedHand}手】抓取 {(hasExplicitTarget ? targetId : "(未指定 target_id，就近搜索可抓取物体)")}</color>");

        if (activeObject != null)
        {
            Debug.LogWarning($"[物理原语] GRAB 失败：【{sanitizedHand}手】已有物体");
            yield break;
        }

        // 🌟 容错：大模型偶尔会漏填 target_id（比如刚 APPROACH 完觉得"该抓什么很明显"），
        // 这时退化成抓取双手可及范围内最近的可抓取物体，而不是直接判定失败。
        GameObject target = hasExplicitTarget
            ? (WorldObjectRegistry.Find(targetId) ?? WorldObjectRegistry.FindFuzzy(targetId))
            : FindNearestGraspableObject();

        if (target != null)
        {
            float currentDist = GetSurfaceDistance(target);

            float maxGraspDistance = 1.25f;
            var semantic = target.GetComponent<SemanticObject>();
            if (semantic != null) maxGraspDistance = semantic.GetMaxGraspDistance();

            Debug.Log($"<color=cyan>[GRAB 距离诊断] {target.name} | 当前实际距离: {currentDist:F2}m | 期望抓取距离 <= {maxGraspDistance:F2}m</color>");

            if (currentDist <= maxGraspDistance)
            {
                if (target == leftHandObject || target == rightHandObject)
                {
                    Debug.LogWarning($"[物理原语] GRAB 失败：目标已被另一只手持有");
                    yield break;
                }

                if (sanitizedHand == "LEFT") leftHandObject = target;
                else rightHandObject = target;

                rb.linearVelocity = Vector3.zero;
                yield return new WaitForSeconds(0.1f);

                var targetRb = target.GetComponent<Rigidbody>();
                if (targetRb != null) targetRb.isKinematic = true;

                target.transform.SetParent(transform);
                float xOffset = (sanitizedHand == "LEFT") ? -0.4f : 0.4f;
                target.transform.localPosition = new Vector3(xOffset, 0.8f, 1.0f);
                target.transform.localRotation = Quaternion.identity;

                Debug.Log($"<color=cyan>[物理原语] 成功用【{sanitizedHand}手】抓取: {target.name}</color>");

                OnGrabSuccess?.Invoke(target, sanitizedHand);
            }
            else
            {
                Debug.LogWarning($"[GRAB] 失败：{target.name} 距离过远 ({currentDist:F2}m > {maxGraspDistance:F2}m)");
            }
        }
        else
        {
            Debug.LogWarning(hasExplicitTarget
                ? $"[物理原语] GRAB 失败：找不到目标 {targetId}"
                : "[物理原语] GRAB 失败：附近没有可抓取的物体");
        }
    }

    /// <summary>
    /// target_id 缺省时的兜底：在注册表里找双手可及范围内最近的可抓取物体。
    /// </summary>
    private GameObject FindNearestGraspableObject()
    {
        GameObject nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var semantic in WorldObjectRegistry.All())
        {
            if (semantic == null) continue;

            float dist = GetSurfaceDistance(semantic.gameObject);
            if (dist <= semantic.GetMaxGraspDistance() && dist < nearestDist)
            {
                nearestDist = dist;
                nearest = semantic.gameObject;
            }
        }

        return nearest;
    }
}