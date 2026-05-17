// SimulationManager.cs
using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    [Header("绑定的 NPC 目标")]
    public GameObject npcCharacter;

    public void ResetSimulation()
    {
        Debug.Log("<color=#FFCC00>[系统管理] 📥 正在重置具身智能物理沙盒...</color>");

        if (EnvironmentManager.Instance != null)
        {
            EnvironmentManager.Instance.ClearAllSpawnedFoods();
            EnvironmentManager.Instance.StartEnvironmentSystem();
        }

        if (npcCharacter != null)
        {
            if (npcCharacter.TryGetComponent<AIBrainController>(out var brain))
            {
                // 重置大脑状态（兼容新大小脑系统）
                if (brain != null)
                {
                    brain.InterruptAndClearGoal();           // 新增的方法
                                                             // 如果还有其他需要重置的逻辑可以在这里加
                }
            }

            if (npcCharacter.TryGetComponent<NPCAttributes>(out var attributes))
            {
                attributes.ResetAttributes();
            }
        }

        // 🌟【完美对齐】：让全场景所有遵循 UniversalPhysicsEntity 的刚体全部优雅重置并重新加载静态配置
        UniversalPhysicsEntity[] allEntities = FindObjectsByType<UniversalPhysicsEntity>(FindObjectsInactive.Include);
        foreach (var entity in allEntities)
        {
            entity.ResetEntity();
        }

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.ResetTime();
        }

        Debug.Log("<color=#00FF00>[系统管理] ✅ 物理环境与世界线已归位！</color>");
    }
}