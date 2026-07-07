/// <summary>
/// 具身智能物理沙盒 - 记忆系统调参配置中心。
/// 跟 PhysicsProtocolConfig / PersonalityProtocolConfig / InstinctProtocolConfig 同一套模式：
/// 所有硬编码数值集中在这里。
/// </summary>
public static class MemoryProtocolConfig
{
    /// <summary>记忆扫描的采样间隔——故意比大脑思考频率（TimeManager.aiThinkInterval，默认20秒）
    /// 快得多，这样移动途中一闪而过、恰好没被"思考那一瞬间的朝向"捕捉到的物体，也能被记住</summary>
    public const float MemoryScanInterval = 0.2f;

    /// <summary>记忆保留的最长时间（秒），超过这个时间没有重新看到就彻底遗忘，不再汇报给大脑——
    /// 避免早已被吃掉/移走的物体一直挂在记忆里误导决策</summary>
    public const float MemoryRetentionSeconds = 60f;

    /// <summary>记忆超过这个时长（秒）没有刷新，汇报时就要在文字里提醒大脑"这是旧记忆，可能已经不准了"</summary>
    public const float StaleWarningThresholdSeconds = 10f;
}
