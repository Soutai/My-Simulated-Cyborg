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
    /// </summary>
    private void UpdateFacingDirection()
    {
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (horizontalVelocity.magnitude < minSpeedToUpdateFacing) return;

        facingDirection = horizontalVelocity.normalized;
    }

    public void StopAllPhysicalMovement()
    {
        StopAllCoroutines();
        rb.linearVelocity = Vector3.zero;
        isExecuting = false;
        // 确保被中断时也能通知小脑解锁
        OnSequenceFinished?.Invoke();
    }

    public void ExecutePrimitiveSequence(List<PlanStep> commands, UnityEngine.UI.Text actionDisplay)
    {
        if (commands == null || commands.Count == 0)
        {
            OnSequenceFinished?.Invoke();
            return;
        }
        // 只有开启新序列时才会主动清理一次旧动作
        StopAllCoroutines();
        StartCoroutine(SequenceRoutine(commands, actionDisplay));
    }

    private IEnumerator SequenceRoutine(List<PlanStep> commands, UnityEngine.UI.Text actionDisplay)
    {
        isExecuting = true;
        foreach (var cmd in commands)
        {
            if (cmd == null || string.IsNullOrEmpty(cmd.arrival_op)) continue;
            if (!isExecuting) yield break;

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
                    TriggerUseLogic(TargetHand);
                    yield return new WaitForSeconds(0.4f);
                    break;
            }
            StabilizeMovement();
        }
        isExecuting = false;
        // 🌟 核心修复：全套动作做完了，通知小脑解开 busy 锁
        OnSequenceFinished?.Invoke();
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
            float currentDistance = Vector3.Distance(transform.position, target.transform.position);
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
            float finalDist = Vector3.Distance(transform.position, target.transform.position);
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

    private void TriggerUseLogic(string hand)
    {
        string sanitizedHand = (hand ?? "").ToUpper().Trim();
        GameObject activeObject = (sanitizedHand == "LEFT") ? leftHandObject : rightHandObject;

        if (activeObject == null)
        {
            Debug.LogWarning($"[USE_ITEM] 失败：【{sanitizedHand}手】空无一物，必须先 GRAB 才能使用");
            return;
        }

        SemanticObject semantic = activeObject.GetComponent<SemanticObject>();
        if (semantic == null) return;

        ApplyUseEffect(PhysicsProtocolConfig.GetUseEffect(semantic.semanticType), activeObject, sanitizedHand);
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
                Collider[] hits = Physics.OverlapSphere(transform.position + facingDirection * effect.forwardOffset, effect.effectRadius);
                foreach (var h in hits)
                {
                    if (h.CompareTag(effect.affectedTag))
                    {
                        Rigidbody targetRb = h.GetComponent<Rigidbody>();
                        if (targetRb)
                            targetRb.AddForce((h.transform.position - transform.position).normalized * effect.knockbackForce, ForceMode.Impulse);
                    }
                }
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
            float currentDist = Vector3.Distance(transform.position, target.transform.position);

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

            float dist = Vector3.Distance(transform.position, semantic.transform.position);
            if (dist <= semantic.GetMaxGraspDistance() && dist < nearestDist)
            {
                nearestDist = dist;
                nearest = semantic.gameObject;
            }
        }

        return nearest;
    }
}