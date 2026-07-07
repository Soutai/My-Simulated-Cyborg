using UnityEngine;
using System.Text;

public class PerceptionRadar : MonoBehaviour
{
    public float perceptionRadius = 15f; // 放大感知距离，给物理拉扯留出足够的空间

    [Header("视觉扇形")]
    [Tooltip("视野半角（单侧），人类双眼视野总角度大约110°-120°，默认给单侧55°")]
    public float visionHalfAngle = 55f;

    // 🌟【新增】：引入物理执行器组件引用，用作自我状态的清洗过滤
    private CharacterActuator actuator;

    // 🌟 复用缓冲区，避免每次思考调用 OverlapSphere 都产生 GC 分配
    private readonly Collider[] scanBuffer = new Collider[64];

    void Awake()
    {
        actuator = GetComponent<CharacterActuator>();
    }

    public string ScanEnvironmentToSemanticJson()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, perceptionRadius, scanBuffer);
        StringBuilder sb = new StringBuilder();
        sb.Append("[\n");

        bool isFirst = true;
        for (int i = 0; i < hitCount; i++)
        {
            var col = scanBuffer[i];
            if (col.gameObject == this.gameObject) continue;

            // 🌟【双手清洗屏障】：如果这个物体正被自己的左手或右手抓着，它已属于自我躯壳的延伸，直接跳过，防止产生认知死循环！
            if (actuator != null && (col.gameObject == actuator.LeftHandObject || col.gameObject == actuator.RightHandObject))
                continue;

            // 🌟 视觉扇形：只有落在正面视野角度内的物体才算"看见"（这里还不是最终形态——
            // 视野外、感知半径内的东西以后会作为"听觉"只给方向不给身份，目前先简单跳过不识别）
            if (actuator != null && !IsWithinVisionCone(col.transform.position))
                continue;

            // 获取挂载在物体身上的语义化组件
            SemanticObject semanticObj = col.GetComponent<SemanticObject>();
            if (semanticObj != null)
            {
                if (!isFirst) sb.Append(",\n");
                isFirst = false;

                // 计算三维空间相对坐标偏移 (以原始人为坐标原点 0,0,0)
                Vector3 relativePos = col.transform.position - transform.position;

                // 枚举名字本身就是 Food/Weapon/Enemy，直接转字符串，不需要手动映射
                string typeString = semanticObj.semanticType.ToString();

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

    /// <summary>
    /// 判断目标是否落在正面视野扇形内。朝向取 CharacterActuator 的 FacingDirection（移动方向），
    /// 目标离自己太近（几乎重合）时无法定义角度，直接判定为可见。
    /// 公开给 HearingReflex 用来判断"这个东西是不是已经看得见，不需要再转头去听"。
    /// </summary>
    public bool IsWithinVisionCone(Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return true;

        Vector3 facing = actuator.FacingDirection;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f) return true; // 还没有明确朝向（比如刚出生没动过），暂不限制

        float angle = Vector3.Angle(facing, toTarget);
        return angle <= visionHalfAngle;
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        // 1. 听觉圆圈：仅绘制水平面 (X-Z) 的感知范围
        UnityEditor.Handles.color = new Color(1f, 0.92f, 0.016f, 0.35f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, perceptionRadius);

        // 2. 视觉扇形：用当前朝向（没有朝向时退化为 transform.forward）画一个能直观看到的扇形
        Vector3 facing = Application.isPlaying && actuator != null ? actuator.FacingDirection : transform.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f) facing = transform.forward;
        facing.Normalize();

        Vector3 leftEdge = Quaternion.Euler(0f, -visionHalfAngle, 0f) * facing;

        UnityEditor.Handles.color = new Color(0f, 1f, 1f, 0.25f);
        UnityEditor.Handles.DrawSolidArc(transform.position, Vector3.up, leftEdge, visionHalfAngle * 2f, perceptionRadius);

        UnityEditor.Handles.color = new Color(0f, 1f, 1f, 0.9f);
        UnityEditor.Handles.DrawWireArc(transform.position, Vector3.up, leftEdge, visionHalfAngle * 2f, perceptionRadius);
        UnityEditor.Handles.DrawLine(transform.position, transform.position + leftEdge * perceptionRadius);
        Vector3 rightEdge = Quaternion.Euler(0f, visionHalfAngle, 0f) * facing;
        UnityEditor.Handles.DrawLine(transform.position, transform.position + rightEdge * perceptionRadius);
#endif
    }
}