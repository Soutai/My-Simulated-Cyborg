using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum SemanticType
{
    Food,
    Weapon,
    Enemy
}

public enum PersistentIntent
{
    None,
    Foraging,           // 觅食相关
    Exploration,        // 探索 / 巡逻 / 警戒
    ThreatElimination   // 消除威胁
}

// ==================== 新增：可扩展的行为策略 ====================
[System.Serializable]
public class BehaviorStrategy
{
    [Tooltip("AI 可能使用的关键词，用 | 分隔")]
    public string keywords;

    public PersistentIntent intentType;

    [TextArea(2, 5)]
    [Tooltip("教AI这个意图的含义和目的")]
    public string descriptionToAI;

    [TextArea(3, 6)]
    [Tooltip("教AI/小脑应该如何执行（路径策略）")]
    public string executionGuidance;
}

public static class SandboxProtocolConfig
{
    // ==================== 原有物体机制（完全保留） ====================
    private static readonly Dictionary<SemanticType, string> MechanismDescriptions = new Dictionary<SemanticType, string>()
    {
        { SemanticType.Food,   "这是一个可食用的静态有机刚体。如果你的坐标与它重合（距离小于0.5米）并执行 USE_ITEM，它将被你的身体消化，为你恢复15点饱食度。" },
        { SemanticType.Weapon, "这是一根质地坚硬的长条刚体，可以被抓取。如果你靠近它并执行 GRAB 成功将其握在手中，它的坐标将跟随你。拿着它时执行 USE_ITEM 会向前产生半径2米的横扫物理撞击，可击退并伤害恶狼。" },
        { SemanticType.Enemy,  "这是一只具有高度敌意、处于游荡状态的动态生物。它会持续追踪并撕咬靠近的无武器目标。它害怕高强度的物理撞击。" }
    };

    // ==================== 新增：行为策略字典（核心，可自由扩展） ====================
    private static readonly List<BehaviorStrategy> BehaviorStrategies = new List<BehaviorStrategy>()
    {
        new BehaviorStrategy
        {
            keywords = "食物|觅食|进食|吃|饱食度|营养|果子|Fruit",
            intentType = PersistentIntent.Foraging,
            descriptionToAI = "寻找并补充饱食度的资源",
            executionGuidance = "优先前往雷达中最近的 Food；若暂无 Food，则进行系统性探索，逐步扩大搜索范围，避免走回头路。"
        },
        new BehaviorStrategy
        {
            keywords = "探索|巡逻|警戒|巡视|扩大范围|未知区域",
            intentType = PersistentIntent.Exploration,
            descriptionToAI = "主动探索未知区域或进行预防性警戒",
            executionGuidance = "采用系统性探索策略：优先向未探索方向移动，逐步扩大覆盖范围。单次移动建议8-15米，避免频繁改变方向和走回头路。可使用轻微螺旋或Z字形前进。"
        },
        new BehaviorStrategy
        {
            keywords = "威胁|危险|狼|敌人|安全|清除|消灭",
            intentType = PersistentIntent.ThreatElimination,
            descriptionToAI = "优先消除当前威胁源",
            executionGuidance = "直接接近目标并使用武器进行高强度物理攻击。"
        }
    };

    // ==================== 公共查询方法 ====================
    public static string GetMechanismDescription(SemanticType type)
    {
        return MechanismDescriptions.TryGetValue(type, out string desc)
            ? desc
            : "未知物理实体，未定义其运作机制。";
    }

    public static BehaviorStrategy GetStrategy(string goalText)
    {
        if (string.IsNullOrEmpty(goalText)) return null;

        string lower = goalText.ToLower();
        return BehaviorStrategies.FirstOrDefault(s =>
            s.keywords.ToLower().Split('|').Any(k => lower.Contains(k)));
    }

    // 给 Prompt 使用：返回所有行为指导
    public static string GetAllBehaviorGuidance()
    {
        return string.Join("\n", BehaviorStrategies.Select(s =>
            $"• {s.intentType}: {s.descriptionToAI} 执行建议：{s.executionGuidance}"));
    }
}