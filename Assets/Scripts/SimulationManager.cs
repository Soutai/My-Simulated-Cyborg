using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    [Header("绑定的 NPC 目标")]
    public GameObject npcCharacter;

    // 绑定 Reset 按钮
    public void ResetSimulation()
    {
        Debug.Log("<color=#FFCC00>[系统管理] 📥 正在重置仿真环境...</color>");

        // 🌟 最小化追加：重置世界时，把随机生成的水果一键清空，并让计时器复位重新开始算时间
        if (EnvironmentManager.Instance != null)
        {
            EnvironmentManager.Instance.ClearAllSpawnedFoods();
            EnvironmentManager.Instance.StartEnvironmentSystem();
        }

        if (npcCharacter != null)
        {
            // 1. 强行让大脑停下任何正在挂起的网络思考或生命周期协程
            if (npcCharacter.TryGetComponent<AIBrainController>(out var brain))
            {
                brain.StopAllCoroutines();
                brain.ResetBrainState(); // 👈 🌟新追加：重置大脑内部的飞翔锁与文本
            }

            // 2. 强行刹车，掐断身体任何正在平滑移动的运动
            if (npcCharacter.TryGetComponent<CharacterActuator>(out var actuator))
            {
                actuator.StopMovement();
            }

            // 3. 将 NPC 的数据、位置、饿感、变色全部复位
            if (npcCharacter.TryGetComponent<NPCAttributes>(out var attributes))
            {
                attributes.ResetAttributes();
                // 💡 既存的处理已经完美平移至 AIBrainController.ResetBrainState 中
            }
        }

        // 🌟 新追加：让世界线时间也倒流回早上 08:00
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.ResetTime();
        }

        Debug.Log("<color=#00FF00>[系统管理] ✅ 人物与世界线已归位！正在清理控制台...</color>");

        // 4. 一键清空 Unity 编辑器控制台
        ClearUnityConsole();
    }

    private void ClearUnityConsole()
    {
#if UNITY_EDITOR
        var assembly = System.Reflection.Assembly.Load("UnityEditor");
        var type = assembly.GetType("UnityEditor.LogEntries");
        var method = type.GetMethod("Clear");
        if (method != null)
        {
            method.Invoke(new object(), null);
        }
#endif
    }
}