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

    void Start()
    {
        // 🌟 用 Start 而非 Awake 注册：确保像 EnvironmentManager 那样"先 Instantiate 再改名字"的物体，
        // 已经用上了最终名字（大模型据此下发的 target_id）之后才登记进注册表。
        WorldObjectRegistry.Register(this);
    }

    // 🌟 用 OnDisable 而不是 OnDestroy：这个项目里物体"消亡"的唯一方式是
    // UniversalPhysicsEntity 耗尽耐受度后 gameObject.SetActive(false)，只会触发 OnDisable，
    // 不会触发 OnDestroy——挂在 OnDestroy 上的注销逻辑在这个项目里从未真正生效过，导致死掉的
    // 物体永远滞留在 WorldObjectRegistry 里，大脑还能凭记忆点名一个早就不存在的目标，
    // APPROACH 对着一个碰撞体已禁用的对象算距离也会得到不可靠的结果（比如读到 0 米，
    // 误判"瞬间到达"）。OnDisable 会在真正 Destroy 之前也被调用一次，所以这一处改动
    // 同时覆盖"被禁用"和"被销毁"两种消失方式，不需要两处都写。
    void OnDisable()
    {
        WorldObjectRegistry.Unregister(this);
    }

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