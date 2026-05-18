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

            // 🌟【双手清洗屏障】：如果这个物体正被自己的左手或右手抓着，它已属于自我躯壳的延伸，直接跳过，防止产生认知死循环！
            if (actuator != null && (col.gameObject == actuator.LeftHandObject || col.gameObject == actuator.RightHandObject))
                continue;

            // 获取挂载在物体身上的语义化组件
            SemanticObject semanticObj = col.GetComponent<SemanticObject>();
            if (semanticObj != null)
            {
                if (!isFirst) sb.Append(",\n");
                isFirst = false;

                // 计算三维空间相对坐标偏移 (以原始人为坐标原点 0,0,0)
                Vector3 relativePos = col.transform.position - transform.position;

                // 统一映射转换名字
                string typeString = "Unknown";
                if (semanticObj.semanticType == SemanticType.Food) typeString = "Food";
                else if (semanticObj.semanticType == SemanticType.Weapon) typeString = "Weapon";
                else if (semanticObj.semanticType == SemanticType.Enemy) typeString = "Enemy";

                // 动态获取绝对配置中心对该物体的机制语义化解释
                string dynamicMechanismRules = semanticObj.MechanismDescription;

                sb.Append("  {\n");
                sb.Append($"    \"object_id\": \"{col.gameObject.name}\",\n");
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

    // 🌟 将这段代码粘贴到 PerceptionRadar.cs 的最底部 🌟
    private void OnDrawGizmos()
    {
        // 设置 Gizmos 颜色为半透明黄色
        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.15f);

        // 1. 画出一个基础的立体线框球体（表现感知范围）
        Gizmos.DrawWireSphere(transform.position, perceptionRadius);

        // 2. 🌟 还原你记忆中的“三个坐标轴黄圈”的硬核层次感
#if UNITY_EDITOR
        // 稍微加深线框颜色使其更清晰
        UnityEditor.Handles.color = new Color(1f, 0.92f, 0.016f, 0.4f);

        // 绘制水平面 (X-Z) 的雷达扫描圈
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, perceptionRadius);

        // 绘制纵切面面 (X-Y) 的垂直参考圈
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, perceptionRadius);

        // 绘制纵切面面 (Y-Z) 的侧视参考圈
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.right, perceptionRadius);
#endif
    }
}