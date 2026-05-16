using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    private bool isNetworkRequestFlying = false; // 新增屏障，防止网络卡顿造成思考周期重叠

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
        // 🌟【新追加的生态自转功能】：让大脑订阅来自时钟的整点时钟广播
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnGameHourPassed += OnGameHourTriggered;
        }

        // 开局主动进行第一小时的生存盘算
        OnDecideAction();
    }

    void OnDestroy()
    {
        // 记得解绑，养成优秀的防内存泄漏习惯
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnGameHourPassed -= OnGameHourTriggered;
        }
    }

    private void OnGameHourTriggered()
    {
        Debug.Log($"<color=yellow>[时间轴广播] ⏰ 生态时钟敲响整点: {TimeManager.Instance.GetCurrentTimeString()}，NPC尝试自主推演！</color>");
        OnDecideAction();
    }

    // 辅助方法：利用修改后的安全反射，强行清空 Unity 控制台日志
    void ClearUnityConsole()
    {
#if UNITY_EDITOR
        var assembly = System.Reflection.Assembly.GetAssembly(typeof(UnityEditor.EditorWindow));
        var type = assembly.GetType("UnityEditor.LogEntries");
        var method = type.GetMethod("Clear");
        method.Invoke(new object(), null);
#endif
    }

    public void OnDecideAction()
    {
        if (isNetworkRequestFlying) return; // 如果上一小时的Gemini还没响应完，不重叠生成新垃圾请求
        ClearUnityConsole();
        StartCoroutine(ThinkLifecycle());
    }

    private IEnumerator ThinkLifecycle()
    {
        isNetworkRequestFlying = true;
        if (monologueDisplay) monologueDisplay.text = "正在盘算当下的生存策略...";

        // 🟢 [1. 神经网络启动]
        Debug.Log("<color=#FFA500>[1. 神经网络启动] 🧠 原始人AI开始进行环境逻辑推演...</color>");

        // 1. 从雷达组件获取纯净的物理环境扫描数据
        string environmentDescription = radar.ScanEnvironment();

        List<string> allowedActions = new List<string> { "IDLE" };
        if (radar.HasFoodInSight) allowedActions.Add("MOVE_TO_FOOD");
        if (radar.HasWeaponInSight) allowedActions.Add("PICKUP_WEAPON");
        if (radar.HasEnemyInSight) allowedActions.Add("EVADE_ENEMY");
        string actionsJsonString = "[" + string.Join(", ", allowedActions.ToArray()) + "]";

        // 动态抓取当前世界时间
        string currentTimeStr = TimeManager.Instance != null ? TimeManager.Instance.GetCurrentTimeString() : "08:00";

        // 2. 将饱食度、性格属性和新追加的世界时间完美传递给剧本矩阵
        string finalPrompt = promptManager.GenerateNpcPrompt(
            attributes.satiety,     // 👈 饱食度
            attributes.personality,
            currentTimeStr,         // 👈 时间
            environmentDescription,
            actionsJsonString,
            radar.HasFoodInSight,
            radar.HasWeaponInSight,
            radar.HasEnemyInSight
        );

        // 3. 送入纯净的网络客户端发送
        yield return StartCoroutine(httpClient.SendPostRequest(finalPrompt, (npcDecision) =>
        {
            // 🟢 [8. 解析成功] 原始业务日志一条不丢
            Debug.Log($"<color=#00FF00>[8. 解析成功] AI 决策结果</color>\n-> 独白(monologue): {npcDecision.monologue}\n-> 动作(action): {npcDecision.action}");

            // 4. 驱动身体与UI
            if (monologueDisplay) monologueDisplay.text = "内心独白：" + npcDecision.monologue;
            actuator.ExecuteAction(npcDecision.action, radar.HasFoodInSight, radar.HasWeaponInSight, radar.HasEnemyInSight, actionDisplay);
        }));

        isNetworkRequestFlying = false;
    }

    // 🌟 最小追加：给 Reset 调用的特殊后门，确保重置时网络锁被安全拧开
    public void ResetBrainState()
    {
        isNetworkRequestFlying = false;
        if (monologueDisplay) monologueDisplay.text = "仿真环境已重置。等待观察...";
        if (actionDisplay) actionDisplay.text = "状态：待机";
    }
}