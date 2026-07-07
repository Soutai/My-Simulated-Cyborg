using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 具身智能物理沙盒 - 空间记忆系统（记忆系统的第一块地基）。
///
/// 只做一件事：持续（比大脑思考频率快得多）巡视视觉范围，把看到的物体的绝对坐标记下来。
/// 汇报给大脑时，用 NPC 当前的实际位置重新计算相对偏移——物体没动的话（食物/武器），
/// 这份"记忆"精确等价于"现在正好看到"；物体会动的话（敌人），会在文字里标注记忆的新旧程度，
/// 让大脑自己判断这份信息还值不值得信。
///
/// 🌟 之所以要自己独立扫描、不能直接复用 PerceptionRadar.ScanEnvironmentToSemanticJson()：
/// 那个方法只在大脑每次思考时才被调用一次（间隔通常是 TimeManager.aiThinkInterval，默认20秒），
/// 而 NPC 在两次思考之间会持续移动、朝向持续变化——移动途中一闪而过、恰好没被"思考那一瞬间的
/// 朝向"捕捉到的物体，靠那个方法永远补不回来。记忆系统必须有自己独立的高频采样节奏。
///
/// 🌟 这是记忆系统的第一块积木，专门设计成可以往上加：
/// - 现在只记"物体在哪"，以后要扩展"威胁记忆"（记住被谁从哪个方向攻击过）、
///   "经验记忆"（记住某个行动的后果）都可以复用 MemoryRecord 这个最小结构。
/// - 现在的遗忘策略是纯粹的"超时"，以后如果需要更精细的置信度衰减模型，
///   只需要改 MemoryRecord.AgeSeconds 的消费方式，不需要动采集逻辑。
/// </summary>
[RequireComponent(typeof(PerceptionRadar))]
[RequireComponent(typeof(CharacterActuator))]
public class SpatialMemoryStore : MonoBehaviour
{
    private PerceptionRadar radar;
    private CharacterActuator actuator;

    private readonly Dictionary<string, MemoryRecord> records = new Dictionary<string, MemoryRecord>();
    private float scanTimer = 0f;

    /// <summary>只读地暴露当前所有记忆条目，供屏幕调试面板（PerceptionHudDisplay）等外部只读消费者使用</summary>
    public IReadOnlyCollection<MemoryRecord> AllRecords => records.Values;

    void Awake()
    {
        radar = GetComponent<PerceptionRadar>();
        actuator = GetComponent<CharacterActuator>();
    }

    void FixedUpdate()
    {
        scanTimer += Time.fixedDeltaTime;
        if (scanTimer < MemoryProtocolConfig.MemoryScanInterval) return;
        scanTimer = 0f;

        ScanAndRemember();
        PruneExpired();
    }

    private void ScanAndRemember()
    {
        // 🌟 复用 PerceptionRadar 已经写好的"当前视野内看得见什么"扫描，不重新写一遍 OverlapSphere+
        // 视觉扇形过滤——记忆系统的"看见"跟 Prompt 感知的"看见"理应是完全同一套标准。
        List<SemanticObject> visible = radar.GetVisibleObjects();
        for (int i = 0; i < visible.Count; i++)
        {
            Remember(visible[i]);
        }
    }

    private void Remember(SemanticObject semanticObj)
    {
        string id = semanticObj.gameObject.name;

        if (!records.TryGetValue(id, out var record))
        {
            record = new MemoryRecord { EntityId = id, SemanticType = semanticObj.semanticType };
            records[id] = record;
        }

        // 重新看到就刷新坐标和时间戳——同一个身份，覆盖旧记忆，不需要额外的"更新"接口
        record.LastKnownPosition = semanticObj.transform.position;
        record.LastSeenTimeStamp = Time.time;
    }

    private void PruneExpired()
    {
        List<string> expired = null;
        foreach (var pair in records)
        {
            if (pair.Value.AgeSeconds > MemoryProtocolConfig.MemoryRetentionSeconds)
            {
                expired ??= new List<string>();
                expired.Add(pair.Key);
            }
        }

        if (expired == null) return;
        foreach (var id in expired) records.Remove(id);
    }

    /// <summary>
    /// 生成给大脑看的"近期记忆"JSON。只汇报当前看不见的记忆条目——当前视野内看得见的
    /// 已经在"当前感知"里报过了，这里不重复列出。相对位置用 NPC 当前实际坐标重新计算，
    /// 对静态物体（食物/武器）而言是精确值；每条记忆都带上"多少秒前看到的"，
    /// 越旧的记忆（尤其是会动的敌人）越应该被大脑当作"不一定还在原地"的参考信息，而非事实。
    /// </summary>
    public string GenerateMemoryJson()
    {
        var sb = new StringBuilder();
        sb.Append("[\n");
        bool isFirst = true;

        string heldLeft = actuator != null && actuator.LeftHandObject != null ? actuator.LeftHandObject.name : null;
        string heldRight = actuator != null && actuator.RightHandObject != null ? actuator.RightHandObject.name : null;

        foreach (var record in records.Values)
        {
            // 已经被抓在手上的东西，不该再作为"记忆中的位置"汇报（位置信息已经失去意义）
            if (record.EntityId == heldLeft || record.EntityId == heldRight) continue;

            // 当前视野内还看得见的，已经在"当前感知"里报过了，记忆列表里不重复列出
            if (radar.IsCurrentlyVisible(record.LastKnownPosition)) continue;

            if (!isFirst) sb.Append(",\n");
            isFirst = false;

            Vector3 relativePos = record.LastKnownPosition - transform.position;
            float ageSeconds = record.AgeSeconds;
            string staleness = ageSeconds > MemoryProtocolConfig.StaleWarningThresholdSeconds
                ? (record.SemanticType == SemanticType.Enemy
                    ? "时间较久，该物体很可能已经移动，此坐标仅供参考"
                    : "时间较久，若物体本身不会移动，此坐标大概率仍然准确")
                : "较新鲜的记忆，可信度较高";

            sb.Append("  {\n");
            sb.Append($"    \"object_id\": \"{record.EntityId}\",\n");
            sb.Append($"    \"type\": \"{record.SemanticType}\",\n");
            sb.Append("    \"relative_position_offset\": {\n");
            sb.Append($"      \"relative_x\": {relativePos.x:F2},\n");
            sb.Append($"      \"relative_z\": {relativePos.z:F2}\n");
            sb.Append("    },\n");
            sb.Append($"    \"distance\": {relativePos.magnitude:F2},\n");
            sb.Append($"    \"seconds_since_last_seen\": {ageSeconds:F0},\n");
            sb.Append($"    \"confidence_note\": \"{staleness}\"\n");
            sb.Append("  }");
        }

        sb.Append("\n]");
        return sb.ToString();
    }
}
