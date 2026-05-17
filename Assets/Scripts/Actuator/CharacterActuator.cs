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
    private GameObject grabbedObject = null;
    private bool isExecuting = false;

    public GameObject CurrentGrabbedObject => grabbedObject;

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
        StopAllPhysicalMovement();
        StartCoroutine(SequenceRoutine(commands, actionDisplay));
    }

    private IEnumerator SequenceRoutine(List<PrimitiveCommand> commands, UnityEngine.UI.Text actionDisplay)
    {
        isExecuting = true;

        foreach (var cmd in commands)
        {
            if (!isExecuting) yield break;

            if (actionDisplay) actionDisplay.text = $"正在执行: {cmd.op}";

            switch (cmd.op.ToUpper())
            {
                case "APPLY_FORCE":
                    yield return StartCoroutine(ApplyForceSafe(cmd.arg_x, cmd.arg_z));
                    break;

                case "GRAB":
                    if (grabbedObject == null)
                    {
                        // 抓取前先刹车，防止高速抓取导致飞天
                        rb.linearVelocity = Vector3.zero;
                        yield return new WaitForSeconds(0.1f);

                        GameObject target = GameObject.Find(cmd.target_id);
                        if (target != null && Vector3.Distance(transform.position, target.transform.position) <= 2.5f)
                        {
                            grabbedObject = target;
                            var targetRb = grabbedObject.GetComponent<Rigidbody>();
                            if (targetRb) targetRb.isKinematic = true;

                            grabbedObject.transform.SetParent(this.transform);
                            grabbedObject.transform.localPosition = new Vector3(0, 0.6f, 0.9f);
                            Debug.Log($"<color=cyan>[物理原语] 成功抓取物体: {cmd.target_id}</color>");
                        }
                    }
                    yield return new WaitForSeconds(0.2f);
                    break;

                case "RELEASE":
                    if (grabbedObject != null)
                    {
                        var targetRb = grabbedObject.GetComponent<Rigidbody>();
                        if (targetRb) targetRb.isKinematic = false;
                        grabbedObject.transform.SetParent(null);
                        Debug.Log($"<color=cyan>[物理原语] 松开了物体: {grabbedObject.name}</color>");
                        grabbedObject = null;
                    }
                    yield return new WaitForSeconds(0.2f);
                    break;

                case "USE_ITEM":
                    TriggerUseLogic();
                    yield return new WaitForSeconds(0.4f);
                    break;
            }

            // 每个指令后轻微稳定
            StabilizeMovement();
        }

        if (actionDisplay) actionDisplay.text = "原子序列执行完毕";
        isExecuting = false;
    }

    // ==================== 核心修复：安全的推力实现 ====================
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

    private void TriggerUseLogic()
    {
        if (grabbedObject != null && grabbedObject.name.Contains("Stick"))
        {
            Debug.Log("<color=yellow>[物理交互] 原始人挥舞了木棍！</color>");
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
                NPCAttributes attr = GetComponent<NPCAttributes>();
                if (attr) attr.satiety = Mathf.Clamp(attr.satiety + 15f, 0f, 100f);
                Debug.Log("<color=green>[物理交互] 🍎 吃了果子！</color>");
                Destroy(col.gameObject);
                break;
            }
        }
    }
}