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

    private Rigidbody rb;

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

    public void StopAllPhysicalMovement()
    {
        StopAllCoroutines();
        rb.linearVelocity = Vector3.zero;
        isExecuting = false;
        // 确保被中断时也能通知小脑解锁
        OnSequenceFinished?.Invoke();
    }

    public void ExecutePrimitiveSequence(List<PrimitiveCommand> commands, UnityEngine.UI.Text actionDisplay)
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

    private IEnumerator SequenceRoutine(List<PrimitiveCommand> commands, UnityEngine.UI.Text actionDisplay)
    {
        isExecuting = true;
        foreach (var cmd in commands)
        {
            if (cmd == null || string.IsNullOrEmpty(cmd.op)) continue;
            if (!isExecuting) yield break;

            string opType = cmd.op.ToUpper().Trim();
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
        GameObject target = GameObject.Find(targetId);
        if (target == null)
        {
            Debug.LogWarning($"[APPROACH] 找不到目标: {targetId}");
            yield break;
        }

        // ==================== 【新配置系统】优先读取 ====================
        float desiredDistance = 0.65f;
        var semantic = target.GetComponent<SemanticObject>();

        if (semantic != null)
        {
            desiredDistance = semantic.GetDesiredApproachDistance();
        }
        else
        {
            // 兜底：通过 SemanticType 查询全局配置
            var config = SandboxProtocolConfig.GetInteractionConfig(SemanticType.Food); // 默认Food
                                                                                        // 如果能从名字或其他方式推断类型，可以进一步优化
            desiredDistance = config.desiredApproachDistance;
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

        if (activeObject != null && activeObject.name.Contains("Stick"))
        {
            Debug.Log($"<color=yellow>[物理交互] 原始人挥舞了【{sanitizedHand}手】的木棍！</color>");
            Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * 1f, 2f);
            foreach (var h in hits)
            {
                if (h.CompareTag("Enemy"))
                {
                    Rigidbody enemyRb = h.GetComponent<Rigidbody>();
                    if (enemyRb)
                        enemyRb.AddForce((h.transform.position - transform.position).normalized * 30f, ForceMode.Impulse);
                }
            }
            return;
        }

        if (activeObject != null && (activeObject.name.Contains("Fruit") || activeObject.CompareTag("Food")))
        {
            NPCAttributes attr = GetComponent<NPCAttributes>();
            if (attr) attr.satiety = Mathf.Clamp(attr.satiety + 15f, 0f, 100f);

            Debug.Log($"<color=green>[物理交互] 🍎 用【{sanitizedHand}手】吃掉了手里拿着的 {activeObject.name}！</color>");

            if (sanitizedHand == "LEFT") leftHandObject = null;
            else rightHandObject = null;

            Destroy(activeObject);
            return;
        }

        Collider[] closeObjects = Physics.OverlapSphere(transform.position, 0.8f);
        foreach (var col in closeObjects)
        {
            if (col.CompareTag("Food"))
            {
                NPCAttributes attr = GetComponent<NPCAttributes>();
                if (attr) attr.satiety = Mathf.Clamp(attr.satiety + 15f, 0f, 100f);
                Debug.Log($"<color=green>[物理交互] 🍎 消耗了地上的果子进食！</color>");

                Destroy(col.gameObject);
                break;
            }
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
        if (string.IsNullOrEmpty(targetId))
        {
            Debug.LogWarning($"[GRAB] 失败：targetId 为空");
            yield break;
        }

        string sanitizedHand = (hand ?? "").ToUpper().Trim();
        GameObject activeObject = (sanitizedHand == "LEFT") ? leftHandObject : rightHandObject;

        // ==================== 诊断日志 ====================
        Debug.Log($"<color=cyan>[GRAB 开始诊断] 尝试用【{sanitizedHand}手】抓取 {targetId}</color>");

        if (activeObject != null)
        {
            Debug.LogWarning($"[物理原语] GRAB 失败：【{sanitizedHand}手】已有物体");
            yield break;
        }

        GameObject target = GameObject.Find(targetId);
        if (target == null)
        {
            var semanticObjs = FindObjectsOfType<SemanticObject>();
            foreach (var sobj in semanticObjs)
            {
                if (sobj.gameObject.name.Contains(targetId) || targetId.Contains(sobj.gameObject.name))
                {
                    target = sobj.gameObject;
                    break;
                }
            }
        }

        if (target != null)
        {
            float currentDist = Vector3.Distance(transform.position, target.transform.position);
            Debug.Log($"<color=cyan>[GRAB 距离诊断] {targetId} | 当前实际距离: {currentDist:F2}m | 期望抓取距离 <= 1.2m</color>");

            if (currentDist <= 1.2f)
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
                Debug.LogWarning($"[GRAB] 失败：{targetId} 距离过远 ({currentDist:F2}m > 4.2m)");
            }
        }
        else
        {
            Debug.LogWarning($"[物理原语] GRAB 失败：找不到目标 {targetId}");
        }
    }
}