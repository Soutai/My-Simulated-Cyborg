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
        Debug.Log($"<color=lime>[大脑] 🎉 侦测到 {hand}手 抓取成功 → {grabbedObj.name}，立即唤醒决策</color>");
        RequestImmediateThink();
    }

    private void OnAIResponseReceived(string rawResponse, string snapshotHeld, string snapshotGoal)
    {
        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 📥 收到AI回复");

        AIPhysicsDecision decision = ParseBrainResponse(rawResponse);
        if (decision == null)
        {
            Debug.LogError($"[{GetCurrentTimestamp()}] [大脑] ❌ JSON 解析失败");
            isThinking = false;
            return;
        }

        Debug.Log($"<color=yellow>[大脑] 📊 解析成功 → goal='{decision.goal}' | target_id='{decision.goal_target_id}' | arrival_op='{decision.goal_arrival_command?.op}'</color>");
        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 💬 收到的AI原始回复内容如下：\n{rawResponse}");

        if (monologueDisplay != null)
            monologueDisplay.text = decision.monologue;

        if (decision.primitive_commands != null && decision.primitive_commands.Count > 0)
        {
            actuator?.ExecutePrimitiveSequence(decision.primitive_commands, actionDisplay);
        }

        currentGoal = string.IsNullOrEmpty(decision.goal) ? "无" : decision.goal;

        if (smallBrain != null && !string.IsNullOrEmpty(decision.goal_target_id))
        {
            Debug.Log($"<color=orange>[大脑] 🎯 已成功下发目标给小脑 → 【{currentGoal}】 | ID={decision.goal_target_id}</color>");
            smallBrain.SetNewGoal(currentGoal, decision.goal_target_id, decision.goal_arrival_command);
        }

        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 🎯 决策新目标 → 【{currentGoal}】");
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