using UnityEngine;
using System.Collections.Generic;
using EmbodiedAI.DTO;

[RequireComponent(typeof(CharacterActuator))]
[RequireComponent(typeof(PerceptionRadar))]
public class LocalMotorController : MonoBehaviour
{
    [Header("小脑参数")]
    public float tickInterval = 0.2f;
    public float approachDistance = 1.5f; // 缩短点，让物理原语更准

    private CharacterActuator actuator;
    private PerceptionRadar radar;
    private AIBrainController brain;

    private string currentGoal = "无";
    private string currentGoalTargetId = "";
    private PrimitiveCommand arrivalCommand = null; // 🌟 托管的大脑最终动作

    private float lastTickTime = 0f;

    void Awake()
    {
        actuator = GetComponent<CharacterActuator>();
        radar = GetComponent<PerceptionRadar>();
        brain = GetComponent<AIBrainController>();
    }

    void Update()
    {
        if (Time.time - lastTickTime < tickInterval) return;
        lastTickTime = Time.time;

        TickSmallBrain();
    }

    // 🌟 大脑下发任务时，连带到达动作一起托管
    public void SetNewGoal(string goalDescription, string targetId, PrimitiveCommand onArrivalCmd)
    {
        currentGoal = goalDescription;
        currentGoalTargetId = targetId;
        arrivalCommand = onArrivalCmd;

        Debug.Log($"<color=orange>[小脑] 🎯 接受托管目标: 【{goalDescription}】 | 目标物体: {targetId} | 到达后动作: {(onArrivalCmd != null ? onArrivalCmd.op : "无")}</color>");
    }

    private void TickSmallBrain()
    {
        // 如果没有明确的目标物体 ID，小脑不瞎跑，保持物理静止或听从大脑即时指令
        if (string.IsNullOrEmpty(currentGoalTargetId)) return;

        // 纯粹通过唯一 ID 查找场景中的目标实体（完全不关心它的类型是狼还是水果）
        GameObject targetObj = GameObject.Find(currentGoalTargetId);
        if (targetObj == null) return;

        float dist = Vector3.Distance(transform.position, targetObj.transform.position);

        // 状况 A：还没走到 —— 高频、丝滑地施加物理力（物理原语驱动）
        if (dist > approachDistance)
        {
            MoveTowardsTarget(targetObj.transform.position);
        }
        // 状况 B：到了！—— 释放战略意图（临门一脚）
        else
        {
            Debug.Log($"<color=green>[小脑] ✨ 已顺利护送肉身抵达目标 【{currentGoalTargetId}】 周边 {dist:F2} 米处！</color>");

            // 🌟 核心修复（病灶 2）：在释放临门一脚动作前，小脑主动踩死刹车，消除高频残留推力对抓取挂载造成的撕裂和受伤
            if (actuator != null)
            {
                actuator.StopAllPhysicalMovement();
            }

            // 执行大脑托付的临门一脚动作
            if (arrivalCommand != null && !string.IsNullOrEmpty(arrivalCommand.op))
            {
                Debug.Log($"<color=yellow>[小脑] ⚡ 正在代为释放大脑托管的原语动作: {arrivalCommand.op}</color>");
                List<PrimitiveCommand> cmds = new List<PrimitiveCommand> { arrivalCommand };
                actuator.ExecutePrimitiveSequence(cmds, null);
            }

            // 功成身退，清空目标，并立刻叫醒大脑进行下一轮战术推演
            currentGoal = "无";
            currentGoalTargetId = "";
            arrivalCommand = null;

            if (brain != null)
            {
                brain.RequestImmediateThink();
            }
        }
    }

    private void MoveTowardsTarget(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position).normalized;

        // 纯物理驱动：小脑只管计算朝向目标的力
        var commands = new List<PrimitiveCommand>
        {
            new PrimitiveCommand { op = "APPLY_FORCE", arg_x = dir.x * 3.5f, arg_z = dir.z * 3.5f }
        };

        actuator.ExecutePrimitiveSequence(commands, null);
    }

    public void InterruptAndClearGoal()
    {
        currentGoal = "无";
        currentGoalTargetId = "";
        arrivalCommand = null; // 清空托管的临门一脚动作

        // 让物理执行器也立刻刹车停止移动
        if (actuator != null)
        {
            actuator.StopAllPhysicalMovement();
        }

        Debug.Log("<color=red>[小脑] 🛑 收到中断指令，已强行清空当前导航战略与托管动作！</color>");
    }
}