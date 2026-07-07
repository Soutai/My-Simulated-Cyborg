/// <summary>
/// 具身智能物理沙盒 - 本能反射系统调参配置中心。
/// 跟 PhysicsProtocolConfig / SandboxProtocolConfig / PersonalityProtocolConfig 同一套模式：
/// 所有硬编码数值集中在这里，InstinctReflex 只管调用，不再把一堆散落的 public 字段
/// 直接摆在组件本身上——这样本能系统全部的调参入口能一眼看全，也跟其余配置中心观感统一。
/// </summary>
public static class InstinctProtocolConfig
{
    // ==================== 危险感知 ====================

    /// <summary>本能反射自己独立的危险扫描半径，不跟视觉/听觉共用——贴身覆写不受视野限制，
    /// 背后贴脸也感觉得到，所以要留出比视觉更大一点的安全网半径</summary>
    public const float DangerSenseRadius = 18f;

    /// <summary>危险变化率超过此值才会触发逃跑反射</summary>
    public const float DangerThreshold = 2.5f;

    /// <summary>危险度降到此值以下视为已脱离危险，停止逃跑</summary>
    public const float SafeDangerDensity = 0.2f;

    /// <summary>敌人进入此距离内，无论它自己是否在动都视为贴身威胁</summary>
    public const float MeleeDangerRange = 2f;

    /// <summary>贴身威胁的固定危险值，需明显高于 DangerThreshold 才能可靠触发</summary>
    public const float MeleeDangerValue = 5f;

    /// <summary>已经在逃跑状态时，退出贴身覆写所需的距离倍数（滞后缓冲）——贴身对峙时两个刚体
    /// 互相挤压，实际距离会在 MeleeDangerRange 边界内外来回抖动几厘米，如果进入/退出用同一个
    /// 边界，危险度会在 MeleeDangerValue 和接近 0 之间逐帧反复横跳。已经在逃跑状态时要求敌人
    /// 明显跑得更远才算真正脱离，就不会被几厘米的抖动打断</summary>
    public const float MeleeHysteresisMultiplier = 1.5f;

    /// <summary>正主动朝我靠近的敌人（还没贴脸），视为"接近威胁"的固定危险值——平方反比公式在
    /// 稍远距离算出来的值太小，不符合"感觉到掠食者冲过来就该跑"的直觉，所以单独给一档。
    ///
    /// ⚠️ 临时方案（2026-07-07 决定）：这一档故意是全向判定，不要求"看得见"（不检查视觉扇形）。
    /// 曾经尝试要求必须在视觉扇形内才算数，结果出现死循环：本能逃跑的方向就是背对威胁的方向，
    /// 而 facingDirection 又是跟着移动方向走的，"开始逃跑"这个动作本身就会让敌人掉出视野，
    /// 导致危险判定在"看得见/看不见"之间逐帧反复横跳，表现为动作疯狂抽搐。改回全向判定后
    /// 问题消失，但代价是"没看见也能反应"，跟贴身覆写一样不够严谨。以后要重新引入方向性判断，
    /// 需要先给朝向做一个独立于移动方向的状态，否则同样的死循环还会重演。</summary>
    public const float ApproachingThreatDangerValue = 3f;

    /// <summary>平方反比危险公式里，敌人的危险权重</summary>
    public const float EnemyDangerOmega = 2.5f;

    /// <summary>平方反比危险公式里，武器（比如被丢出去的木棍）的危险权重</summary>
    public const float WeaponDangerOmega = 0.1f;

    // ==================== 逃跑反射 ====================

    /// <summary>必须明显高于 EnemyController.chaseForce，否则每次被逼到墙角重新起步都会被狼追上</summary>
    public const float FleeForce = 16f;

    /// <summary>必须明显高于 EnemyController.maxChaseSpeed，只要有开阔直线距离就能持续拉开差距</summary>
    public const float MaxFleeSpeed = 7f;

    // ==================== 障碍物规避 ====================

    /// <summary>往逃跑方向探测多远，撞到任何障碍物（墙/水果/木棍等）就混入"远离它"的方向</summary>
    public const float WallCheckDistance = 2f;

    // ==================== 排障诊断 ====================

    /// <summary>逃跑期间每隔多久检查一次位移，用来排查"持续逃跑但被夹死原地不动"这类问题</summary>
    public const float DiagnosticLogInterval = 0.5f;

    // ==================== 卡死急救 ====================

    /// <summary>逃跑时每隔 DiagnosticLogInterval 检查一次位移，低于此值视为被卡死</summary>
    public const float StuckDisplacementThreshold = 0.15f;

    /// <summary>判定卡死时给出的瞬间冲量强度，需明显大于 FleeForce 才能破开物理死锁</summary>
    public const float UnstickImpulseForce = 25f;

    // ==================== 赤手空拳反击 ====================

    /// <summary>被逼到贴身、双手空空、又被判定卡死逃不掉时，本能会朝威胁方向反击一拳
    /// （物理击退，跟武器横扫同一套机制但力度弱得多）。两次反击之间的最短间隔</summary>
    public const float PunchCooldown = 1f;
}
