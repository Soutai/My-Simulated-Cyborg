using UnityEngine;
using System.Collections.Generic;
using EmbodiedAI.DTO;

[RequireComponent(typeof(CharacterActuator))]
[RequireComponent(typeof(PerceptionRadar))]
public class LocalMotorController : MonoBehaviour
{
    [Header("小脑参数")]
    public float tickInterval = 0.2f;
    public float approachDistance = 2.5f;

    private CharacterActuator actuator;
    private PerceptionRadar radar;
    private AIBrainController brain;

    private string currentGoal = "无";
    private string currentGoalTargetId = "";

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

    public void SetNewGoal(string goalDescription, string targetId = "")
    {
        currentGoal = goalDescription;
        currentGoalTargetId = targetId;
        Debug.Log($"<color=orange>[小脑] 收到新Goal: {goalDescription} (目标ID: {targetId})</color>");
    }

    private void TickSmallBrain()
    {
        if (string.IsNullOrEmpty(currentGoal) || currentGoal == "无") return;

        // 新增：检查目标是否已经完成
        if (IsGoalAlreadyAchieved())
        {
            Debug.Log($"<color=green>[小脑] Goal 已完成: {currentGoal}</color>");
            currentGoal = "无";
            currentGoalTargetId = "";
            return;
        }

        SemanticObject targetObj = FindBestTargetForGoal();

        if (targetObj != null)
        {
            float distance = Vector3.Distance(transform.position, targetObj.transform.position);

            if (distance > approachDistance)
            {
                MoveTowardsTarget(targetObj.transform.position);
            }
            else
            {
                AutoInteract(targetObj);
            }
        }
        else if (brain != null)
        {
            brain.RequestImmediateThink();
        }
    }

    // 新增：判断当前Goal是否已完成（语义化）
    private bool IsGoalAlreadyAchieved()
    {
        if (actuator == null || actuator.CurrentGrabbedObject == null) return false;

        string heldName = actuator.CurrentGrabbedObject.name.ToLower();
        string goalLower = currentGoal.ToLower();

        if (goalLower.Contains("weapon") || goalLower.Contains("stick"))
            return heldName.Contains("stick");

        if (goalLower.Contains("food") || goalLower.Contains("fruit"))
            return heldName.Contains("fruit");

        return false;
    }

    private SemanticObject FindBestTargetForGoal()
    {
        // 如果已经持有目标，就不需要再找
        if (IsGoalAlreadyAchieved()) return null;

        SemanticObject[] allObjs = FindObjectsOfType<SemanticObject>();
        SemanticObject best = null;
        float minDist = float.MaxValue;

        foreach (var obj in allObjs)
        {
            if (obj == null || obj.gameObject == this.gameObject) continue;

            float dist = Vector3.Distance(transform.position, obj.transform.position);
            if (dist < minDist && IsRelevantToGoal(currentGoal, obj))
            {
                minDist = dist;
                best = obj;
            }
        }
        return best;
    }

    private bool IsRelevantToGoal(string goal, SemanticObject obj)
    {
        if (string.IsNullOrEmpty(goal)) return false;
        string g = goal.ToLower();
        string typeStr = obj.semanticType.ToString().ToLower();

        if (g.Contains("weapon") || g.Contains("stick") || g.Contains("木棍"))
            return typeStr.Contains("weapon");

        if (g.Contains("food") || g.Contains("fruit") || g.Contains("吃"))
            return typeStr.Contains("food");

        if (g.Contains("enemy") || g.Contains("wolf"))
            return typeStr.Contains("enemy");

        return !string.IsNullOrEmpty(currentGoalTargetId) &&
               obj.gameObject.name.Contains(currentGoalTargetId);
    }

    private void MoveTowardsTarget(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        var commands = new List<PrimitiveCommand>
        {
            new PrimitiveCommand { op = "APPLY_FORCE", arg_x = dir.x * 3.2f, arg_z = dir.z * 3.2f }
        };

        actuator.ExecutePrimitiveSequence(commands, null);
    }

    private void AutoInteract(SemanticObject target)
    {
        List<PrimitiveCommand> commands = new List<PrimitiveCommand>();

        if (target.semanticType == SemanticType.Weapon)
        {
            commands.Add(new PrimitiveCommand { op = "GRAB", target_id = target.gameObject.name });
        }
        else if (target.semanticType == SemanticType.Food)
        {
            commands.Add(new PrimitiveCommand { op = "USE_ITEM" });
        }

        if (commands.Count > 0)
            actuator.ExecutePrimitiveSequence(commands, null);
    }

    public void InterruptAndClearGoal()
    {
        currentGoal = "无";
        currentGoalTargetId = "";
        if (actuator) actuator.StopAllPhysicalMovement();
    }
}