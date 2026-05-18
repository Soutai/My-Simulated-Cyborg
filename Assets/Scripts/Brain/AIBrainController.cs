using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using EmbodiedAI.DTO;

public class AIBrainController : MonoBehaviour
{
    [Header("组件")]
    public NPCAttributes attributes;
    public PerceptionRadar radar;
    public PromptManager promptManager;
    public GeminiHttpClient httpClient;
    public CharacterActuator actuator;
    public LocalMotorController smallBrain;

    [Header("UI")]
    public UnityEngine.UI.Text monologueDisplay;
    public UnityEngine.UI.Text actionDisplay;

    private bool isThinking = false;
    private string currentGoal = "无";

    void Awake()
    {
        if (attributes == null) attributes = GetComponent<NPCAttributes>();
        if (radar == null) radar = GetComponent<PerceptionRadar>();
        if (promptManager == null) promptManager = GetComponent<PromptManager>();
        if (httpClient == null) httpClient = GetComponent<GeminiHttpClient>();
        if (actuator == null) actuator = GetComponent<CharacterActuator>();
        if (smallBrain == null) smallBrain = GetComponent<LocalMotorController>();

        if (actuator != null)
            actuator.OnGrabSuccess += HandleGrabSuccess;
    }

    void Start()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnAITick += OnPhysicsBrainTick;

        StartCoroutine(DelayedFirstTick());
    }

    private IEnumerator DelayedFirstTick()
    {
        yield return new WaitForSeconds(1f);
        OnPhysicsBrainTick();
    }

    public void OnPhysicsBrainTick()
    {
        if (isThinking) return;
        StartCoroutine(ThinkPhysicsRoutine());
    }

    private IEnumerator ThinkPhysicsRoutine()
    {
        if (isThinking) yield break;
        isThinking = true;

        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 🧠 开始决策... 当前目标: 【{currentGoal}】");

        string timeStr = TimeManager.Instance?.GetCurrentTimeString() ?? "08:00";
        string radarData = radar.ScanEnvironmentToSemanticJson();

        string leftHand = actuator?.LeftHandObject != null ? actuator.LeftHandObject.name : "空无一物";
        string rightHand = actuator?.RightHandObject != null ? actuator.RightHandObject.name : "空无一物";

        string fullPrompt = promptManager.GeneratePhysicsEnginePrompt(
            attributes.satiety,
            attributes.personality,
            timeStr,
            radarData,
            leftHand,
            rightHand,
            currentGoal ?? "无");

        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 📤 发送Prompt给Gemini");
        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 📄 发送给AI的完整Prompt内容如下：\n{fullPrompt}");

        httpClient.PostPrompt(fullPrompt, (response) => OnAIResponseReceived(response, leftHand + "|" + rightHand, currentGoal), OnAIRequestFailed);

        yield break;
    }

    private void HandleGrabSuccess(GameObject grabbedObj, string hand)
    {
        Debug.Log($"<color=lime>[大脑] 🎉 抓取成功 → {grabbedObj.name}，检查是否需要重新思考...</color>");

        // 🌟 关键优化：只有在当前没有正在执行的多步计划时，才请求新思考
        if (smallBrain != null && smallBrain.IsPlanEmpty())
        {
            Debug.Log("<color=lime>[大脑] 📍 当前计划已完成，请求大脑重新思考</color>");
            RequestImmediateThink();
        }
        else
        {
            Debug.Log("<color=cyan>[大脑] 📍 仍在执行多步计划中，无需立即重新思考</color>");
        }
    }

    private void OnAIResponseReceived(string rawResponse, string snapshotHeld, string snapshotGoal)
    {
        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 📥 收到AI回复");

        AIPhysicsDecision decision = ParseBrainResponse(rawResponse);
        if (decision == null)
        {
            Debug.LogError($"[{GetCurrentTimestamp()}] [大脑] ❌ JSON 反序列化失败！");
            isThinking = false;
            return;
        }

        Debug.Log($"<color=yellow>[大脑] 📊 解析成功 → goal='{decision.goal}' | plan_steps={decision.plan_steps?.Count ?? 0} | persistent_goal='{decision.persistent_goal}'</color>");

        if (!string.IsNullOrEmpty(rawResponse))
            Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 💬 收到的AI原始回复内容如下：\n{rawResponse}");

        if (monologueDisplay != null)
            monologueDisplay.text = decision.monologue;

        // 1. 执行即时原子动作（最高优先级）
        if (decision.primitive_commands != null && decision.primitive_commands.Count > 0)
        {
            Debug.Log($"<color=green>[大脑] ⚡ 执行 {decision.primitive_commands.Count} 条即时原语</color>");
            actuator?.ExecutePrimitiveSequence(decision.primitive_commands, actionDisplay);
        }

        // 2. 持久目标（长期自主行为）—— 优先处理
        if (!string.IsNullOrEmpty(decision.persistent_goal))
        {
            Debug.Log($"<color=magenta>[大脑] 🌌 检测到持久目标 → {decision.persistent_goal}</color>");
            if (smallBrain != null)
            {
                smallBrain.SetPersistentGoal(decision.persistent_goal);
            }
            isThinking = false;
            return;   // 直接返回，不再执行短期计划
        }

        // 3. 多步短期计划
        if (decision.plan_steps != null && decision.plan_steps.Count > 0)
        {
            Debug.Log($"<color=cyan>[大脑] 📋 收到多步计划，共 {decision.plan_steps.Count} 步</color>");
            if (smallBrain != null)
            {
                smallBrain.SetNewPlan(decision.plan_steps, decision.goal);
            }
        }
        // 4. 兼容旧的单步 goal
        else if (smallBrain != null && !string.IsNullOrEmpty(decision.goal_target_id))
        {
            string goalStr = string.IsNullOrEmpty(decision.goal) ? "无" : decision.goal;
            Debug.Log($"<color=orange>[大脑] 🎯 单步目标 → 【{goalStr}】 | ID={decision.goal_target_id}</color>");

            var singleStep = new List<PlanStep>
        {
            new PlanStep
            {
                description = goalStr,
                target_id = decision.goal_target_id,
                arrival_op = decision.goal_arrival_command?.op ?? "GRAB",
                hand = decision.goal_arrival_command?.hand
            }
        };
            smallBrain.SetNewPlan(singleStep, goalStr);
        }
        else
        {
            Debug.Log("<color=gray>[大脑] 当前无新计划或持久目标</color>");
        }

        currentGoal = string.IsNullOrEmpty(decision.goal) ? "无" : decision.goal;
        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 🎯 决策完成 → 当前计划: 【{currentGoal}】 | 持久目标: 【{decision.persistent_goal}】");

        isThinking = false;
    }
    private void OnAIRequestFailed()
    {
        Debug.LogWarning($"[{GetCurrentTimestamp()}] [大脑] ❌ 请求失败");
        isThinking = false;
    }

    private AIPhysicsDecision ParseBrainResponse(string rawText)
    {
        int start = rawText.IndexOf("{");
        int end = rawText.LastIndexOf("}") + 1;
        if (start >= 0 && end > start)
        {
            string json = rawText.Substring(start, end - start);
            return JsonUtility.FromJson<AIPhysicsDecision>(json);
        }
        return null;
    }

    private string GetCurrentTimestamp() => System.DateTime.Now.ToString("HH:mm:ss");

    public void RequestImmediateThink()
    {
        if (!isThinking) OnPhysicsBrainTick();
    }

    // 🌟 SimulationManager 需要的重置方法
    public void InterruptAndClearGoal()
    {
        currentGoal = "无";

        if (smallBrain != null)
            smallBrain.InterruptAndClearGoal();

        if (actuator != null)
            actuator.StopAllPhysicalMovement();

        isThinking = false;
        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] ⚠️ 外部重置信号触发");
    }

    void OnDestroy()
    {
        if (actuator != null)
            actuator.OnGrabSuccess -= HandleGrabSuccess;

        if (TimeManager.Instance != null)
            TimeManager.Instance.OnAITick -= OnPhysicsBrainTick;
    }
}