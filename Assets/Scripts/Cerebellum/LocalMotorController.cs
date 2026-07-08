
using UnityEngine;
using System.Collections.Generic;
using EmbodiedAI.DTO;

[RequireComponent(typeof(CharacterActuator))]
[RequireComponent(typeof(AIBrainController))]
[RequireComponent(typeof(WanderReflex))]
public class LocalMotorController : MonoBehaviour
{
    private CharacterActuator actuator;
    private AIBrainController brain;
    private PerceptionRadar radar;
    private InstinctReflex instinctReflex;
    private WanderReflex wanderReflex;

    private bool isBusy = false;

    // 🌟 供 InstinctReflex 判断"身体现在有没有在执行大脑下发的计划"
    public bool IsBusy => isBusy;

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
        instinctReflex = GetComponent<InstinctReflex>();
        wanderReflex = GetComponent<WanderReflex>();

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
                Debug.Log("<color=#00FFFF>[小脑感官唤醒] 🔔 叫醒大脑重新决策——不是危险，先不打断身体正在进行的漫步。</color>");

                // 🌟 软性打断：只是发现了值得重新思考的东西，不是紧急情况，不强行停下身体——
                // 身体会继续漫步，直到大脑真的给出新指令为止，避免网络请求这段真实延迟期间站着发呆。
                // brain.InterruptAndClearGoal() 内部会调用 smallBrain.InterruptAndClear()，这里不需要重复调用
                brain.InterruptAndClearGoal(hardStopMovement: false);

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

        // 🌟 本能反射接管期间，身体不算"空闲"——哪怕 isBusy 恰好是 false（比如反射刚抢完控制权），
        // 新计划也只能乖乖排到后台缓冲区，等本能解除后才被接续执行，不能反过来打断本能。
        bool occupiedByInstinct = instinctReflex != null && instinctReflex.IsFleeing;

        // 🌟 纯净双缓冲分流
        if (!isBusy && !occupiedByInstinct)
        {
            Debug.Log($"<color=cyan>[小脑] 🟢 身体空闲，拉起前台缓冲区直接执行。目标: {brainGoal}</color>");
            frontBuffer = incomingCommands;
            ExecuteFrontBuffer();
        }
        else
        {
            string reason = occupiedByInstinct ? "身体正被本能反射占用" : "身体正忙";
            Debug.Log($"<color=orange>[小脑] 💾 {reason}，新指令无缝锁入后台缓冲区(Back Buffer)。目标: {brainGoal}</color>");
            backBuffer = incomingCommands;
        }
    }

    private void ExecuteFrontBuffer()
    {
        if (frontBuffer.Count == 0) return;

        isBusy = true;

        // 🌟 EXPLORE 是开放式的本地漫步，不是一个"做完就结束"的定长原语，不走 CharacterActuator
        // 的原语序列——交给 WanderReflex 持续接管，直到锚点唤醒或本能反射把控制权抢回去为止。
        if (frontBuffer.Count == 1 && IsExploreStep(frontBuffer[0]))
        {
            wanderReflex.BeginWandering();
            return;
        }

        // 🌟 只有真正切换到具体原语（APPROACH/GRAB 等）时才停止漫步——软性打断（锚点唤醒）之后
        // 身体可能还在继续漫步，这里才是真正需要"接管身体"的那一刻，不能更早停，否则又会制造空窗期
        wanderReflex.StopWandering();
        actuator.ExecutePrimitiveSequence(frontBuffer, null);
    }

    private static bool IsExploreStep(PlanStep step)
    {
        return step != null && string.Equals(step.arrival_op?.Trim(), "EXPLORE", System.StringComparison.OrdinalIgnoreCase);
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
            Debug.Log("[小脑] 💤 所有计划执行完毕，没有排队的后续计划，立刻叫醒大脑决定下一步。");
            // 🌟 补上第三个"发呆缺口"：本能脱险、锚点软性打断都已经会主动叫醒大脑，
            // 唯独"普通任务正常做完"这条路径漏了——之前只是默默进入静默观察，干等最长 20 秒的
            // 常规思考定时器。大多数计划（APPROACH+GRAB 这类）本来就是几秒钟的短任务，
            // 跑完了没有理由继续发呆，应该立刻让大脑决定接下来干什么。
            brain?.RequestImmediateThink();
        }
    }

    /// <summary>
    /// 🌟 本能反射解除接管后调用：如果大脑在反射占用身体期间攒了新计划（锁在后台缓冲区），
    /// 立刻无缝接续执行；没有的话什么也不做，交由调用方（InstinctReflex）决定要不要叫醒大脑。
    /// 返回值告诉调用方"有没有真的接续上一个计划"，没有的话身体现在是彻底空闲的。
    /// </summary>
    public bool TryResumeFromBackBuffer()
    {
        if (isBusy) return true; // 正常不该发生，双保险——既然已经忙起来了，就当作"已经有人接管"处理
        if (backBuffer.Count > 0)
        {
            Debug.Log("<color=lime>[小脑] ⚡ 本能反射已解除，接续执行期间攒下的后台缓冲区(Back Buffer)！</color>");
            frontBuffer = new List<PlanStep>(backBuffer);
            backBuffer.Clear();
            ExecuteFrontBuffer();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 💥 中断接口。hardStopMovement=true（默认，危险/死亡用）会连漫步一起硬停；
    /// hardStopMovement=false（锚点唤醒这种非紧急重新思考用）只清空计划缓冲区、
    /// 不打断身体正在进行的漫步——避免"发个网络请求就先僵住干等"的空窗期。
    /// </summary>
    public void InterruptAndClear(bool hardStopMovement = true)
    {
        Debug.LogWarning(hardStopMovement
            ? "[小脑] 💥 收到本能急停指令！格式化所有前后台缓冲区，强制踩死物理刹车！"
            : "[小脑] 🔔 收到软性重新思考指令，清空计划缓冲区，但保留身体正在进行的漫步。");
        frontBuffer.Clear();
        backBuffer.Clear();
        isBusy = false;

        if (hardStopMovement)
        {
            suppressNextFinishLog = true; // 紧接着触发的 actuator.StopAllPhysicalMovement 会连带产生一次多余回调
            wanderReflex?.StopWandering(); // EXPLORE 不经过 actuator 的原语序列，StopAllPhysicalMovement 管不到它，这里单独喊停
        }
    }
}