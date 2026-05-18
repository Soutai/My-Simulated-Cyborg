using UnityEngine;
using System.Collections.Generic;
using EmbodiedAI.DTO;

[RequireComponent(typeof(CharacterActuator))]
[RequireComponent(typeof(PerceptionRadar))]
public class LocalMotorController : MonoBehaviour
{
    [Header("小脑参数")]
    public float tickInterval = 0.2f;
    public float approachDistance = 1.5f;

    private CharacterActuator actuator;
    private PerceptionRadar radar;
    private AIBrainController brain;

    private string currentGoal = "无";
    private string currentGoalTargetId = "";
    private PrimitiveCommand arrivalCommand = null;

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

    public void SetNewGoal(string goalDescription, string targetId, PrimitiveCommand onArrivalCmd)
    {
        currentGoal = goalDescription;
        currentGoalTargetId = targetId;
        arrivalCommand = onArrivalCmd;

        Debug.Log($"<color=orange>[小脑] 🎯 接受托管目标: 【{goalDescription}】 | 目标物体: {targetId} | 到达后动作: {(onArrivalCmd != null ? onArrivalCmd.op : "无")}</color>");
    }

    private void TickSmallBrain()
    {
        if (string.IsNullOrEmpty(currentGoalTargetId)) return;

        GameObject targetObj = GameObject.Find(currentGoalTargetId);
        if (targetObj == null) return;

        float dist = Vector3.Distance(transform.position, targetObj.transform.position);

        if (dist > approachDistance)
        {
            MoveTowardsTarget(targetObj.transform.position);
        }
        else
        {
            Debug.Log($"<color=green>[小脑] ✨ 已顺利护送肉身抵达目标 【{currentGoalTargetId}】 周边 {dist:F2} 米处！</color>");

            if (actuator != null)
                actuator.StopAllPhysicalMovement();

            if (arrivalCommand != null && !string.IsNullOrEmpty(arrivalCommand.op))
            {
                Debug.Log($"<color=yellow>[小脑] ⚡ 正在代为释放大脑托管的原语动作: {arrivalCommand.op}</color>");
                List<PrimitiveCommand> cmds = new List<PrimitiveCommand> { arrivalCommand };
                actuator.ExecutePrimitiveSequence(cmds, null);
            }

            // 清空目标，由 CharacterActuator 的 OnGrabSuccess 事件负责唤醒大脑
            currentGoal = "无";
            currentGoalTargetId = "";
            arrivalCommand = null;
        }
    }

    private void MoveTowardsTarget(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
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
        arrivalCommand = null;
        if (actuator != null) actuator.StopAllPhysicalMovement();
        Debug.Log("<color=red>[小脑] 🛑 中断并清空当前导航目标</color>");
    }
}