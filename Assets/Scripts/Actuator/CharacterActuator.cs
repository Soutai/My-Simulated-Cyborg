using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using EmbodiedAI.DTO;

[RequireComponent(typeof(Rigidbody))]
public class CharacterActuator : MonoBehaviour
{
    public float forceMultiplier = 5f; // 冲量放大系数
    private Rigidbody rb;
    private GameObject grabbedObject = null;
    private bool isExecuting = false;
    // 🌟【新增：最小限度暴露接口】提供给雷达进行“自我身体状态”过滤的只读属性
    public GameObject CurrentGrabbedObject => grabbedObject;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // 确保刚体不会因为碰撞乱晃倒下
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public void StopAllPhysicalMovement()
    {
        StopAllCoroutines();
        rb.linearVelocity = Vector3.zero;
        isExecuting = false;
    }

    // 依次消化 AI 发来的原子物理指令队列
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

            if (actionDisplay) actionDisplay.text = $"正在执行原语: {cmd.op}";

            switch (cmd.op.ToUpper())
            {
                case "APPLY_FORCE":
                    // 物理推力原语：施加一个持续 0.5 秒的方向矢量力
                    Vector3 forceVector = new Vector3(cmd.arg_x, 0f, cmd.arg_z) * forceMultiplier;
                    float duration = 0.5f;
                    while (duration > 0f)
                    {
                        rb.AddForce(forceVector, ForceMode.Force);
                        duration -= Time.deltaTime;
                        yield return null;
                    }
                    rb.linearVelocity = Vector3.zero; // 消耗完力后平稳刹车
                    break;

                case "GRAB":
                    // 抓取原语：扫描周围指定 ID（名称）的目标，如果在物理接触范围内，将其吸附到手上
                    if (grabbedObject == null)
                    {
                        GameObject target = GameObject.Find(cmd.target_id);
                        if (target != null && Vector3.Distance(transform.position, target.transform.position) <= 2.0f)
                        {
                            grabbedObject = target;
                            // 让被抓取物取消独立物理，挂载为角色的子物体
                            if (grabbedObject.GetComponent<Rigidbody>()) grabbedObject.GetComponent<Rigidbody>().isKinematic = true;
                            grabbedObject.transform.SetParent(this.transform);
                            grabbedObject.transform.localPosition = new Vector3(0, 0.5f, 0.8f); // 举在身前
                            Debug.Log($"<color=cyan>[物理原语] 成功抓取物体: {cmd.target_id}</color>");
                        }
                    }
                    yield return new WaitForSeconds(0.2f);
                    break;

                case "RELEASE":
                    // 松开原语：丢掉手中的物体，恢复其独立物理特性
                    if (grabbedObject != null)
                    {
                        if (grabbedObject.GetComponent<Rigidbody>()) grabbedObject.GetComponent<Rigidbody>().isKinematic = false;
                        grabbedObject.transform.SetParent(null);
                        Debug.Log($"<color=cyan>[物理原语] 松开了物体: {grabbedObject.name}</color>");
                        grabbedObject = null;
                    }
                    yield return new WaitForSeconds(0.2f);
                    break;

                case "USE_ITEM":
                    // 使用道具原语：根据手里有没有东西，或者脚下踩着什么，触发世界的环境反馈
                    TriggerUseLogic();
                    yield return new WaitForSeconds(0.4f);
                    break;
            }
        }
        if (actionDisplay) actionDisplay.text = "原子序列执行完毕，等待下一个时钟";
        isExecuting = false;
    }

    private void TriggerUseLogic()
    {
        // 机制1：如果手里抓着木棍，使用它则对正前方发动横扫物理撞击
        if (grabbedObject != null && grabbedObject.name.Contains("Stick"))
        {
            Debug.Log("<color=yellow>[物理交互] 原始人挥舞了木棍！释放了半径2米的横扫判定！</color>");
            Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * 1f, 2f);
            foreach (var h in hits)
            {
                if (h.CompareTag("Enemy"))
                {
                    // 给狼一个向后的强烈排斥力
                    Rigidbody enemyRb = h.GetComponent<Rigidbody>();
                    if (enemyRb) enemyRb.AddForce((h.transform.position - transform.position).normalized * 30f, ForceMode.Impulse);
                    Debug.Log("<color=red>💥 恶狼被你的木棍狠狠击退了！</color>");
                }
            }
            return;
        }

        // 机制2：如果手里没东西，检测脚下碰到了什么（比如苹果）
        Collider[] closeObjects = Physics.OverlapSphere(transform.position, 0.8f);
        foreach (var col in closeObjects)
        {
            if (col.CompareTag("Food"))
            {
                // 触发加分/饱食度恢复
                if (GetComponent<NPCAttributes>())
                {
                    GetComponent<NPCAttributes>().satiety = Mathf.Clamp(GetComponent<NPCAttributes>().satiety + 15f, 0f, 100f);
                }
                Debug.Log("<color=green>[物理交互] 🍎 原始人把脚下的果子塞进嘴里吃了！饱食度大幅度恢复！</color>");
                Destroy(col.gameObject);
                break;
            }
        }
    }
}