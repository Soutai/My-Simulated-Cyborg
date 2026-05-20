using UnityEngine;

public class SemanticObject : MonoBehaviour
{
    [Header("具身智能语义标签")]
    [Tooltip("直接在下拉菜单中选择，无需手动拼写字符串")]
    public SemanticType semanticType;

    [Header("物理交互参数（可覆盖全局配置）")]
    [Tooltip("是否覆盖 SandboxProtocolConfig 中的全局默认值")]
    public bool overrideDefaultDistance = false;

    [Tooltip("APPROACH 时希望被靠近到的停止距离（米）")]
    public float desiredApproachDistance = 0.65f;

    [Tooltip("GRAB 允许的最大抓取距离")]
    public float maxGraspDistance = 1.25f;

    /// <summary>
    /// 开放一个只读属性，外界（如雷达）调用时，物体自己就能报出自己的物理机制说明
    /// </summary>
    public string MechanismDescription
    {
        get { return SandboxProtocolConfig.GetMechanismDescription(semanticType); }
    }

    /// <summary>
    /// 获取本物体最终使用的靠近距离（支持 Prefab 覆盖）
    /// </summary>
    public float GetDesiredApproachDistance()
    {
        if (overrideDefaultDistance)
            return desiredApproachDistance;

        var config = SandboxProtocolConfig.GetInteractionConfig(semanticType);
        return config.desiredApproachDistance;
    }

    /// <summary>
    /// 获取本物体最终使用的最大抓取距离
    /// </summary>
    public float GetMaxGraspDistance()
    {
        if (overrideDefaultDistance)
            return maxGraspDistance;

        var config = SandboxProtocolConfig.GetInteractionConfig(semanticType);
        return config.maxGraspDistance;
    }
}