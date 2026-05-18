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

        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 🔍 组件检查 → smallBrain: {(smallBrain != null ? "✅ 存在" : "❌ NULL")}");  // ← 只加这一行

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

        // ======= 在控制台打印发送给 AI 的完整 Prompt （保留原有风格）=======
        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 📄 发送给AI的完整Prompt内容如下：\n{fullPrompt}");

        // ==================== 🛠️ 最小修改：捕获发送网络请求瞬间的关键状态快照 ====================
        string snapshotHeld = held;
        string snapshotGoal = currentGoal;

        httpClient.PostPrompt(fullPrompt, (response) => OnAIResponseReceived(response, snapshotHeld, snapshotGoal), OnAIRequestFailed);

        yield break;
    }

    // AIBrainController.cs 内部的方法修改
    private void OnAIResponseReceived(string rawResponse, string snapshotHeld, string snapshotGoal)
    {
        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 📥 收到AI回复");

        AIPhysicsDecision decision = ParseBrainResponse(rawResponse);
        if (decision == null)
        {
            Debug.LogError($"[{GetCurrentTimestamp()}] [大脑] ❌ JSON 反序列化失败！原始数据: {rawResponse}");
            isThinking = false;
            return;
        }

        Debug.Log($"<color=yellow>[大脑] 📊 解析成功 → goal='{decision.goal}' | target_id='{decision.goal_target_id}' | arrival_op='{decision.goal_arrival_command?.op}'</color>");  // ← 只加这一行

        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 💬 收到的AI原始回复内容如下：\n{rawResponse}");

        // ==================== 原有逻辑保持不变 ====================
        string currentHeld = (actuator != null && actuator.CurrentGrabbedObject != null) ? actuator.CurrentGrabbedObject.name : "手无寸铁";

        if (snapshotHeld == "手无寸铁" && currentHeld != "手无寸铁")
        {
            Debug.LogWarning($"[{GetCurrentTimestamp()}] [大脑] ⚠️ 时空错位拦截：网络延迟期间，小脑已成功获取武器【{currentHeld}】！");
            isThinking = false;
            return;
        }

        if (!string.IsNullOrEmpty(decision.goal_target_id) && radar != null)
        {
            string currentRadarJson = radar.ScanEnvironmentToSemanticJson();
            if (!currentRadarJson.Contains($"\"object_id\": \"{decision.goal_target_id}\""))
            {
                Debug.LogWarning($"[{GetCurrentTimestamp()}] [大脑] ⚠️ 目标物体【{decision.goal_target_id}】已不存在！");
                isThinking = false;
                return;
            }
        }

        if (monologueDisplay != null)
            monologueDisplay.text = decision.monologue;

        if (decision.primitive_commands != null && decision.primitive_commands.Count > 0)
        {
            if (actuator != null)
            {
                actuator.ExecutePrimitiveSequence(decision.primitive_commands, actionDisplay);
            }
        }

        currentGoal = string.IsNullOrEmpty(decision.goal) ? "无" : decision.goal;

        if (smallBrain != null && !string.IsNullOrEmpty(decision.goal_target_id))
        {
            Debug.Log($"<color=orange>[大脑] 🎯 已成功下发目标给小脑 → 【{currentGoal}】 | ID={decision.goal_target_id}</color>");  // ← 只加这一行
            smallBrain.SetNewGoal(currentGoal, decision.goal_target_id, decision.goal_arrival_command);
        }
        else if (smallBrain == null)
        {
            Debug.LogError("<color=red>[大脑] ❌ smallBrain (LocalMotorController) 组件未找到！</color>");
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

    public void RequestImmediateThink()
    {
        if (!isThinking) OnPhysicsBrainTick();
    }

    public void InterruptAndClearGoal()
    {
        currentGoal = "无";

        if (smallBrain != null)
        {
            smallBrain.InterruptAndClearGoal();
        }

        if (actuator != null)
        {
            actuator.StopAllPhysicalMovement();
        }

        isThinking = false;
        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] ⚠️ 外部重置信号触发");
    }
}