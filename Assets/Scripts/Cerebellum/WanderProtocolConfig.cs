/// <summary>
/// 具身智能物理沙盒 - 本地漫步探索系统调参配置中心。
/// 跟 InstinctProtocolConfig / MemoryProtocolConfig 同一套模式：数值集中在这里。
/// </summary>
public static class WanderProtocolConfig
{
    /// <summary>漫步推进力度。🌟 实测校准过：8 这个力度配合刚体实际的质量/阻尼，稳态只能撑到约 1 m/s，
    /// 根本到不了 MaxWanderSpeed 的上限——力度和稳态速度基本成正比，按 8→1.0 m/s 反推，
    /// 想要接近 3.5 m/s 的上限大约需要 28 左右（见 WanderReflex 诊断日志排查记录）</summary>
    public const float WanderForce = 28f;

    /// <summary>漫步最大速度——明显低于逃跑/追击速度，漫步本来就该是悠闲的，不是冲刺</summary>
    public const float MaxWanderSpeed = 3.5f;

    /// <summary>每次选定方向后，最短维持多久才会重新考虑换方向（除非被障碍物挡住）</summary>
    public const float MinDirectionHold = 3f;

    /// <summary>每次选定方向后，最长维持多久必须重新考虑换方向</summary>
    public const float MaxDirectionHold = 6f;

    /// <summary>朝当前方向探测多远算作"前方通畅"，探测距离内撞到障碍物就提前换方向</summary>
    public const float ObstacleLookahead = 4f;

    /// <summary>候选方向如果连这么近的距离都撞东西，直接排除，不参与打分</summary>
    public const float ObstacleRejectRatio = 0.3f;

    /// <summary>换方向时，在候选角度基础上叠加的随机扰动范围（度），避免每次都死板地挑同一批固定角度</summary>
    public const float DirectionJitterDegrees = 15f;

    /// <summary>每隔多久把当前位置记入"最近走过的轨迹"，用来判断哪些方向是老地方、哪些是新地方</summary>
    public const float TrailSampleInterval = 1.5f;

    /// <summary>轨迹最多记多少个点，超过就把最老的丢掉——只需要"最近走过哪"，不需要记住整张地图</summary>
    public const int TrailMaxLength = 12;

    /// <summary>往候选方向探多远算作"这个方向的落点"，用来跟历史轨迹比对新鲜度</summary>
    public const float NoveltyProbeDistance = 6f;

    /// <summary>新鲜度（离历史轨迹有多远）在候选方向打分里的权重——权重越高，越倾向于去没去过的地方，
    /// 而不是纯粹沿着最通畅的方向走（可能导致在同一片开阔区域里来回打转）</summary>
    public const float NoveltyWeight = 0.6f;

    /// <summary>威胁规避（离最近敌人多远）在候选方向打分里的权重。🌟 明显高于 NoveltyWeight——
    /// 避开危险应该比"去没去过的地方"优先得多，不然漫步系统很容易把本能刚从危险区域带出来的身体，
    /// 又主动带回去，形成"逃出来→漫步走回去→本能再逃出来"的死循环</summary>
    public const float ThreatAvoidanceWeight = 3f;
}
