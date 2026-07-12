using UnityEngine;

/// <summary>
/// 敌对生物的写死行为逻辑：不联网、不经过大模型，纯粹的感知-追击-攻击状态机。
/// 感知范围内出现挂了 NPCAttributes 的目标就冲上去，进入攻击范围后按固定频率咬一口。
///
/// 只要还活着、进入感知范围就会被追猎，不管手上有没有武器——呼应 SandboxProtocolConfig
/// 里 Enemy 机制说明文字自己的设定，这样机制描述发给大模型看的时候才是真话，
/// 不会出现"文档说的和实际发生的不一样"的情况。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EnemyController : MonoBehaviour
{
    [Header("感知与追击")]
    [Tooltip("场景是 50x50 的方形区域；这个值必须明显小于 NPC 本能反射的 alertRange，" +
        "确保 NPC 至少和狼同时甚至更早察觉到追击，不会被打个措手不及")]
    public float perceptionRadius = 14f;
    [Tooltip("必须明显低于 NPC 的 fleeForce，否则 NPC 每次被逼到墙角/障碍物重新起步时都会被加速度更快的狼追上，永远逃不掉")]
    public float chaseForce = 13f;
    [Tooltip("必须明显低于 NPC 的 maxFleeSpeed，这样只要 NPC 有足够开阔的直线距离，差距就会持续拉开")]
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
    /// 已有目标且仍然满足条件（在感知范围内、还活着）就继续追它，不满足条件才重新搜索。
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
    /// 🌟 只要还活着就是有效猎物，不管手上有没有武器——狼不怕武器，只怕武器打出来的物理撞击
    /// （这是 UniversalPhysicsEntity 那套物理耐受度系统的事，跟这里"要不要追"完全是两回事）。
    /// 目标已经死亡（消失）则直接放弃，不然会对着一具尸体持续"攻击"下去。
    /// </summary>
    private bool IsValidTarget(Transform target)
    {
        var npc = target.GetComponent<NPCAttributes>();
        return npc == null || npc.Health > 0f;
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
