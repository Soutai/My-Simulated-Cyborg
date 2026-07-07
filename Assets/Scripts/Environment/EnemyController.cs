using UnityEngine;

/// <summary>
/// 敌对生物的写死行为逻辑：不联网、不经过大模型，纯粹的感知-追击-攻击状态机。
/// 感知范围内出现挂了 NPCAttributes 的目标就冲上去，进入攻击范围后按固定频率咬一口。
///
/// 只追猎"手无寸铁"的目标，呼应 SandboxProtocolConfig 里 Enemy 机制说明文字自己的设定
/// （"持续追踪并撕咬靠近的无武器目标"）——这样机制描述发给大模型看的时候才是真话，
/// 不会出现"文档说的和实际发生的不一样"的情况。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EnemyController : MonoBehaviour
{
    [Header("感知与追击")]
    public float perceptionRadius = 15f;
    public float chaseForce = 15f;
    public float maxChaseSpeed = 5f;

    [Header("攻击")]
    public float attackRange = 1.5f;
    public float attackDamage = 10f;
    public float attackInterval = 1.5f;

    private Rigidbody rb;
    private Transform currentTarget;
    private float attackCooldown = 0f;
    private bool wasInAttackRange = false;

    private readonly Collider[] perceptionBuffer = new Collider[16];

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        FindTarget();

        if (currentTarget == null)
        {
            wasInAttackRange = false;
            return;
        }

        float distance = Vector3.Distance(transform.position, currentTarget.position);

        if (distance <= attackRange)
        {
            // 🌟 只在刚进入攻击范围的那一瞬间刹一次车，而不是每帧都无条件清零速度——
            // 之前每帧清零会跟物理引擎的碰撞分离冲量互相打架，两个刚体重叠在一起谁都推不开，
            // 表现出来就是"穿模卡死"。只刹一次之后，哪怕物理引擎需要用几帧把重叠的刚体顶开也不会被打断。
            if (!wasInAttackRange)
            {
                rb.linearVelocity = Vector3.zero;
                wasInAttackRange = true;
            }

            TryAttack();
        }
        else
        {
            wasInAttackRange = false;
            ChaseTarget();
        }
    }

    /// <summary>
    /// 已有目标且仍然满足条件（在感知范围内、依然手无寸铁）就继续追它，不满足条件才重新搜索。
    /// </summary>
    private void FindTarget()
    {
        if (currentTarget != null)
        {
            float d = Vector3.Distance(transform.position, currentTarget.position);
            if (d <= perceptionRadius && IsValidTarget(currentTarget)) return;
            currentTarget = null;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, perceptionRadius, perceptionBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            var col = perceptionBuffer[i];
            if (col.GetComponent<NPCAttributes>() != null && IsValidTarget(col.transform))
            {
                currentTarget = col.transform;
                break;
            }
        }
    }

    /// <summary>
    /// 手无寸铁才追猎；一旦目标抓起了任何东西，视为不再符合狩猎条件（也不主动逃跑，只是不继续追）。
    /// </summary>
    private bool IsValidTarget(Transform target)
    {
        var actuator = target.GetComponent<CharacterActuator>();
        return actuator == null || actuator.CurrentGrabbedObject == null;
    }

    private void ChaseTarget()
    {
        Vector3 direction = currentTarget.position - transform.position;
        direction.y = 0f;
        direction.Normalize();

        rb.AddForce(direction * chaseForce, ForceMode.Force);

        Vector3 vel = rb.linearVelocity;
        Vector3 horizontalVel = new Vector3(vel.x, 0f, vel.z);
        if (horizontalVel.magnitude > maxChaseSpeed)
        {
            Vector3 limited = horizontalVel.normalized * maxChaseSpeed;
            rb.linearVelocity = new Vector3(limited.x, vel.y, limited.z);
        }
    }

    private void TryAttack()
    {
        attackCooldown -= Time.fixedDeltaTime;
        if (attackCooldown > 0f) return;

        attackCooldown = attackInterval;

        NPCAttributes npc = currentTarget.GetComponent<NPCAttributes>();
        if (npc != null)
        {
            npc.TakeDamage(attackDamage);
            Debug.Log($"<color=#CC0000>[敌对生物] 🐺 {gameObject.name} 咬了 {currentTarget.name} 一口，造成 {attackDamage:F1} 点伤害！</color>");
        }
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        // 感知范围：橙色圈
        UnityEditor.Handles.color = new Color(1f, 0.4f, 0f, 0.35f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, perceptionRadius);

        // 攻击范围：红色圈，跟感知范围区分开
        UnityEditor.Handles.color = new Color(1f, 0f, 0f, 0.5f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, attackRange);
#endif
    }
}
