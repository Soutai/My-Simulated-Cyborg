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
    public LocalMotorController smallBrain;   // 新增

    [Header("UI")]
    public UnityEngine.UI.Text monologueDisplay;
    public UnityEngine.UI.Text actionDisplay;

    private bool isThinking = false;
    private string currentGoal = "无";

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
        if (isThinking)
        {
            yield break;
        }

        isThinking = true;

        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 🧠 开始决策... 当前目标: 【{currentGoal}】");

        // ==================== 关键修复：强制获取组件 ====================
        if (attributes == null) attributes = GetComponent<NPCAttributes>();
        if (radar == null) radar = GetComponent<PerceptionRadar>();
        if (promptManager == null) promptManager = GetComponent<PromptManager>();
        if (httpClient == null) httpClient = GetComponent<GeminiHttpClient>();
        if (actuator == null) actuator = GetComponent<CharacterActuator>();
        if (smallBrain == null) smallBrain = GetComponent<LocalMotorController>();

        // 再次检查关键组件
        if (promptManager == null || radar == null || attributes == null || httpClient == null)
        {
            Debug.LogError($"[大脑] 严重错误：核心组件仍然为空！当前组件状态： Prompt={promptManager}, Radar={radar}, Attributes={attributes}");
            isThinking = false;
            yield break;
        }

        string timeStr = TimeManager.Instance?.GetCurrentTimeString() ?? "08:00";
        string radarData = radar.ScanEnvironmentToSemanticJson();
        string held = (actuator != null && actuator.CurrentGrabbedObject != null)
                      ? actuator.CurrentGrabbedObject.name
                      : "手无寸铁";

        string fullPrompt = promptManager.GeneratePhysicsEnginePrompt(
            attributes.satiety,
            attributes.personality,
            timeStr,
            radarData,
            held,
            currentGoal ?? "无");

        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 📤 发送Prompt给Gemini");

        httpClient.PostPrompt(fullPrompt, OnAIResponseReceived, OnAIRequestFailed);

        yield break;
    }

    private void OnAIResponseReceived(string aiRawText)
    {
        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 📥 收到AI回复");

        AIPhysicsDecision decision = ParseBrainResponse(aiRawText);
        if (decision == null)
        {
            isThinking = false;
            return;
        }

        // 更新Goal（核心）
        if (!string.IsNullOrEmpty(decision.goal))
        {
            currentGoal = decision.goal;
            if (smallBrain != null)
                smallBrain.SetNewGoal(decision.goal, decision.goal_target_id);
        }

        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 🎯 决策新目标 → 【{decision.goal}】");

        // 执行短期原子动作
        if (decision.primitive_commands != null && decision.primitive_commands.Count > 0)
        {
            actuator.ExecutePrimitiveSequence(decision.primitive_commands, actionDisplay);
        }

        if (monologueDisplay) monologueDisplay.text = decision.monologue;
        isThinking = false;
    }

    private void OnAIRequestFailed()
    {
        Debug.LogWarning($"[{GetCurrentTimestamp()}] [大脑] ❌ 请求失败");
        isThinking = false;
    }

    private AIPhysicsDecision ParseBrainResponse(string rawText)
    {
        // 提取JSON（保持你原来的正则或简单处理）
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

    void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnAITick -= OnPhysicsBrainTick;
    }

    // 供小脑调用
    public void RequestImmediateThink()
    {
        if (!isThinking) OnPhysicsBrainTick();
    }

    // 添加到 AIBrainController 类末尾
    public void InterruptAndClearGoal()
    {
        currentGoal = "无";
        if (smallBrain != null)
            smallBrain.InterruptAndClearGoal();

        if (actuator != null)
            actuator.StopAllPhysicalMovement();

        Debug.Log("<color=red>[大脑] 已重置所有Goal和动作（Simulation Reset）</color>");
    }
}