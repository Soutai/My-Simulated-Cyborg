using UnityEngine;
using System.Text;

public class PerceptionRadar : MonoBehaviour
{
    public float perceptionRadius = 15f; // 放大感知距离，给物理拉扯留出足够的空间

    // 🌟【新增】：引入物理执行器组件引用，用作自我状态的清洗过滤
    private CharacterActuator actuator;

    void Awake()
    {
        actuator = GetComponent<CharacterActuator>();
    }

    public string ScanEnvironmentToSemanticJson()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, perceptionRadius);
        StringBuilder sb = new StringBuilder();
        sb.Append("[\n");

        bool isFirst = true;
        foreach (var col in hitColliders)
        {
            if (col.gameObject == this.gameObject) continue;

            // 🌟【核心修复】：如果这个物体正被自己抓在手里，它已属于自我躯壳的一部分，不再作为“外界环境”干扰大脑认知
            if (actuator != null && col.gameObject == actuator.CurrentGrabbedObject)
                continue;

            // 获取挂载在物体身上的语义化组件
            SemanticObject semanticObj = col.GetComponent<SemanticObject>();
            if (semanticObj != null)
            {
                if (!isFirst) sb.Append(",\n");
                isFirst = false;

                // 计算三维空间相对坐标偏移 (以原始人为坐标原点 0,0,0)
                Vector3 relativePos = col.transform.position - transform.position;

                // 直接向物体伸手要它自身的机制描述
                string dynamicMechanismRules = semanticObj.MechanismDescription;

                // 将强类型枚举转换为大模型需要的字符串（如 "Food"、"Weapon"）
                string typeString = semanticObj.semanticType.ToString();

                // 使用正确的 \" 来在 C# 字符串中转义生成 JSON 的双引号
                sb.Append("  {\n");
                sb.Append($"    \"unique_id\": \"{col.gameObject.name}\",\n");
                sb.Append($"    \"type\": \"{typeString}\",\n");
                sb.Append("    \"relative_position_offset\": {\n");
                sb.Append($"      \"relative_x\": {relativePos.x:F2},\n");
                sb.Append($"      \"relative_z\": {relativePos.z:F2}\n");
                sb.Append("    },\n");
                sb.Append($"    \"distance\": {relativePos.magnitude:F2},\n");
                sb.Append($"    \"mechanism_rules\": \"{dynamicMechanismRules}\"\n"); // 注入机制语义描述
                sb.Append("  }");
            }
        }
        sb.Append("\n]");
        return sb.ToString();
    }
}