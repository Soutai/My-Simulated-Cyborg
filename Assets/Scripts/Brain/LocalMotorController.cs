using UnityEngine;
using System.Collections.Generic;
using EmbodiedAI.DTO;

[RequireComponent(typeof(CharacterActuator))]
[RequireComponent(typeof(AIBrainController))]
public class LocalMotorController : MonoBehaviour
{
    private CharacterActuator actuator;
    private AIBrainController brain;

    private bool isBusy = false;

    // 🌟 核心双缓冲队列
    private List<PrimitiveCommand> frontBuffer = new List<PrimitiveCommand>();
    private List<PrimitiveCommand> backBuffer = new List<PrimitiveCommand>();

    private string savedBackBufferGoal = "无";

    void Awake()
    {
        actuator = GetComponent<CharacterActuator>();
        brain = GetComponent<AIBrainController>();

        if (actuator != null)
        {
            // 监听执行器物理连招完工通知
            actuator.OnSequenceFinished += OnCurrentSequenceFinished;
        }
    }

    // LocalMotorController.cs 内部追加

    void Update()
    {
        // 🌟【核心战略锚定拦截器】：完全泛化，不带任何具体物体或硬编码
        if (isBusy && brain != null && brain.CurrentInterruptAnchor != "None")
        {
            // 借助雷达检测当前感知列表内是否有任何物体的语义类型与大脑下发的锚点字符串一致
            if (CheckRadarForAnchor(brain.CurrentInterruptAnchor))
            {
                Debug.Log($"<color=#00FFFF>[小脑感官唤醒] 👁️ 警报！雷达扫描到与长任务终极锚点一致的类型【{brain.CurrentInterruptAnchor}】！</color>");
                Debug.Log("<color=#00FFFF>[小脑感官唤醒] 🛑 掐断剩余全部无意义走格子步骤，强制交还大脑决策。</color>");

                // 1. 本地清空所有前后台缓冲区，让小脑和执行器停下来
                InterruptAndClear();

                // 2. 强行把当前宏观战略彻底格式化，复位大脑目标
                brain.InterruptAndClearGoal();

                // 3. 立刻强制大脑联网重新思考。此时雷达数据里已经有目标了，大脑会下达精准单步连招
                brain.RequestImmediateThink();
            }
        }
    }

    /// <summary>
    /// 💡 纯语义的雷达数据比对函数（完全复用且通用，不掺杂任何特定逻辑）
    /// </summary>
    private bool CheckRadarForAnchor(string anchorType)
    {
        // 拿到雷达组件
        PerceptionRadar radarComponent = GetComponent<PerceptionRadar>();
        if (radarComponent == null) return false;

        // 🌟 利用 Unity 的 Physics 探测当前感知半径内的物体
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, radarComponent.perceptionRadius);
        foreach (var col in hitColliders)
        {
            if (col.gameObject == this.gameObject) continue;

            // 获取物体的通用语义标签组件
            SemanticObject semanticObj = col.GetComponent<SemanticObject>();
            if (semanticObj != null)
            {
                // 将物体的 SemanticType 枚举转换为字符串进行通用比对 (例如：Food, Enemy, Weapon)
                if (semanticObj.semanticType.ToString().ToUpper().Trim() == anchorType.ToUpper().Trim())
                {
                    return true; // 只要雷达里进来了任何一个对得上号的类型，判定成功
                }
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

        // 解析为底层物理原语
        List<PrimitiveCommand> incomingCommands = new List<PrimitiveCommand>();
        foreach (var step in planSteps)
        {
            incomingCommands.Add(new PrimitiveCommand
            {
                op = step.arrival_op ?? "APPLY_FORCE",
                hand = step.hand,
                target_id = step.target_id,
                arg_x = step.arg_x,
                arg_z = step.arg_z,
                strength = step.strength
            });
        }

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
            savedBackBufferGoal = brainGoal;
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

        // 检查后台是否有大脑未雨绸缪送来的缓冲计划
        if (backBuffer.Count > 0)
        {
            Debug.Log($"<color=lime>[小脑] ⚡ 零延迟无缝切换！将后台缓冲区(Back Buffer)激活推进！</color>");
            frontBuffer = new List<PrimitiveCommand>(backBuffer);
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
        savedBackBufferGoal = "无";
        isBusy = false;
    }
}