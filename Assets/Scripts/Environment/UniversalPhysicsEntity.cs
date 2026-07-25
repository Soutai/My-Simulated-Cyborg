// 建议放进 📁 Environment 文件夹
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SemanticObject))] // 强行绑定语义标签，以此作为去配置中心拿数据的 Key
[RequireComponent(typeof(HitFlash))] // 受击视觉反馈，自动补齐
public class UniversalPhysicsEntity : MonoBehaviour
{
    [Header("实时物理状态监控 (无需在面板配置)")]
    [SerializeField] private float currentTolerance;
    [SerializeField] private bool isDepleted = false;

    [Header("机制语义化事件响应")]
    [Tooltip("当物理耐受度归零时触发的世界线规则。可在面板绑定或代码动态绑定")]
    public UnityEvent OnToleranceDepleted;

    private Rigidbody rb;
    private SemanticObject semanticObj;
    private HitFlash hitFlash;

    // 从配置中心动态拉取的只读物理规则
    private PhysicsProtocolConfig.PhysicsResistance physicsRule;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        semanticObj = GetComponent<SemanticObject>();
        hitFlash = GetComponent<HitFlash>();
    }

    void Start()
    {
        InitializePhysicsRule();
    }

    /// <summary>
    /// 贯彻机制语义化：从绝对配置中心拉取属于该物体的物理抗性规则
    /// </summary>
    private void InitializePhysicsRule()
    {
        // 动态读取 SandboxProtocolConfig 里定义的那个强类型枚举 (例如 SemanticType.Enemy)
        physicsRule = PhysicsProtocolConfig.GetResistance(semanticObj.semanticType);
        currentTolerance = physicsRule.maxTolerance;
        isDepleted = false;
    }

    void FixedUpdate()
    {
        if (isDepleted) return;

        // 1. 监测由于物理原语动作（如 USE_ITEM 横扫冲量）导致的速度突变
        float instantaneousSpeed = rb.linearVelocity.magnitude;

        // 一旦突破了配置中心规定的物理阈值，开始产生伤害
        if (instantaneousSpeed > physicsRule.impactThreshold)
        {
            EvaluatePhysicsImpact(instantaneousSpeed * physicsRule.damageMultiplier);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isDepleted) return;

        // 2. 监测硬碰撞（如被击飞后撞墙，或者硬物相撞）的相对动能
        float relativeImpactSpeed = collision.relativeVelocity.magnitude;
        if (relativeImpactSpeed > physicsRule.impactThreshold)
        {
            EvaluatePhysicsImpact(relativeImpactSpeed * physicsRule.damageMultiplier * physicsRule.collisionDamageMultiplier);
        }
    }

    /// <summary>
    /// 🌟 供武器命中判定（CharacterActuator.PerformSweepAttack）直接调用：不依赖下一帧被动
    /// 读取 rb.linearVelocity——那个读数容易在同一个物理帧内被目标身上其他脚本（比如
    /// EnemyController 的追击限速，每帧都会把速度夹回 maxChaseSpeed 以内）抢先篡改，导致
    /// 武器明明打中了，检测到的却是"已经被夹回正常范围"的速度，永远摸不到冲击阈值。
    /// 命中的那一刻直接把这次冲击的等效速度（冲量 / 目标质量）同步传进来评估，跟 FixedUpdate
    /// 里的检测走同一套阈值和伤害公式，只是不给任何时序竞争的机会。
    /// </summary>
    public void ReportDirectImpact(float impactSpeed)
    {
        if (isDepleted) return;

        if (impactSpeed > physicsRule.impactThreshold)
        {
            EvaluatePhysicsImpact(impactSpeed * physicsRule.damageMultiplier);
        }
    }

    private void EvaluatePhysicsImpact(float computedForce)
    {
        if (computedForce <= 0f) return;

        currentTolerance -= computedForce;
        hitFlash?.Flash();
        Debug.Log($"<color=#FF6600>[物理原语驱动] 实体 {gameObject.name} (类型: {semanticObj.semanticType}) 正在承受物理冲击: {computedForce:F1}，剩余耐受度: {currentTolerance:F1}</color>");

        if (currentTolerance <= 0f && !isDepleted)
        {
            isDepleted = true;
            currentTolerance = 0f;

            Debug.Log($"<color=#FF0055>💥 [机制语义化触发] {gameObject.name} 的物理承载已达极限，触发世界消亡规则！</color>");

            // 触发通知
            OnToleranceDepleted?.Invoke();

            // 默认行为：从物理宇宙和雷达中隐退
            gameObject.SetActive(false);
        }
    }
}