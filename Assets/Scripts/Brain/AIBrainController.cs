using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using EmbodiedAI.DTO;

[RequireComponent(typeof(NPCAttributes))]
[RequireComponent(typeof(PerceptionRadar))]
[RequireComponent(typeof(PromptManager))]
[RequireComponent(typeof(GeminiHttpClient))]
[RequireComponent(typeof(CharacterActuator))]
public class AIBrainController : MonoBehaviour
{
    [Header("UI 绑定")]
    public UnityEngine.UI.Text monologueDisplay;
    public UnityEngine.UI.Text actionDisplay;

    private NPCAttributes attributes;
    private PerceptionRadar radar;
    private PromptManager promptManager;
    private GeminiHttpClient httpClient;
    private CharacterActuator actuator;

    private bool isThinking = false;

    void Awake()
    {
        attributes = GetComponent<NPCAttributes>();
        radar = GetComponent<PerceptionRadar>();
        promptManager = GetComponent<PromptManager>();
        httpClient = GetComponent<GeminiHttpClient>();
        actuator = GetComponent<CharacterActuator>();
    }

    void Start()
    {
        if (TimeManager.Instance != null)
        {
            // 取消原来的每30分钟事件，改用新的20秒AI事件
            TimeManager.Instance.OnAITick += OnPhysicsBrainTick;
        }

        StartCoroutine(DelayedFirstTick());
    }

    // 🌟 追加这个小辅助协程，给物理环境、外部 Key 的 IO 读取留出绝对安全的就绪时间
    private IEnumerator DelayedFirstTick()
    {
        yield return new WaitForSeconds(0.2f);
        Debug.Log("<color=#00FFCC>[大脑初始化] ⏰ 基础组件与物理时钟已完全就绪，发起开局第一帧自主思考！</color>");
        OnPhysicsBrainTick();
    }

    private void OnPhysicsBrainTick()
    {
        if (isThinking) return;
        StartCoroutine(ThinkPhysicsRoutine());
    }

    // AIBrainController.cs 内部片段修正
    private IEnumerator ThinkPhysicsRoutine()
    {
        isThinking = true;

        string timeStr = TimeManager.Instance != null ? TimeManager.Instance.GetCurrentTimeString() : "00:00";
        string serializedRadarData = radar.ScanEnvironmentToSemanticJson();
        // 🌟【新增提取】先去物理执行器问一下现在手里到底抓没抓东西
        string heldItemName = "无（手里没有任何武器或道具，手无寸铁）";
        if (actuator != null && actuator.CurrentGrabbedObject != null)
        {
            heldItemName = actuator.CurrentGrabbedObject.name; // 此时拿到的就是类似 "Stick" 的实体名字
        }

        // 🌟【修改传参】把刚才提取到的 heldItemName 传给末尾参数
        string fullPrompt = promptManager.GeneratePhysicsEnginePrompt(
            attributes.satiety,
            attributes.personality,
            timeStr,
            serializedRadarData,
            heldItemName // 🌟 填在这里
        );

        // 1. 调用纯净的通信客户端，此时回调拿到的是纯文本
        // 确认调用时传入了 3 个参数：1.Prompt字符串, 2.成功回调Lambda, 3.失败回调Lambda
        httpClient.PostPrompt(fullPrompt, (aiRawText) =>
        {
            // 成功回调逻辑（保持不动）
            AIPhysicsDecision npcDecision = ParseBrainResponse(aiRawText);
            if (npcDecision != null && npcDecision.primitive_commands != null)
            {
                if (monologueDisplay) monologueDisplay.text = "AI物理直觉：" + npcDecision.monologue;
                actuator.ExecutePrimitiveSequence(npcDecision.primitive_commands, actionDisplay);
            }
        },
        () =>
        {
            // 🌟 失败回调逻辑
            Debug.LogWarning("<color=orange>[大脑防死锁] ⚠️ 发现大模型请求失败，自动重置思考锁。</color>");
            isThinking = false;
        });

        // 🌟 改为直接单步跳出协程即可：
        yield break;
    }

    // 专门负责业务逻辑解析的私有方法
    private AIPhysicsDecision ParseBrainResponse(string aiRawText)
    {
        try
        {
            // 正则提取清洗完整的 JSON 块
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
                aiRawText, @"{.*}", System.Text.RegularExpressions.RegexOptions.Singleline
            );

            if (match.Success)
            {
                return JsonUtility.FromJson<AIPhysicsDecision>(match.Value);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[大脑业务解析错误] 物理原语序列 JSON 反序列化破产: {e.Message}");
        }
        return null;
    }

    public void ResetBrainState()
    {
        isThinking = false;
        if (actuator) actuator.StopAllPhysicalMovement();
    }

    void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnAITick -= OnPhysicsBrainTick;
        }
    }
}