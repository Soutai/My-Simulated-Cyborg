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

    // AIBrainController.cs 内部的方法修改
    private void OnAIResponseReceived(string rawResponse)
    {
        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 📥 收到AI回复");

        AIPhysicsDecision decision = ParseBrainResponse(rawResponse);
        if (decision == null)
        {
            Debug.LogError($"[{GetCurrentTimestamp()}] [大脑] ❌ JSON 反序列化失败！原始数据: {rawResponse}");
            isThinking = false;
            return;
        }

        // 1. 处理大脑即时动作（例如当下的物理微调、闪避力、内心独白显示）
        if (monologueDisplay != null)
            monologueDisplay.text = decision.monologue;

        if (decision.primitive_commands != null && decision.primitive_commands.Count > 0)
        {
            if (actuator != null)
            {
                actuator.ExecutePrimitiveSequence(decision.primitive_commands, actionDisplay);
            }
        }

        // 2. 核心架构对齐：解耦硬编码，把战略目标和托管原语通通打包给小脑
        currentGoal = string.IsNullOrEmpty(decision.goal) ? "无" : decision.goal;

        if (smallBrain != null)
        {
            // 🌟 核心点：将目标描述、目标ID、以及大脑托管的“临门一脚动作”一起传给小脑
            // 注意：这里需要确保你的 LocalMotorController.SetNewGoal 方法签名已修改为接收这三个参数
            smallBrain.SetNewGoal(currentGoal, decision.goal_target_id, decision.goal_arrival_command);
        }

        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 🎯 决策新目标 → 【{currentGoal}】 | 托管动作: {(decision.goal_arrival_command != null ? decision.goal_arrival_command.op : "无")}");
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

    public void InterruptAndClearGoal()
    {
        currentGoal = "无";

        // 🌟 加强健壮性：先检查小脑组件是否存在，再进行清理
        if (smallBrain != null)
        {
            smallBrain.InterruptAndClearGoal();
        }

        if (actuator != null)
        {
            actuator.StopAllPhysicalMovement();
        }

        isThinking = false;
        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] ⚠️ 外部重置信号触发：已强制中断当前思考并格式化所有战略意图。");
    }
}