using UnityEngine;

public class SemanticObject : MonoBehaviour
{
    [Header("具身智能语义标签")]
    [Tooltip("直接在下拉菜单中选择，无需手动拼写字符串")]
    public SemanticType semanticType;

    /// <summary>
    /// 开放一个只读属性，外界（如雷达）调用时，物体自己就能报出自己的物理机制说明
    /// </summary>
    public string MechanismDescription
    {
        get { return SandboxProtocolConfig.GetMechanismDescription(semanticType); }
    }
}