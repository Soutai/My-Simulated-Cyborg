/// <summary>
/// 具身智能物理沙盒 - 本能反射系统调参配置中心。
/// 跟 PhysicsProtocolConfig / SandboxProtocolConfig / PersonalityProtocolConfig 同一套模式：
/// 所有硬编码数值集中在这里，InstinctReflex 只管调用，不再把一堆散落的 public 字段
/// 直接摆在组件本身上——这样本能系统全部的调参入口能一眼看全，也跟其余配置中心观感统一。
/// </summary>
public static class InstinctProtocolConfig
{
    // ==================== 危险感知 ====================

    /// <summary>本能反射自己独立的危险扫描半径，不跟视觉/听觉共用——贴身覆写和"接近威胁"这两档
    /// 都不检查视觉扇形（背后贴脸也感觉得到）。
    ///
    /// ⚠️ 2026-07-13 收窄：曾经故意比 PerceptionRadar.perceptionRadius（默认 15f）更大（18f），
    /// 想给"背后偷袭"留安全网，结果代价是本能会对大脑（Prompt 里"当前感知"列表，受视野范围+扇形
    /// 双重限制）根本还没看到的敌人做出全力逃跑反应，表现成"莫名其妙凭空逃跑"。现在对齐到不超过
    /// 视觉半径——"背后感觉得到"这个安全网依然存在（本档判定本身不检查扇形角度，只是最大探测距离
    /// 缩短了），只是不再比大脑能看到的范围更远。如果以后调整 PerceptionRadar.perceptionRadius，
    /// 这个常量也要跟着一起看一眼，两者没有强制联动。</summary>
    public const float DangerSenseRadius = 15f;

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

    // ==================== 专注系统 ====================

    /// <summary>"专注"状态（LocalMotorController.CurrentFocusTarget）——大脑当前明确点名要
    /// 交战/接近的目标，在专注期间不会被本能判定成偷袭——最长能不经确认地维持多久。
    ///
    /// ⚠️ 2026-07-20：专注只由两条规则控制生死：这里的超时自然淡去，以及大脑换目标/换成无
    /// 目标计划时刷新（见 CharacterActuator.UpdateFocusFromPlan）。挨打不再触发即刻解除——
    /// 用户明确要求"目标是攻击就该打到分出结果，挨一下就跑太奇怪了"，所以专注一旦建立，
    /// 只会因为超时没人确认、或者大脑自己换了主意才会解除，不会因为对方还手了一下就被打断。
    ///
    /// 取值参考：GeminiHttpClient 的请求超时是 15 秒，TimeManager.aiThinkInterval 常规思考
    /// 周期是 20 秒——这里给到 25 秒，能扛住一两次网络抖动，但不会无限期装死等一个可能已经
    /// 失联的请求。</summary>
    public const float FocusDecayTimeout = 25f;

    /// <summary>NPC 自身生命值低于 MaxHealth 的这个比例时，不管专注还剩多久、目标换没换，
    /// 强制让专注对当前目标的豁免失效，把判断权立即交还本能——纯粹的生存兜底，不是"值不值得
    /// 继续打"的权衡（那个仍然按计划推迟，见记忆 risk_tradeoff_system_deferred）。
    ///
    /// ⚠️ 2026-07-20 新增：专注去掉"挨打即刻解除"之后，如果攻击一直没命中/没能打死对方，
    /// 而对方一直打得中，专注会在超时（最长 25 秒）之前全程蒙蔽本能，NPC 可能被生生打死都
    /// 不会有任何自救反应。这里是纯阈值触发的安全网，跟"贴身即视为危险"性质一样，不看伤害
    /// 轻重、不看双方战局，只看"我是不是快死了"——绝大多数正常交火根本不会触发这条线，
    /// 只有真的要被打死时才会响。取值参考：狼单口伤害 10（NPCAttributes.MaxHealth 的 10%），
    /// 0.3 意味着触发后本能还有大约两口的窗口能把 NPC 带离贴身距离。</summary>
    public const float CriticalHealthRatio = 0.3f;

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
