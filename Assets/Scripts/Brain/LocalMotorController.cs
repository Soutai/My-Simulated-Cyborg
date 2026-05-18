using UnityEngine;
using System.Collections.Generic;
using EmbodiedAI.DTO;

[RequireComponent(typeof(CharacterActuator))]
[RequireComponent(typeof(PerceptionRadar))]
public class LocalMotorController : MonoBehaviour
{
    [Header("小脑参数")]
    public float tickInterval = 0.2f;

    private CharacterActuator actuator;
    private AIBrainController brain;

    private Queue<PrimitiveCommand> commandQueue = new Queue<PrimitiveCommand>();

    private float lastTickTime = 0f;

    void Awake()
    {
        actuator = GetComponent<CharacterActuator>();
        brain = GetComponent<AIBrainController>();
    }

    void Update()
    {
        if (Time.time - lastTickTime < tickInterval) return;
        lastTickTime = Time.time;
        TickSmallBrain();
    }

    public void SetNewPlan(List<PlanStep> planSteps, string initialGoal = "")
    {
        commandQueue.Clear();

        if (planSteps != null && planSteps.Count > 0)
        {
            foreach (var step in planSteps)
            {
                var cmd = new PrimitiveCommand
                {
                    op = step.arrival_op ?? "APPLY_FORCE",
                    hand = step.hand,
                    target_id = step.target_id
                };
                commandQueue.Enqueue(cmd);
            }

            Debug.Log($"<color=orange>[小脑] 📋 接收到 {planSteps.Count} 步原子计划，开始顺序执行</color>");
        }
    }

    private void TickSmallBrain()
    {
        if (commandQueue.Count == 0) return;

        PrimitiveCommand cmd = commandQueue.Dequeue();

        Debug.Log($"<color=yellow>[小脑] ⚙️ 执行原子原语: {cmd.op}</color>");

        List<PrimitiveCommand> single = new List<PrimitiveCommand> { cmd };
        actuator.ExecutePrimitiveSequence(single, null);
    }

    public void ExecuteCommands(List<PrimitiveCommand> commands)
    {
        if (commands == null || commands.Count == 0) return;

        foreach (var cmd in commands)
        {
            commandQueue.Enqueue(cmd);
        }

        Debug.Log($"<color=orange>[小脑] 📥 接收到 {commands.Count} 条原子原语</color>");
    }

    public void InterruptAndClear()
    {
        commandQueue.Clear();
        if (actuator != null) actuator.StopAllPhysicalMovement();
        Debug.Log("<color=gray>[小脑] 🛑 清空执行队列</color>");
    }
}