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
            // 改用新的20秒AI事件
            TimeManager.Instance.OnAITick += OnPhysicsBrainTick;
        }

        StartCoroutine(DelayedFirstTick());
    }

    // 🌟 追加这个小辅助协程，给物理环境、外部 Key 的 IO 读取留出绝对安全的就绪时间
    private IEnumerator DelayedFirstTick()
    {
        yield return new WaitForSeconds(0.2f);
        OnPhysicsBrainTick();
    }

    private void OnPhysicsBrainTick()
    {
        if (isThinking) return;
        StartCoroutine(ThinkPhysicsRoutine());
    }

    private IEnumerator ThinkPhysicsRoutine()
    {
        if (isThinking)
        {
            Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 正在思考中，跳过本次触发");
            yield break;
        }

        isThinking = true;

        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 🚀 开始新一轮思考...");

        string timeStr = TimeManager.Instance != null ? TimeManager.Instance.GetCurrentTimeString() : "00:00";
        string serializedRadarData = radar.ScanEnvironmentToSemanticJson();
        string heldItemName = "无（手里没有任何武器或道具，手无寸铁）";
        if (actuator != null && actuator.CurrentGrabbedObject != null)
        {
            heldItemName = actuator.CurrentGrabbedObject.name;
        }

        string fullPrompt = promptManager.GeneratePhysicsEnginePrompt(
            attributes.satiety, attributes.personality, timeStr, serializedRadarData, heldItemName);

        // ==================== 新增：打印完整 Prompt ====================
        Debug.Log($"<color=yellow>[{GetCurrentTimestamp()}] [大脑] 📤【发送给AI的完整Prompt】</color>\n{fullPrompt}\n");

        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 📤 发送Prompt给Gemini（等待AI回复）");

        // 发送请求
        httpClient.PostPrompt(fullPrompt, (aiRawText) =>
        {
            // ==================== 新增：打印AI原始回复 ====================
            Debug.Log($"<color=cyan>[{GetCurrentTimestamp()}] [大脑] 📥【AI原始回复内容】</color>\n{aiRawText}\n");

            AIPhysicsDecision npcDecision = ParseBrainResponse(aiRawText);
            if (npcDecision != null)
            {
                Debug.Log($"[{GetCurrentTimestamp()}] [大脑] ✅ 解析成功，Monologue: {npcDecision.monologue}");

                if (npcDecision.primitive_commands != null && npcDecision.primitive_commands.Count > 0)
                {
                    Debug.Log($"[{GetCurrentTimestamp()}] [大脑] 🎯 解析到 {npcDecision.primitive_commands.Count} 个物理原语：");

                    for (int i = 0; i < npcDecision.primitive_commands.Count; i++)
                    {
                        var cmd = npcDecision.primitive_commands[i];
                        Debug.Log($"   [{i + 1}] op = {cmd.op} | target_id = {cmd.target_id} | arg_x = {cmd.arg_x} | arg_z = {cmd.arg_z}");
                    }

                    if (monologueDisplay) monologueDisplay.text = "AI物理直觉：" + npcDecision.monologue;
                    actuator.ExecutePrimitiveSequence(npcDecision.primitive_commands, actionDisplay);
                }
                else
                {
                    Debug.LogWarning($"[{GetCurrentTimestamp()}] [大脑] ⚠️ 解析成功，但没有可执行的原语");
                }
            }
            else
            {
                Debug.LogWarning($"[{GetCurrentTimestamp()}] [大脑] ⚠️ 无法解析AI回复");
            }

            isThinking = false;
        },
        () =>
        {
            Debug.LogWarning($"[{GetCurrentTimestamp()}] [大脑] ❌ 请求失败，解锁思考状态");
            isThinking = false;
        });

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

    // 新增辅助方法（放在类最后）
    private string GetCurrentTimestamp()
    {
        return System.DateTime.Now.ToString("HH:mm:ss.fff");
    }
}