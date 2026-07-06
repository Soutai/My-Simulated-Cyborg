
using UnityEngine;
using System.Collections.Generic;
using EmbodiedAI.DTO;

[RequireComponent(typeof(CharacterActuator))]
[RequireComponent(typeof(AIBrainController))]
public class LocalMotorController : MonoBehaviour
{
    private CharacterActuator actuator;
    private AIBrainController brain;
    private PerceptionRadar radar;

    private bool isBusy = false;

    // 🌟 核心双缓冲队列
    private List<PlanStep> frontBuffer = new List<PlanStep>();
    private List<PlanStep> backBuffer = new List<PlanStep>();

    // 🌟 复用缓冲区，避免每帧 OverlapSphere 产生 GC 分配
    private readonly Collider[] anchorOverlapBuffer = new Collider[32];

    // 🌟 中断流程里 actuator.StopAllPhysicalMovement() 会连带触发 OnSequenceFinished，
    // 用这个标志位吞掉那次回调，避免打断被误打印成"任务正常完成"
    private bool suppressNextFinishLog = false;

    void Awake()
    {
        actuator = GetComponent<CharacterActuator>();
        brain = GetComponent<AIBrainController>();
        radar = GetComponent<PerceptionRadar>();

        if (actuator != null)
        {
            // 监听执行器物理连招完工通知
            actuator.OnSequenceFinished += OnCurrentSequenceFinished;
        }
    }

    void OnDestroy()
    {
        if (actuator != null)
            actuator.OnSequenceFinished -= OnCurrentSequenceFinished;
    }

    // LocalMotorController.cs 内部追加

    void Update()
    {
        // 🌟【核心战略锚定拦截器】：完全泛化，不带任何具体物体或硬编码
        if (isBusy && brain != null && brain.CurrentInterruptAnchor.HasValue)
        {
            SemanticType anchor = brain.CurrentInterruptAnchor.Value;

            // 借助雷达检测当前感知列表内是否有任何物体的语义类型与大脑下发的锚点一致
            if (CheckRadarForAnchor(anchor))
            {
                Debug.Log($"<color=#00FFFF>[小脑感官唤醒] 👁️ 警报！雷达扫描到与长任务终极锚点一致的类型【{anchor}】！</color>");
                Debug.Log("<color=#00FFFF>[小脑感官唤醒] 🛑 掐断剩余全部无意义走格子步骤，强制交还大脑决策。</color>");

                // brain.InterruptAndClearGoal() 内部会调用 smallBrain.InterruptAndClear()，这里不需要重复调用
                brain.InterruptAndClearGoal();

                // 立刻强制大脑联网重新思考。此时雷达数据里已经有目标了，大脑会下达精准单步连招
                brain.RequestImmediateThink();
            }
        }
    }

    /// <summary>
    /// 💡 纯语义的雷达数据比对函数（完全复用且通用，不掺杂任何特定逻辑）
    /// </summary>
    private bool CheckRadarForAnchor(SemanticType anchorType)
    {
        if (radar == null) return false;

        // 🌟 利用 Unity 的 Physics 探测当前感知半径内的物体（复用缓冲区，避免 GC 分配）
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, radar.perceptionRadius, anchorOverlapBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            var col = anchorOverlapBuffer[i];
            if (col.gameObject == this.gameObject) continue;

            // 获取物体的通用语义标签组件，直接按枚举比对，不再走字符串
            SemanticObject semanticObj = col.GetComponent<SemanticObject>();
            if (semanticObj != null && semanticObj.semanticType == anchorType)
            {
                return true; // 只要雷达里进来了任何一个对得上号的类型，判定成功
            }
        }
        return false;
    }

    /// <summary>
    /// 🧠 大脑唯一调用的连招计划接入口
    /// </summary>
    public void ReceiveBrainPlan(List<PlanStep> planSteps, string brainGoal)
    {
        if (planSteps == null || planSteps.Count == 0) return;

        // 🌟 PlanStep 本身就是执行器认识的命令结构，不再需要额外转换成 PrimitiveCommand；
        // 这里拷贝一份新列表，只是为了让缓冲区跟大模型原始返回的列表解耦
        List<PlanStep> incomingCommands = new List<PlanStep>(planSteps);

        // 🌟 纯净双缓冲分流
        if (!isBusy)
        {
            Debug.Log($"<color=cyan>[小脑] 🟢 身体空闲，拉起前台缓冲区直接执行。目标: {brainGoal}</color>");
            frontBuffer = incomingCommands;
            ExecuteFrontBuffer();
        }
        else
        {
            Debug.Log($"<color=orange>[小脑] 💾 身体正忙，新指令无缝锁入后台缓冲区(Back Buffer)。目标: {brainGoal}</color>");
            backBuffer = incomingCommands;
        }
    }

    private void ExecuteFrontBuffer()
    {
        if (frontBuffer.Count == 0) return;

        isBusy = true;
        actuator.ExecutePrimitiveSequence(frontBuffer, null);
    }

    private void OnCurrentSequenceFinished()
    {
        isBusy = false;
        frontBuffer.Clear();

        if (suppressNextFinishLog)
        {
            // 这次回调是中断流程里 StopAllPhysicalMovement 带来的副作用，不是任务正常完成，跳过
            suppressNextFinishLog = false;
            return;
        }

        // 检查后台是否有大脑未雨绸缪送来的缓冲计划
        if (backBuffer.Count > 0)
        {
            Debug.Log($"<color=lime>[小脑] ⚡ 零延迟无缝切换！将后台缓冲区(Back Buffer)激活推进！</color>");
            frontBuffer = new List<PlanStep>(backBuffer);
            backBuffer.Clear();

            ExecuteFrontBuffer();
        }
        else
        {
            Debug.Log("[小脑] 💤 所有计划执行完毕，智能体进入静默观察状态。");
        }
    }

    /// <summary>
    /// 💥 本能中断急停接口
    /// </summary>
    public void InterruptAndClear()
    {
        Debug.LogWarning("[小脑] 💥 收到本能急停指令！格式化所有前后台缓冲区，强制踩死物理刹车！");
        frontBuffer.Clear();
        backBuffer.Clear();
        isBusy = false;
        suppressNextFinishLog = true; // 紧接着触发的 actuator.StopAllPhysicalMovement 会连带产生一次多余回调
    }
}