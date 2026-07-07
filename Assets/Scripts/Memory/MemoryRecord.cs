using UnityEngine;

/// <summary>
/// 具身智能物理沙盒 - 记忆系统的最小单元：对某一个具体实体的一次"最近目击"记录。
///
/// 🌟 故意保持字段最少、结构通用——这是整个记忆系统未来生长的地基，不是为了糊一个补丁。
/// 以后要扩展"威胁记忆"（记住被谁从哪个方向攻击过）、"经验记忆"（记住某个行动的后果）时，
/// 应该优先考虑在这个结构上加字段，或者做出平行的 XxxMemoryRecord，而不是推翻重来。
///
/// 存的是绝对世界坐标而不是相对偏移量——NPC 自己会动，只有绝对坐标才不会因为身体移动而过时。
/// 汇报给大脑时，用 NPC 当前的实际位置重新计算相对偏移，这样静态物体（食物/武器）的记忆
/// 精确等价于"现在正好看到"，动态物体（敌人）的记忆则会随时间变得不可靠，靠 AgeSeconds 体现。
/// </summary>
public class MemoryRecord
{
    /// <summary>WorldObjectRegistry 里的名字，跟大脑用的 target_id 是同一套身份体系</summary>
    public string EntityId;

    public SemanticType SemanticType;

    /// <summary>最后一次亲眼看到时，它所在的绝对世界坐标</summary>
    public Vector3 LastKnownPosition;

    /// <summary>最后一次亲眼看到的时间点（Time.time）</summary>
    public float LastSeenTimeStamp;

    /// <summary>这条记忆有多旧了（秒）</summary>
    public float AgeSeconds => Time.time - LastSeenTimeStamp;
}
