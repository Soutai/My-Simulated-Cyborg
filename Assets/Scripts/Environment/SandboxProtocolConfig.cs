using System.Collections.Generic;

// 1. 定义一个强类型的物体语义枚举
public enum SemanticType
{
    Food,
    Weapon,
    Enemy
}

public static class SandboxProtocolConfig
{
    // 2. 将 Registry 的 Key 改为枚举类型
    private static readonly Dictionary<SemanticType, string> EnvironmentSemanticRegistry = new Dictionary<SemanticType, string>()
    {
        {
            SemanticType.Food,
            "这是一个可食用的静态有机刚体。如果你的坐标与它重合（距离小于0.5米）并执行 USE_ITEM，它将被你的身体消化，为你恢复15点饱食度。"
        },
        {
            SemanticType.Weapon,
            "这是一根质地坚硬的长条刚体，可以被抓取。如果你靠近它并执行 GRAB 成功将其握在手中，它的坐标将跟随你。拿着它时执行 USE_ITEM 会向前产生半径2米的横扫物理撞击，可击退并伤害恶狼。"
        },
        {
            SemanticType.Enemy,
            "这是一只具有高度敌意、处于游荡状态的动态生物。它会持续追踪并撕咬靠近的无武器目标。它害怕高强度的物理撞击。"
        }
    };

    /// <summary>
    /// 支持直接通过强类型枚举查询
    /// </summary>
    public static string GetMechanismDescription(SemanticType type)
    {
        if (EnvironmentSemanticRegistry.TryGetValue(type, out string desc))
        {
            return desc;
        }
        return "未知物理实体，未定义其运作机制。";
    }
}