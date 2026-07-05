using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 运行时语义物体的唯一实例注册表。
/// 用来替代 GameObject.Find（全局层级树搜索，且无法保证名字唯一）和
/// FindObjectsByType（全场景反射扫描），改为 O(1) 的实例直连查找。
/// </summary>
public static class WorldObjectRegistry
{
    private static readonly Dictionary<string, SemanticObject> registry = new Dictionary<string, SemanticObject>();

    public static void Register(SemanticObject obj)
    {
        registry[obj.gameObject.name] = obj;
    }

    public static void Unregister(SemanticObject obj)
    {
        if (registry.TryGetValue(obj.gameObject.name, out var current) && current == obj)
            registry.Remove(obj.gameObject.name);
    }

    /// <summary>
    /// 按大模型下发的 target_id 精确查找唯一实例。
    /// </summary>
    public static GameObject Find(string targetId)
    {
        if (string.IsNullOrEmpty(targetId)) return null;
        return registry.TryGetValue(targetId, out var obj) && obj != null ? obj.gameObject : null;
    }

    /// <summary>
    /// 精确查找失败时的宽松兜底：处理大模型给出的 ID 与实际实例名字不完全一致的情况（如省略了生成序号后缀）。
    /// </summary>
    public static GameObject FindFuzzy(string targetId)
    {
        if (string.IsNullOrEmpty(targetId)) return null;

        foreach (var pair in registry)
        {
            if (pair.Value == null) continue;
            if (pair.Key.Contains(targetId) || targetId.Contains(pair.Key))
                return pair.Value.gameObject;
        }
        return null;
    }

    /// <summary>
    /// 枚举当前所有已注册的语义物体，供"没有指定具体 target_id 时就近搜索"这类兜底逻辑使用。
    /// </summary>
    public static IEnumerable<SemanticObject> All()
    {
        return registry.Values;
    }
}
