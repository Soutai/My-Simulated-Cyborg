using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum SemanticType
{
    Food,
    Weapon,
    Enemy
}

// ==================== 物体物理交互配置 ====================
[System.Serializable]
public class PhysicalInteractionConfig
{
    public SemanticType type;

    [Tooltip("APPROACH 时默认停止距离（米）")]
    public float desiredApproachDistance = 0.65f;

    [Tooltip("GRAB 允许的最大抓取距离")]
    public float maxGraspDistance = 1.25f;

    [Tooltip("给AI的描述（可选）")]
    public string descriptionForAI = "";
}

public static class SandboxProtocolConfig
{
    // ==================== 物理交互配置表（核心） ====================
    private static readonly List<PhysicalInteractionConfig> InteractionConfigs = new List<PhysicalInteractionConfig>()
    {
        new PhysicalInteractionConfig
        {
            type = SemanticType.Food,
            desiredApproachDistance = 0.85f,
            maxGraspDistance = 1.25f,
            descriptionForAI = "这是一个小型食物，建议靠近到0.85米以内再抓取。"
        },
        new PhysicalInteractionConfig
        {
            type = SemanticType.Weapon,
            desiredApproachDistance = 0.60f,
            maxGraspDistance = 1.1f,
            descriptionForAI = "这是一根长条武器，建议靠近到0.6米以内抓取。"
        },
        new PhysicalInteractionConfig
        {
            type = SemanticType.Enemy,
            desiredApproachDistance = 1.8f,
            maxGraspDistance = 2.5f,
            descriptionForAI = "敌人危险，建议保持1.8米以上距离进行攻击。"
        }
    };

    // ==================== 公共查询方法 ====================
    /// <summary>
    /// 🌟 机制说明文字里凡是涉及具体数值的地方，都从 PhysicsProtocolConfig 实时读取，
    /// 不再手写死数字——避免以后调整数值平衡时，说明文字和真实配置各说各话。
    /// </summary>
    public static string GetMechanismDescription(SemanticType type)
    {
        switch (type)
        {
            case SemanticType.Food:
            {
                var effect = PhysicsProtocolConfig.GetUseEffect(SemanticType.Food);
                return $"这是一个可食用的静态有机刚体。必须先靠近并执行 GRAB 将其抓到手中，再执行 USE_ITEM 才能吃掉它，为你恢复{effect.satietyRestore:F0}点饱食度。";
            }
            case SemanticType.Weapon:
            {
                var effect = PhysicsProtocolConfig.GetUseEffect(SemanticType.Weapon);
                return $"这是一根质地坚硬的长条刚体，可以被抓取。如果你靠近它并执行 GRAB 成功将其握在手中，它的坐标将跟随你。拿着它时执行 USE_ITEM 会向前产生半径{effect.effectRadius:F0}米的横扫物理撞击，可击退并伤害{effect.affectedTag}。";
            }
            case SemanticType.Enemy:
                return "这是一只具有高度敌意、处于游荡状态的动态生物。它会持续追踪并撕咬靠近的无武器目标。它害怕高强度的物理撞击。";
            default:
                return "未知物理实体，未定义其运作机制。";
        }
    }

    public static PhysicalInteractionConfig GetInteractionConfig(SemanticType type)
    {
        return InteractionConfigs.FirstOrDefault(c => c.type == type)
               ?? new PhysicalInteractionConfig { type = type, desiredApproachDistance = 0.65f, maxGraspDistance = 1.25f };
    }
}
