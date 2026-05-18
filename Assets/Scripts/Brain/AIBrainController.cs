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

        Debug.Log($"<color=yellow>[大脑] 📊 解析成功 → goal='{decision.goal}' | plan_steps={decision.plan_steps?.Count ?? 0}</color>");

        if (!string.IsNullOrEmpty(rawResponse))
            Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 💬 收到的AI原始回复内容如下：\n{rawResponse}");

        if (monologueDisplay != null)
            monologueDisplay.text = decision.monologue;

        // ==================== 只执行原子原语 ====================
        List<PrimitiveCommand> commandsToExecute = new List<PrimitiveCommand>();

        // 1. 即时原子动作（最高优先级）
        if (decision.primitive_commands != null && decision.primitive_commands.Count > 0)
        {
            commandsToExecute.AddRange(decision.primitive_commands);
            Debug.Log($"<color=green>[大脑] ⚡ 执行 {decision.primitive_commands.Count} 条即时原子原语</color>");
        }

        // 2. 多步计划 → 转换为原子原语序列
        if (decision.plan_steps != null && decision.plan_steps.Count > 0)
        {
            Debug.Log($"<color=cyan>[大脑] 📋 收到 {decision.plan_steps.Count} 步计划，转换为原子指令序列</color>");

            foreach (var step in decision.plan_steps)
            {
                var cmd = new PrimitiveCommand
                {
                    op = step.arrival_op ?? "APPLY_FORCE",
                    hand = step.hand,
                    target_id = step.target_id
                };
                commandsToExecute.Add(cmd);
            }
        }

        // 执行所有原子原语
        if (commandsToExecute.Count > 0)
        {
            smallBrain?.ExecuteCommands(commandsToExecute);
        }
        else
        {
            Debug.Log("<color=gray>[大脑] 当前无原子原语可执行</color>");
        }

        currentGoal = string.IsNullOrEmpty(decision.goal) ? "无" : decision.goal;
        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 🎯 决策完成 → 当前目标: 【{currentGoal}】");

        isThinking = false;
    }

    private void HandleGrabSuccess(GameObject grabbedObj, string hand)
    {
        Debug.Log($"<color=lime>[大脑] 🎉 抓取成功 → {grabbedObj.name}</color>");
        // 纯原语驱动下，不自动请求新思考，由AI自主决定下一步
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

    public void InterruptAndClearGoal()
    {
        currentGoal = "无";
        if (smallBrain != null) smallBrain.InterruptAndClear();
        if (actuator != null) actuator.StopAllPhysicalMovement();
        isThinking = false;
    }

    void OnDestroy()
    {
        if (actuator != null)
            actuator.OnGrabSuccess -= HandleGrabSuccess;

        if (TimeManager.Instance != null)
            TimeManager.Instance.OnAITick -= OnPhysicsBrainTick;
    }
}