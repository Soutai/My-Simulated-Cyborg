using UnityEngine;
using System.Collections;

public class CharacterActuator : MonoBehaviour
{
    public float moveSpeed = 3.5f;
    private Coroutine currentMoveCoroutine;
    private NPCAttributes attributes;

    void Awake()
    {
        attributes = GetComponent<NPCAttributes>();
    }

    // 执行移动决策
    public void ExecuteAction(string action, bool hasFood, bool hasWeapon, bool hasEnemy, UnityEngine.UI.Text actionDisplay)
    {
        StopMovement(); // 先停止上一次的移动

        string act = action.ToUpper();

        if (act.Contains("MOVE_TO_FOOD") && hasFood)
        {
            if (actionDisplay) actionDisplay.text = "行动：跑向食物";
            currentMoveCoroutine = StartCoroutine(MoveToTag("Food", Color.red, actionDisplay));
        }
        else if (act.Contains("PICKUP_WEAPON") && hasWeapon)
        {
            if (actionDisplay) actionDisplay.text = "行动：捡起木棍";
            currentMoveCoroutine = StartCoroutine(MoveToTag("Weapon", Color.green, actionDisplay));
        }
        else if (act.Contains("EVADE_ENEMY") && hasEnemy)
        {
            if (actionDisplay) actionDisplay.text = "行动：惊恐后退，躲避恶狼";
            if (attributes) attributes.SetColor(Color.yellow);

            GameObject enemy = GameObject.FindWithTag("Enemy");
            if (enemy)
            {
                Vector3 runAwayPos = transform.position + (transform.position - enemy.transform.position).normalized * 4f;
                currentMoveCoroutine = StartCoroutine(MoveTo(runAwayPos, actionDisplay));
            }
        }
        else
        {
            if (actionDisplay) actionDisplay.text = "行动：警惕地在原地蹲守";
            if (attributes) attributes.SetColor(Color.gray);
        }
    }

    // 强行掐断当前的身体运动
    public void StopMovement()
    {
        if (currentMoveCoroutine != null)
        {
            StopCoroutine(currentMoveCoroutine);
            currentMoveCoroutine = null;
        }
    }

    private IEnumerator MoveToTag(string tag, Color feedbackColor, UnityEngine.UI.Text actionDisplay)
    {
        if (attributes) attributes.SetColor(feedbackColor);
        GameObject targetObj = GameObject.FindWithTag(tag);
        if (targetObj != null)
        {
            yield return StartCoroutine(MoveTo(targetObj.transform.position, actionDisplay));
        }
    }

    private IEnumerator MoveTo(Vector3 target, UnityEngine.UI.Text actionDisplay)
    {
        // 🌟 最小化补丁：强行锁死 Y 轴高度，让目标点的高度等于 NPC 当前的自身高度
        // 这样 NPC 就只会水平走过去，绝对不会因为目标太矮而陷入地下！
        target.y = transform.position.y;

        while (Vector3.Distance(transform.position, target) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }

        if (actionDisplay) actionDisplay.text = "当前动作完成";

        // 运动完成后，检测是否到达食物点 (保持 Tag 为 "Food" 检测)
        GameObject foodObj = GameObject.FindWithTag("Food");

        // 🌟 顺便微调：因为高度被锁死了，NPC 和地面上的水果在 3D 空间上可能存在 Y 轴高度差。
        // 为了稳定触发吃水果，我们用 Vector2.Distance 只判断它们在地面（X和Z轴）上的平面距离！
        if (foodObj != null && Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(foodObj.transform.position.x, foodObj.transform.position.z)) < 0.5f)
        {
            if (actionDisplay) actionDisplay.text = "吃下了水果，肚子饱了！";

            if (attributes) attributes.ConsumeFood(foodObj);
            Destroy(foodObj);
        }
    }
}