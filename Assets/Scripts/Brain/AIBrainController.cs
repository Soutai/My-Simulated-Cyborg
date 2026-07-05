using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using EmbodiedAI.DTO;

// 🌟 【强力躯干护栏】：强制要求 NPC 身上必须同时带有这六个组件，缺一不可，Unity 会在挂载时自动补齐
[RequireComponent(typeof(CharacterActuator))]
[RequireComponent(typeof(LocalMotorController))]
[RequireComponent(typeof(NPCAttributes))]
[RequireComponent(typeof(PerceptionRadar))]
[RequireComponent(typeof(PromptManager))]
[RequireComponent(typeof(GeminiHttpClient))]
public class AIBrainController : MonoBehaviour
{
    [Header("🖥️ UI 元素 (只有 UI 需要手动拖拽)")]
    public UnityEngine.UI.Text monologueDisplay;
    public UnityEngine.UI.Text actionDisplay;

    // 🌟 核心外设与躯干全部私有化，彻底隐藏！面板上再也不会有任何 None 空框！
    private NPCAttributes attributes;
    private PerceptionRadar radar;
    private PromptManager promptManager;
    private GeminiHttpClient httpClient;
    private CharacterActuator actuator;
    private LocalMotorController smallBrain;

    private bool isThinking = false;
    private string currentGoal = "无";
    private string currentInterruptAnchor = "None";

    private float lastDangerDensity = 0f;
    [Header("🧠 小脑本能中断阈值")]
    public float dangerThreshold = 2.5f;

    // 🌟 复用缓冲区，避免每个物理帧的 OverlapSphere 产生 GC 分配
    private readonly Collider[] dangerOverlapBuffer = new Collider[32];

    // AIBrainController.cs 追加对外接口
    public string CurrentInterruptAnchor => currentInterruptAnchor;

    void Awake()
    {
        // 🌟 在本地一口气自动抓取全部挂在自己身上的组件，零性能开销，100% 自动化
        attributes = GetComponent<NPCAttributes>();
        radar = GetComponent<PerceptionRadar>();
        promptManager = GetComponent<PromptManager>();
        httpClient = GetComponent<GeminiHttpClient>();
        actuator = GetComponent<CharacterActuator>();
        smallBrain = GetComponent<LocalMotorController>();

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
        yield return new WaitForSeconds(1.0f);
        OnPhysicsBrainTick();
    }

    void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnAITick -= OnPhysicsBrainTick;

        if (actuator != null)
            actuator.OnGrabSuccess -= HandleGrabSuccess;
    }

    void FixedUpdate()
    {
        // 每帧动态计算小脑雷达感知范围内的环境危险密度
        float currentDangerDensity = CalculateEnvironmentalDangerDensity();

        // 计算关于时间的导数（突变率）
        float dDanger_dt = (currentDangerDensity - lastDangerDensity) / Time.fixedDeltaTime;
        lastDangerDensity = currentDangerDensity;

        // 【本能中断判定】：如果危险突变率超过阈值，说明发生了瞬时剧变
        if (dDanger_dt > dangerThreshold)
        {
            Debug.LogError($"<color=red>🚨 [小脑物理本能爆发] 检测到环境危险熵突变率 {dDanger_dt:F2} 超过安全阈值 {dangerThreshold}！触发本能中断！</color>");

            // 1. 瞬间踩死物理刹车，强行格式化双缓冲待办清单
            InterruptAndClearGoal();

            // 2. 零延迟强制大脑立刻联网对剧变进行思考
            RequestImmediateThink();
        }
    }

    private float CalculateEnvironmentalDangerDensity()
    {
        if (radar == null) return 0f;

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, radar.perceptionRadius, dangerOverlapBuffer);
        float totalDanger = 0f;

        for (int i = 0; i < hitCount; i++)
        {
            var col = dangerOverlapBuffer[i];
            if (col.gameObject == this.gameObject) continue;

            SemanticObject semanticObj = col.GetComponent<SemanticObject>();
            Rigidbody targetRb = col.GetComponent<Rigidbody>();

            if (semanticObj != null)
            {
                float omega = 0f;
                if (semanticObj.semanticType == SemanticType.Enemy) omega = 2.5f;     // 狼高危
                else if (semanticObj.semanticType == SemanticType.Weapon) omega = 0.1f;

                // 获取物体的绝对运动速度
                Vector3 velocityVec = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
                float velocity = velocityVec.magnitude;

                // 🌟【新增保护护栏】：如果物体有速度，判断它是在接近我还是远离我
                if (velocity > 0.1f)
                {
                    Vector3 toMe = (transform.position - col.transform.position).normalized;
                    // 计算物体运动方向与“朝向我”方向的点积
                    float dot = Vector3.Dot(velocityVec.normalized, toMe);

                    // 如果 dot <= 0，说明物体的运动方向正在背离我（被打飞或逃跑），威胁度归零！
                    if (dot <= 0f) continue;
                }

                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance < 0.5f) distance = 0.5f;

                totalDanger += (velocity * omega) / (distance * distance);
            }
        }

        return totalDanger;
    }

    private void OnPhysicsBrainTick()
    {
        if (isThinking) return;

        string radarJson = radar.ScanEnvironmentToSemanticJson();
        float currentSatiety = attributes != null ? attributes.satiety : 100f;
        string personality = attributes != null ? attributes.personality : "DEFAULT";
        string timeStr = TimeManager.Instance != null ? TimeManager.Instance.GetCurrentTimeString() : "00:00";

        string finalPrompt = promptManager.GeneratePhysicsEnginePrompt(
            currentSatiety,
            personality,
            timeStr,
            radarJson,
            actuator.LeftHandObject ? actuator.LeftHandObject.name : "",
            actuator.RightHandObject ? actuator.RightHandObject.name : "",
            currentGoal
        );

        isThinking = true;
        if (monologueDisplay != null) monologueDisplay.text = "思考中...";

        httpClient.PostPrompt(finalPrompt, OnBrainResponseReceived, OnAIRequestFailed);
    }

    // AIBrainController.cs 约第 155 行左右
    private void OnBrainResponseReceived(string rawText)
    {
        AIPhysicsDecision decision = ParseBrainResponse(rawText);
        if (decision == null)
        {
            isThinking = false;
            return;
        }

        if (monologueDisplay != null) monologueDisplay.text = decision.monologue;

        currentGoal = decision.goal;

        // 🌟【需要补上这一行】：将大模型生成的动态锚点赋予本地变量，供小脑读取！
        currentInterruptAnchor = !string.IsNullOrEmpty(decision.interrupt_anchor_type) ? decision.interrupt_anchor_type : "None";

        // 🌟 纯净流：送入小脑双缓冲
        if (smallBrain != null && decision.plan_steps != null && decision.plan_steps.Count > 0)
        {
            smallBrain.ReceiveBrainPlan(decision.plan_steps, currentGoal);
        }

        isThinking = false;
        Debug.Log($"<color=lime>[大脑] 🎯 决策完成 → 当前目标: 【{currentGoal}】</color>");
    }

    private void HandleGrabSuccess(GameObject grabbedObj, string hand)
    {
        Debug.Log($"<color=lime>[大脑] 🎉 抓取成功 → {grabbedObj.name}</color>");
    }

    private void OnAIRequestFailed()
    {
        Debug.Log($"[{GetCurrentTimestamp()}] [大脑] ❌ 请求失败");
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
        currentInterruptAnchor = "None"; // 🌟【需要补上这一行】：打断时同步复位锚点

        if (smallBrain != null)
        {
            smallBrain.InterruptAndClear();
        }
        if (actuator != null)
        {
            actuator.StopAllPhysicalMovement();
        }
    }
}