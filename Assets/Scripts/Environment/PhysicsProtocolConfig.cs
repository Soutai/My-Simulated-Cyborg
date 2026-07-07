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
        public float maxTolerance;              // 物理耐受度 (生命值/耐久度)
        public float impactThreshold;           // 瞬时速度或碰撞突变受伤阈值
        public float damageMultiplier;          // 物理冲击放大系数
        public float collisionDamageMultiplier; // 硬碰撞相对于单纯超速的额外伤害放大系数
    }

    // USE_ITEM 触发的效果种类
    public enum UseEffectKind
    {
        None,
        SweepAttack,   // 挥舞横扫，对指定 Tag 的物体施加击退冲量
        Consume        // 消耗自身，为使用者恢复饱食度
    }

    // USE_ITEM 效果的具体物理参数
    public struct ItemUseEffect
    {
        public UseEffectKind kind;

        // ---- SweepAttack 专用 ----
        public float effectRadius;      // 判定球半径
        public float forwardOffset;     // 判定球中心相对身体的前移距离
        public float knockbackForce;    // 击退冲量
        public string affectedTag;      // 生效的目标 Tag

        // ---- Consume 专用 ----
        public float satietyRestore;    // 恢复的饱食度数值
    }

    // 🌟 核心注册表：所有物体的物理抗性在这里集中配置，严禁散落到 Inspector 面板
    private static readonly Dictionary<SemanticType, PhysicsResistance> ResistanceRegistry =
        new Dictionary<SemanticType, PhysicsResistance>()
        {
            {
                SemanticType.Enemy, // 狼：躯体较脆弱，容易被木棍高速击退轰飞触发消亡
                new PhysicsResistance { maxTolerance = 60f, impactThreshold = 12f, damageMultiplier = 2.5f, collisionDamageMultiplier = 1.5f }
            },
            {
                SemanticType.Food,  // 水果：极为脆弱，如果发生剧烈碰撞或被踩踏可能会坏掉
                new PhysicsResistance { maxTolerance = 10f, impactThreshold = 5f, damageMultiplier = 1.0f, collisionDamageMultiplier = 1.5f }
            },
            {
                SemanticType.Weapon, // 木棍：作为坚硬的武器，基本不会因为物理碰撞而损坏
                new PhysicsResistance { maxTolerance = 9999f, impactThreshold = 100f, damageMultiplier = 0f, collisionDamageMultiplier = 1.5f }
            }
        };

    // 🌟 核心注册表：USE_ITEM 会产生什么物理效果也集中配置，执行器只管"问物体自己会发生什么"，不写死 if-else
    private static readonly Dictionary<SemanticType, ItemUseEffect> UseEffectRegistry =
        new Dictionary<SemanticType, ItemUseEffect>()
        {
            {
                SemanticType.Weapon, // 木棍：向前横扫，击退恶狼
                new ItemUseEffect
                {
                    kind = UseEffectKind.SweepAttack,
                    effectRadius = 2f,
                    forwardOffset = 1f,
                    knockbackForce = 30f,
                    affectedTag = "Enemy"
                }
            },
            {
                SemanticType.Food, // 水果：被吃掉，恢复饱食度
                new ItemUseEffect
                {
                    kind = UseEffectKind.Consume,
                    satietyRestore = 15f
                }
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
        return new PhysicsResistance { maxTolerance = 100f, impactThreshold = 10f, damageMultiplier = 2.0f, collisionDamageMultiplier = 1.5f };
    }

    /// <summary>
    /// 根据语义类型获取其被 USE_ITEM 时应触发的效果配置
    /// </summary>
    public static ItemUseEffect GetUseEffect(SemanticType type)
    {
        return UseEffectRegistry.TryGetValue(type, out ItemUseEffect effect)
            ? effect
            : new ItemUseEffect { kind = UseEffectKind.None };
    }

    // 🌟 赤手空拳的反击效果：本能反射专用，不需要持有任何物品。跟木棍横扫用的是同一套
    // SweepAttack 机制（纯物理击退，不直接扣血），只是力度明显弱于武器——
    // 徒手推一把总比拿棍子抡一下弱，这样"有没有武器"依然有意义。
    public static readonly ItemUseEffect UnarmedPunchEffect = new ItemUseEffect
    {
        kind = UseEffectKind.SweepAttack,
        effectRadius = 1.5f,
        forwardOffset = 0.6f,
        knockbackForce = 12f,
        affectedTag = "Enemy"
    };
}
