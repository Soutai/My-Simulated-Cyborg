// 建议放进 📁 Environment 文件夹
using System.Collections.Generic;

/// <summary>
/// 具身智能物理沙盒 - 物理实体耐受度与临界值绝对配置中心
/// </summary>
public static class PhysicsProtocolConfig
{
    // 定义每个语义物体的底层物理抗性数据结构
    public struct PhysicsResistance
    {
        public float maxTolerance;          // 物理耐受度 (生命值/耐久度)
        public float impactThreshold;       // 瞬时速度或碰撞突变受伤阈值
        public float damageMultiplier;      // 物理冲击放大系数
    }

    // 🌟 核心注册表：所有物体的物理抗性在这里集中配置，严禁散落到 Inspector 面板
    private static readonly Dictionary<SemanticType, PhysicsResistance> ResistanceRegistry =
        new Dictionary<SemanticType, PhysicsResistance>()
        {
            {
                SemanticType.Enemy, // 狼：躯体较脆弱，容易被木棍高速击退轰飞触发消亡
                new PhysicsResistance { maxTolerance = 60f, impactThreshold = 12f, damageMultiplier = 2.5f }
            },
            {
                SemanticType.Food,  // 水果：极为脆弱，如果发生剧烈碰撞或被踩踏可能会坏掉
                new PhysicsResistance { maxTolerance = 10f, impactThreshold = 5f, damageMultiplier = 1.0f }
            },
            {
                SemanticType.Weapon, // 木棍：作为坚硬的武器，基本不会因为物理碰撞而损坏
                new PhysicsResistance { maxTolerance = 9999f, impactThreshold = 100f, damageMultiplier = 0f }
            }
        };

    /// <summary>
    /// 根据语义类型获取其底层的物理抗性配置
    /// </summary>
    public static PhysicsResistance GetResistance(SemanticType type)
    {
        if (ResistanceRegistry.TryGetValue(type, out PhysicsResistance resistance))
        {
            return resistance;
        }
        // 默认保底配置，防止未注册类型报错
        return new PhysicsResistance { maxTolerance = 100f, impactThreshold = 10f, damageMultiplier = 2.0f };
    }
}