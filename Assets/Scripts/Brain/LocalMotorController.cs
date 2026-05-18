using UnityEngine;
using System.Collections.Generic;
using EmbodiedAI.DTO;
using System.Linq;

[RequireComponent(typeof(CharacterActuator))]
[RequireComponent(typeof(PerceptionRadar))]
public class LocalMotorController : MonoBehaviour
{
    [Header("小脑参数")]
    public float tickInterval = 0.2f;
    public float approachDistance = 1.5f;

    private CharacterActuator actuator;
    private PerceptionRadar radar;
    private AIBrainController brain;

    // 当前正在执行的目标
    private string currentGoal = "无";
    private string currentGoalTargetId = "";
    private PrimitiveCommand arrivalCommand = null;

    // 🌟 多步计划队列
    private List<PlanStep> currentPlanSteps = new List<PlanStep>();
    private int currentStepIndex = 0;

    private float lastTickTime = 0f;

    // 🌟 持久目标相关
    private string currentPersistentGoal = "";
    private PersistentIntent currentPersistentIntent = PersistentIntent.None;

    void Awake()
    {
        actuator = GetComponent<CharacterActuator>();
        radar = GetComponent<PerceptionRadar>();
        brain = GetComponent<AIBrainController>();
    }

    void Update()
    {
        if (Time.time - lastTickTime < tickInterval) return;
        lastTickTime = Time.time;
        TickSmallBrain();
    }

    // 接收大脑下发的计划（单步或多步）
    public void SetNewPlan(List<PlanStep> planSteps, string initialGoal = "")
    {
        // 取消之前的持久探索
        CancelInvoke("WanderStep");

        currentPlanSteps = planSteps ?? new List<PlanStep>();
        currentStepIndex = 0;

        if (currentPlanSteps.Count > 0)
        {
            var firstStep = currentPlanSteps[0];
            currentGoal = firstStep.description;
            currentGoalTargetId = firstStep.target_id;

            PrimitiveCommand cmd = new PrimitiveCommand
            {
                op = firstStep.arrival_op ?? "GRAB",
                hand = firstStep.hand,
                target_id = firstStep.target_id
            };

            arrivalCommand = cmd;

            Debug.Log($"<color=orange>[小脑] 📋 接收到 {currentPlanSteps.Count} 步计划 → 开始执行第1步: {firstStep.description}</color>");
        }
        else if (!string.IsNullOrEmpty(initialGoal))
        {
            currentGoal = initialGoal;
        }
    }

    private void TickSmallBrain()
    {
        if (string.IsNullOrEmpty(currentGoalTargetId)) return;

        GameObject targetObj = GameObject.Find(currentGoalTargetId);
        if (targetObj == null)
        {
            Debug.LogWarning($"[小脑] 目标 {currentGoalTargetId} 已消失");
            NextStepOrReset();
            return;
        }

        float dist = Vector3.Distance(transform.position, targetObj.transform.position);

        if (dist > approachDistance)
        {
            MoveTowardsTarget(targetObj.transform.position);
        }
        else
        {
            Debug.Log($"<color=green>[小脑] ✨ 已抵达目标 【{currentGoalTargetId}】 {dist:F2}m</color>");

            if (actuator != null) actuator.StopAllPhysicalMovement();

            if (arrivalCommand != null && !string.IsNullOrEmpty(arrivalCommand.op))
            {
                Debug.Log($"<color=yellow>[小脑] ⚡ 执行到达动作: {arrivalCommand.op}</color>");
                List<PrimitiveCommand> cmds = new List<PrimitiveCommand> { arrivalCommand };
                actuator.ExecutePrimitiveSequence(cmds, null);
            }

            NextStepOrReset();
        }
    }

    private void NextStepOrReset()
    {
        currentStepIndex++;

        if (currentStepIndex < currentPlanSteps.Count)
        {
            var nextStep = currentPlanSteps[currentStepIndex];
            currentGoal = nextStep.description;
            currentGoalTargetId = nextStep.target_id;

            arrivalCommand = new PrimitiveCommand
            {
                op = nextStep.arrival_op ?? "GRAB",
                hand = nextStep.hand,
                target_id = nextStep.target_id
            };

            Debug.Log($"<color=cyan>[小脑] 📌 自动推进到计划第 {currentStepIndex + 1} 步 → {nextStep.description}</color>");
        }
        else
        {
            Debug.Log("<color=lime>[小脑] ✅ 完整计划执行完毕，请求大脑重新思考</color>");
            currentPlanSteps.Clear();
            currentStepIndex = 0;
            currentGoal = "无";
            currentGoalTargetId = "";
            arrivalCommand = null;

            if (brain != null) brain.RequestImmediateThink();
        }
    }

    // 🌟 处理持久目标（长期自主行为）
    public void SetPersistentGoal(string goalDescription)
    {
        if (string.IsNullOrEmpty(goalDescription)) return;

        // 取消之前的随机移动
        CancelInvoke("WanderStep");

        currentPersistentGoal = goalDescription;

        var strategy = SandboxProtocolConfig.GetStrategy(goalDescription);

        if (strategy != null)
        {
            currentPersistentIntent = strategy.intentType;
            Debug.Log($"<color=cyan>[小脑] 🌌 进入持久意图 → {strategy.intentType}</color>");
            Debug.Log($"<color=cyan>[小脑] 📜 执行策略: {strategy.executionGuidance}</color>");

            // 【重要修改】不再主动随机移动，等待 AI 下发 APPLY_FORCE
            // 小脑只标记自己处于持久模式
        }
        else
        {
            Debug.LogWarning($"[小脑] 未知持久目标: {goalDescription}");
        }
    }

    private void MoveTowardsTarget(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        var commands = new List<PrimitiveCommand>
        {
            new PrimitiveCommand { op = "APPLY_FORCE", arg_x = dir.x * 3.5f, arg_z = dir.z * 3.5f }
        };
        actuator.ExecutePrimitiveSequence(commands, null);
    }

    public void InterruptAndClearGoal()
    {
        CancelInvoke("WanderStep");   // 停止持久探索

        currentPlanSteps.Clear();
        currentStepIndex = 0;
        currentPersistentGoal = "";
        currentPersistentIntent = PersistentIntent.None;
        currentGoal = "无";
        currentGoalTargetId = "";
        arrivalCommand = null;

        if (actuator != null) actuator.StopAllPhysicalMovement();
    }

    public bool IsPlanEmpty()
    {
        return currentPlanSteps == null || currentPlanSteps.Count == 0 || currentStepIndex >= currentPlanSteps.Count;
    }
}