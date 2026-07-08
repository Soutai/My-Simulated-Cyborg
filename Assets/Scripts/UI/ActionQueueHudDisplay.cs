using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 具身智能物理沙盒 - 目标/动作/队列调试面板。
///
/// 只做一件事：定期把"大脑当前目标""身体当前执行的动作""还排队等待的动作"整理成
/// 人类可读的文本，显示在 Canvas 上绑定的 Text 组件里——纯展示层，不产生任何行为逻辑。
/// 数据全部来自已有的公开状态（AIBrainController.CurrentGoal、CharacterActuator 的
/// CurrentActionDescription / RemainingQueuedSteps、WanderReflex.IsWandering），
/// 不重复维护一份状态。
/// </summary>
[RequireComponent(typeof(AIBrainController))]
[RequireComponent(typeof(CharacterActuator))]
[RequireComponent(typeof(WanderReflex))]
public class ActionQueueHudDisplay : MonoBehaviour
{
    [Header("UI 绑定")]
    [Tooltip("拖入 Canvas 下面用来显示目标/动作/队列的 Text 组件")]
    public Text displayText;

    [Tooltip("刷新间隔（秒）——不需要每帧都重新整理字符串，省得产生多余的 GC")]
    public float refreshInterval = 0.3f;

    private AIBrainController brain;
    private CharacterActuator actuator;
    private WanderReflex wanderReflex;
    private float timer = 0f;

    void Awake()
    {
        brain = GetComponent<AIBrainController>();
        actuator = GetComponent<CharacterActuator>();
        wanderReflex = GetComponent<WanderReflex>();
    }

    void Update()
    {
        if (displayText == null) return;

        timer += Time.deltaTime;
        if (timer < refreshInterval) return;
        timer = 0f;

        displayText.text = BuildDisplayText();
    }

    private string BuildDisplayText()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"目标: {brain.CurrentGoal}");
        sb.AppendLine($"当前动作: {GetCurrentActionText()}");

        sb.Append("队列: ");
        AppendQueuedActions(sb);

        return sb.ToString();
    }

    private string GetCurrentActionText()
    {
        if (wanderReflex.IsWandering) return "自主漫步探索";
        return !string.IsNullOrEmpty(actuator.CurrentActionDescription)
            ? actuator.CurrentActionDescription
            : "（空闲）";
    }

    private void AppendQueuedActions(StringBuilder sb)
    {
        var queued = actuator.RemainingQueuedSteps;
        if (queued.Count == 0)
        {
            sb.AppendLine("(无)");
            return;
        }

        sb.AppendLine();
        foreach (var step in queued)
        {
            string desc = !string.IsNullOrEmpty(step.description) ? step.description : step.arrival_op;
            sb.AppendLine($"  · {desc}");
        }
    }
}
