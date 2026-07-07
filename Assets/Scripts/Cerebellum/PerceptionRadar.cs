using UnityEngine;
using System.Text;
using System.Collections.Generic;

public class PerceptionRadar : MonoBehaviour
{
    // 🌟 视觉的最大有效距离（发给大脑的感知 JSON、'战略惊醒锚点'扫描都用这个）。
    // 听觉（HearingReflex.hearingRadius）和本能危险感知（InstinctReflex.dangerSenseRadius）
    // 各自有独立半径，不再跟这个共用——三套感官理应各自可调。
    public float perceptionRadius = 15f;

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

    /// <summary>
    /// 扫描出此刻视野内（感知半径 + 视觉扇形角度都满足）的语义物体列表。
    /// 抽出来独立成公开方法，是因为不止 Prompt 需要这份"当前看到了什么"——
    /// 屏幕上的感知调试面板（PerceptionHudDisplay）也需要同一份数据，不应该各自重新扫一遍。
    /// </summary>
    public List<SemanticObject> GetVisibleObjects()
    {
        var visible = new List<SemanticObject>();
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, perceptionRadius, scanBuffer);

        for (int i = 0; i < hitCount; i++)
        {
            var col = scanBuffer[i];
            if (col.gameObject == this.gameObject) continue;

            // 🌟【双手清洗屏障】：如果这个物体正被自己的左手或右手抓着，它已属于自我躯壳的延伸，直接跳过，防止产生认知死循环！
            if (actuator != null && (col.gameObject == actuator.LeftHandObject || col.gameObject == actuator.RightHandObject))
                continue;

            // 🌟 视觉扇形：只有落在正面视野角度内的物体才算"看见"
            if (actuator != null && !IsWithinVisionCone(col.transform.position))
                continue;

            SemanticObject semanticObj = col.GetComponent<SemanticObject>();
            if (semanticObj != null) visible.Add(semanticObj);
        }

        return visible;
    }

    public string ScanEnvironmentToSemanticJson()
    {
        List<SemanticObject> visible = GetVisibleObjects();
        StringBuilder sb = new StringBuilder();
        sb.Append("[\n");

        for (int i = 0; i < visible.Count; i++)
        {
            SemanticObject semanticObj = visible[i];

            if (i > 0) sb.Append(",\n");

            // 计算三维空间相对坐标偏移 (以原始人为坐标原点 0,0,0)
            Vector3 relativePos = semanticObj.transform.position - transform.position;

            // 枚举名字本身就是 Food/Weapon/Enemy，直接转字符串，不需要手动映射
            string typeString = semanticObj.semanticType.ToString();

            // 动态获取绝对配置中心对该物体的机制语义化解释
            string dynamicMechanismRules = semanticObj.MechanismDescription;

            sb.Append("  {\n");
            sb.Append($"    \"object_id\": \"{semanticObj.gameObject.name}\",\n");
            sb.Append($"    \"type\": \"{typeString}\",\n");
            sb.Append("    \"relative_position_offset\": {\n");
            sb.Append($"      \"relative_x\": {relativePos.x:F2},\n");
            sb.Append($"      \"relative_z\": {relativePos.z:F2}\n");
            sb.Append("    },\n");
            sb.Append($"    \"distance\": {relativePos.magnitude:F2},\n");
            sb.Append($"    \"mechanism_rules\": \"{dynamicMechanismRules}\"\n"); // 注入机制语义描述
            sb.Append("  }");
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

    /// <summary>
    /// 判断某个世界坐标是否"此刻真的能看见"——距离在 perceptionRadius 内，且在视觉扇形角度内。
    /// 供记忆系统（SpatialMemoryStore）判断"这条记忆现在还看得见吗，要不要跳过不重复汇报"。
    /// </summary>
    public bool IsCurrentlyVisible(Vector3 worldPosition)
    {
        float distance = Vector3.Distance(transform.position, worldPosition);
        return distance <= perceptionRadius && IsWithinVisionCone(worldPosition);
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        // 视觉扇形：用当前朝向（没有朝向时退化为 transform.forward）画一个能直观看到的扇形。
        // perceptionRadius 本身就是这个扇形的半径，不用再额外画一个独立的参考圆圈。
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