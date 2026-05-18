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
    Foraging,           // 觅食 / 补充饱食度
    Exploration,        // 探索未知区域
    Patrolling,         // 警戒 / 巡逻
    ThreatElimination   // 消除威胁
}

[System.Serializable]
public class IntentStrategy
{
    [Tooltip("AI 可能使用的关键词，用 | 分隔")]
    public string keywords;

    public PersistentIntent intentType;

    [TextArea(2, 4)]
    public string descriptionToAI;

    [Tooltip("小脑执行策略提示")]
    public string executionHint;
}

public static class SandboxProtocolConfig
{
    // ==================== 原有物体机制（保持不变） ====================
    private static readonly Dictionary<SemanticType, string> EnvironmentSemanticRegistry = new Dictionary<SemanticType, string>()
    {
        { SemanticType.Food,   "这是一个可食用的静态有机刚体。如果你的坐标与它重合（距离小于0.5米）并执行 USE_ITEM，它将被你的身体消化，为你恢复15点饱食度。" },
        { SemanticType.Weapon, "这是一根质地坚硬的长条刚体，可以被抓取。如果你靠近它并执行 GRAB 成功将其握在手中，它的坐标将跟随你。拿着它时执行 USE_ITEM 会向前产生半径2米的横扫物理撞击，可击退并伤害恶狼。" },
        { SemanticType.Enemy,  "这是一只具有高度敌意、处于游荡状态的动态生物。它会持续追踪并撕咬靠近的无武器目标。它害怕高强度的物理撞击。" }
    };

    // ==================== 新增：持久意图映射表 ====================
    private static readonly List<IntentStrategy> IntentStrategies = new List<IntentStrategy>()
    {
        new IntentStrategy
        {
            keywords = "食物|觅食|进食|吃|饱食度|营养|果子|Fruit|补充资源",
            intentType = PersistentIntent.Foraging,
            descriptionToAI = "寻找并补充饱食度的资源",
            executionHint = "foraging_search"
        },
        new IntentStrategy
        {
            keywords = "探索|巡逻|警戒|巡视|扩大范围|未知区域|预防|巡查",
            intentType = PersistentIntent.Exploration,
            descriptionToAI = "主动探索未知区域或进行预防性警戒",
            executionHint = "frontier_explore"
        },
        new IntentStrategy
        {
            keywords = "威胁|危险|狼|敌人|安全|清除|消灭|攻击",
            intentType = PersistentIntent.ThreatElimination,
            descriptionToAI = "优先消除当前威胁源",
            executionHint = "direct_threat"
        }
    };

    public static string GetMechanismDescription(SemanticType type)
    {
        return EnvironmentSemanticRegistry.TryGetValue(type, out string desc)
            ? desc
            : "未知物理实体，未定义其运作机制。";
    }

    public static IntentStrategy GetStrategy(string goalText)
    {
        if (string.IsNullOrEmpty(goalText)) return null;

        string lower = goalText.ToLower();
        return IntentStrategies.FirstOrDefault(s =>
            s.keywords.ToLower().Split('|').Any(k => lower.Contains(k)));
    }
}