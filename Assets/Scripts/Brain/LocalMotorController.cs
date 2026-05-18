using UnityEngine;
using System.Collections.Generic;
using EmbodiedAI.DTO;

[RequireComponent(typeof(CharacterActuator))]
[RequireComponent(typeof(AIBrainController))]
public class LocalMotorController : MonoBehaviour
{
    private CharacterActuator actuator;
    private AIBrainController brain;

    // 标记当前小脑是否正在执行一连串计划
    private bool isBusy = false;

    void Awake()
    {
        actuator = GetComponent<CharacterActuator>();
        brain = GetComponent<AIBrainController>();

        // 监听执行器，当一轮动作序列彻底做完时，释放小脑状态
        if (actuator != null)
        {
            actuator.OnSequenceFinished += () => { isBusy = false; };
        }
    }

    // 大脑决策完成后，直接把整个多步计划打包扔过来
    public void SetNewPlan(List<PlanStep> planSteps, string initialGoal = "")
    {
        if (isBusy)
        {
            Debug.LogWarning("[小脑] ⚠️ 当前序列正在执行中，拒绝新计划（除非调用 InterruptAndClear）");
            return;
        }

        if (planSteps == null || planSteps.Count == 0) return;

        // 将大语言模型的 Plan 转换为底层的原子指令包
        List<PrimitiveCommand> cmdList = new List<PrimitiveCommand>();
        foreach (var step in planSteps)
        {
            var cmd = new PrimitiveCommand
            {
                op = step.arrival_op ?? "APPLY_FORCE",
                hand = step.hand,
                target_id = step.target_id,
                arg_x = step.arg_x,
                arg_z = step.arg_z,
                strength = step.strength
            };
            cmdList.Add(cmd);
        }

        Debug.Log($"<color=orange>[小脑] 📋 接收到 {planSteps.Count} 步原子计划，打包交付物理执行器</color>");

        isBusy = true;
        // 一把将整个动作包扔给协程流水线
        actuator.ExecutePrimitiveSequence(cmdList, null);
    }

    // 兼容原有的单条指令增量发送接口
    public void ExecuteCommands(List<PrimitiveCommand> commands)
    {
        if (isBusy) return;
        if (commands == null || commands.Count == 0) return;

        isBusy = true;
        actuator.ExecutePrimitiveSequence(commands, null);
        Debug.Log($"<color=orange>[小脑] 📥 接收到 {commands.Count} 条独立原子原语并执行</color>");
    }

    public void InterruptAndClear()
    {
        isBusy = false;
        if (actuator != null) actuator.StopAllPhysicalMovement();
        Debug.Log("<color=gray>[小脑] 🛑 强制中断当前物理动作并清空状态</color>");
    }
}