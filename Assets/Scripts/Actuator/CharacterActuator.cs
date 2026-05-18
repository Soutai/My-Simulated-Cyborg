using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using EmbodiedAI.DTO;

[RequireComponent(typeof(Rigidbody))]
public class CharacterActuator : MonoBehaviour
{
    [Header("物理参数")]
    public float forceMultiplier = 4f;        // 降低一点强度，更易控
    public float maxHorizontalSpeed = 6f;     // 新增：最大水平速度限制
    public float brakeForce = 8f;             // 刹车强度

    private Rigidbody rb;

    // 🌟【双手升级】将单手转换为独立的左右手持物变量
    private GameObject leftHandObject = null;
    private GameObject rightHandObject = null;
    private bool isExecuting = false;

    // 🌟【安全公开接口】供外部雷达清洗和状态查询使用
    public GameObject LeftHandObject => leftHandObject;
    public GameObject RightHandObject => rightHandObject;
    public GameObject CurrentGrabbedObject => rightHandObject ?? leftHandObject; // 向下兼容旧逻辑

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.linearDamping = 2f;                    // 增加空气阻力
        rb.angularDamping = 5f;
    }

    public void StopAllPhysicalMovement()
    {
        StopAllCoroutines();
        rb.linearVelocity = Vector3.zero;
        isExecuting = false;
    }

    public void ExecutePrimitiveSequence(List<PrimitiveCommand> commands, UnityEngine.UI.Text actionDisplay)
    {
        if (commands == null || commands.Count == 0) return;

        StopAllPhysicalMovement();
        StartCoroutine(SequenceRoutine(commands, actionDisplay));
    }

    private IEnumerator SequenceRoutine(List<PrimitiveCommand> commands, UnityEngine.UI.Text actionDisplay)
    {
        isExecuting = true;

        foreach (var cmd in commands)
        {
            // 1. 极致安全防线：同时过滤 cmd 为空 以及 cmd.op 为空的情况
            if (cmd == null || string.IsNullOrEmpty(cmd.op))
            {
                Debug.LogWarning("[物理原语] 过滤掉了一个空的命令或无效的操作名(op 为空)。");
                continue;
            }
            if (!isExecuting) yield break;

            try
            {
                if (actionDisplay != null)
                    actionDisplay.text = $"正在执行: {cmd.op}";
            }
            catch { }

            string opType = cmd.op.ToUpper().Trim();
            // 🌟 获取当前命令指定的手，默认归为右手
            string TargetHand = (!string.IsNullOrEmpty(cmd.hand) && cmd.hand.ToUpper().Trim() == "LEFT") ? "LEFT" : "RIGHT";

            switch (opType)
            {
                case "APPLY_FORCE":
                    yield return StartCoroutine(ApplyForceSafe(cmd.arg_x, cmd.arg_z));
                    break;

                case "GRAB":
                    // 🌟 路由到双手安全的抓取逻辑
                    yield return StartCoroutine(PerformGrab(cmd.target_id, TargetHand));
                    break;

                case "RELEASE":
                    // 🌟 路由到双手安全的释放逻辑
                    PerformRelease(TargetHand);
                    yield return new WaitForSeconds(0.2f);
                    break;

                case "USE_ITEM":
                    // 🌟 传入指定的手，精准触发该手持物的对应逻辑
                    TriggerUseLogic(TargetHand);
                    yield return new WaitForSeconds(0.4f);
                    break;

                default:
                    Debug.LogWarning($"[物理原语] 未知的操作命令: {opType}");
                    break;
            }

            StabilizeMovement();
        }

        try
        {
            if (actionDisplay != null)
                actionDisplay.text = "原子序列执行完毕";
        }
        catch { }

        isExecuting = false;
    }

    // ==================== 核心修复：安全的推力实现（100%保留原逻辑） ====================
    private IEnumerator ApplyForceSafe(float argX, float argZ)
    {
        Vector3 direction = new Vector3(argX, 0f, argZ).normalized;
        float strength = Mathf.Clamp(Mathf.Sqrt(argX * argX + argZ * argZ), 0f, 5f); // 限制强度

        Vector3 impulse = direction * strength * forceMultiplier;

        // 单次施加 Impulse（更稳定，不容易叠加）
        rb.AddForce(impulse, ForceMode.Impulse);

        // 持续一小段时间并限制速度
        float timer = 0.4f;
        while (timer > 0f)
        {
            // 速度限制
            LimitHorizontalSpeed();
            timer -= Time.deltaTime;
            yield return null;
        }

        // 强力刹车
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
        if (vel.y > 2f) vel.y = 2f;           // 防止向上飞
        rb.linearVelocity = vel;
    }

    // ==================== 🌟 双手重构核心：带“手”路由的物理交互逻辑 ====================
    private void TriggerUseLogic(string hand)
    {
        // 🌟 修正：确保传入的参数做大写鲁棒性处理
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

        // 吃食物逻辑
        Collider[] closeObjects = Physics.OverlapSphere(transform.position, 0.8f);
        foreach (var col in closeObjects)
        {
            if (col.CompareTag("Food"))
            {
                // 🌟 额外安全防线：如果要吃的果子正好就是手里的这个，或者是地上的同类果子，都允许触发
                NPCAttributes attr = GetComponent<NPCAttributes>();
                if (attr) attr.satiety = Mathf.Clamp(attr.satiety + 15f, 0f, 100f);
                Debug.Log($"<color=green>[物理交互] 🍎 消耗了【{sanitizedHand}手】附近的果子进食！</color>");

                // 如果刚好把手里拿着的这个果子吃了，清空对应手的引用
                if (col.gameObject == activeObject)
                {
                    if (sanitizedHand == "LEFT") leftHandObject = null;
                    else rightHandObject = null;
                }

                Destroy(col.gameObject);
                break;
            }
        }
    }

    private void PerformRelease(string hand)
    {
        // 🌟 修正：确保传入的参数做大写鲁棒性处理
        string sanitizedHand = (hand ?? "").ToUpper().Trim();
        GameObject activeObject = (sanitizedHand == "LEFT") ? leftHandObject : rightHandObject;

        if (activeObject != null)
        {
            var targetRb = activeObject.GetComponent<Rigidbody>();
            if (targetRb != null)
                targetRb.isKinematic = false;

            activeObject.transform.SetParent(null);
            Debug.Log($"<color=cyan>[物理原语] 松开了【{sanitizedHand}手】的物体: {activeObject.name}</color>");

            if (sanitizedHand == "LEFT") leftHandObject = null;
            else rightHandObject = null;
        }
    }

    private IEnumerator PerformGrab(string targetId, string hand)
    {
        if (string.IsNullOrEmpty(targetId))
        {
            yield return new WaitForSeconds(0.1f);
            yield break;
        }

        // 🌟 修正：确保传入的参数做大写鲁棒性处理
        string sanitizedHand = (hand ?? "").ToUpper().Trim();

        // 检查该手是否已经持物，若有则不允许覆盖抓取（必须先 RELEASE）
        GameObject activeObject = (sanitizedHand == "LEFT") ? leftHandObject : rightHandObject;
        if (activeObject != null)
        {
            Debug.LogWarning($"[物理原语] GRAB 失败：【{sanitizedHand}手】已经持有物体 {activeObject.name}");
            yield return new WaitForSeconds(0.1f);
            yield break;
        }

        // 优先精确查找
        GameObject target = GameObject.Find(targetId);

        // 如果找不到，尝试模糊查找
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

        if (target != null && Vector3.Distance(transform.position, target.transform.position) <= 3.5f)
        {
            // 🌟 检查目标是否已经被另一只手抓住了，防止“双手互抢一个物体”导致物理冲突
            if (target == leftHandObject || target == rightHandObject)
            {
                Debug.LogWarning($"[物理原语] GRAB 失败：目标 {targetId} 已被另一只手抓取。");
                yield break;
            }

            // 赋值给对应的手
            if (sanitizedHand == "LEFT") leftHandObject = target;
            else rightHandObject = target;

            rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(0.1f);

            var targetRb = target.GetComponent<Rigidbody>();
            if (targetRb != null) targetRb.isKinematic = true;

            target.transform.SetParent(transform);

            // 🌟【双手视觉挂载微调】：根据左/右手稍微错开左右位置（X轴），避免两只手的物体穿模重叠在正中间
            float xOffset = (sanitizedHand == "LEFT") ? -0.4f : 0.4f;
            target.transform.localPosition = new Vector3(xOffset, 0.8f, 1.0f);
            target.transform.localRotation = Quaternion.identity;

            Debug.Log($"<color=cyan>[物理原语] 成功用【{sanitizedHand}手】抓取物体: {target.name}</color>");
        }
        else
        {
            Debug.LogWarning($"[物理原语] GRAB 失败：无法找到目标 {targetId} 或距离太远");
        }

        yield return new WaitForSeconds(0.25f);
    }
}