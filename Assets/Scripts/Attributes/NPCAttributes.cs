using UnityEngine;
using UnityEngine.UI; // 👈 既存保持：确保能使用 Slider 和 Text

/// <summary>
/// NPC 性格/本能倾向。用枚举而非字符串，避免手打拼写错误导致悄无声息地退化成默认性格。
/// </summary>
public enum NpcPersonality
{
    GLUTTONS,       // 贪吃者：只要不饱就想吃东西
    RISK_AVOIDANT,  // 极度怕死：优先防身
    RISK_TAKER,     // 亡命之徒：饿了就要吃，敌人是猎物
    NEUTRAL         // 理性中立：纯粹基于物理得失计算
}

[RequireComponent(typeof(HitFlash))] // 受击视觉反馈，自动补齐
public class NPCAttributes : MonoBehaviour
{
    public const float MaxSatiety = 100f;

    [Header("生存属性 (0 - 100)")]
    [Tooltip("100表示彻底饱腹，0表示彻底饿死。替代原有的饥饿值逻辑")]
    [SerializeField] private float satiety = MaxSatiety;

    /// <summary>
    /// 当前饱食度。读写统一走这个属性，赋值时自动夹在 [0, MaxSatiety] 区间内，
    /// 杜绝外部脚本绕过边界直接写坏数值。
    /// </summary>
    public float Satiety
    {
        get => satiety;
        private set => satiety = Mathf.Clamp(value, 0f, MaxSatiety);
    }

    public const float MaxHealth = 100f;

    [Header("生命值 (0 - 100)")]
    [Tooltip("被 Enemy 攻击时扣减，归零视为死亡")]
    [SerializeField] private float health = MaxHealth;

    /// <summary>
    /// 当前生命值，读写统一走这个属性，自动夹在 [0, MaxHealth] 区间内。
    /// </summary>
    public float Health
    {
        get => health;
        private set => health = Mathf.Clamp(value, 0f, MaxHealth);
    }

    [Header("性格/本能倾向设定")]
    [Tooltip("直接在下拉菜单中选择，无需手动拼写字符串")]
    public NpcPersonality personality = NpcPersonality.GLUTTONS;

    [Header("UI 绑定")]
    public Slider satietySlider;
    public Text satietyPercentageText; // 👈 🌟新追加：用来显示百分比的文本组件

    [Header("生态自然扣减系数")]
    [Tooltip("游戏世界中每过去一个小时，自动跌落多少点饱食度")]
    public float satietyLossPerHour = 10f;

    [Header("濒死视觉警示")]
    [Tooltip("饱食度低于此值时，身体变色作为濒死警示")]
    public float lowSatietyThreshold = 5f;
    [Tooltip("濒死时的警示颜色")]
    public Color lowSatietyColor = Color.black;

    private Color startColor;
    private Renderer myRenderer;
    private HitFlash hitFlash;

    void Awake()
    {
        // 🌟 用 GetComponentInChildren 而非 GetComponent：即便以后模型挂在子物体上也能找到
        myRenderer = GetComponentInChildren<Renderer>();
        if (myRenderer != null) startColor = myRenderer.material.color;
        hitFlash = GetComponent<HitFlash>();

        if (satietySlider != null)
        {
            satietySlider.minValue = 0;
            satietySlider.maxValue = MaxSatiety;
        }
    }

    void Update()
    {
        // 生态自减核心算法
        if (TimeManager.Instance != null && Satiety > 0)
        {
            float lossPerSecond = satietyLossPerHour / TimeManager.Instance.realSecondsPerHour;
            Satiety -= Time.deltaTime * lossPerSecond;
        }

        UpdateUI();

        // 既存兼容逻辑：饱食度接近崩溃时，触发身体变色表现
        // 🌟 受击闪光进行中时暂时让路，不然每帧都会把颜色抢回来，闪光就看不见了
        bool isFlashing = hitFlash != null && hitFlash.IsFlashing;
        if (!isFlashing && Satiety <= lowSatietyThreshold)
        {
            SetColor(lowSatietyColor);
        }
    }

    private void UpdateUI()
    {
        // 1. 刷新进度条
        if (satietySlider != null)
        {
            satietySlider.value = Satiety;
        }

        // 2. 🌟新追加：动态计算百分比并刷新文本（Mathf.CeilToInt 向上取整防止显示0%但其实还没挂）
        if (satietyPercentageText != null)
        {
            int percent = Mathf.CeilToInt(Satiety);
            satietyPercentageText.text = percent + "%";
        }
    }

    // 改变身体颜色（反馈状态）
    public void SetColor(Color color)
    {
        if (myRenderer != null) myRenderer.material.color = color;
    }

    public void ResetColor()
    {
        if (myRenderer != null) myRenderer.material.color = startColor;
    }

    /// <summary>
    /// 🌟 通用消耗结算：任何被 PhysicsProtocolConfig 标记为 Consume 效果的物体都会调用这里，
    /// 不只是食物，所以方法名和实现都不对具体物体类型做假设。
    /// </summary>
    public void RestoreSatiety(GameObject source, float amount)
    {
        Satiety += amount;
        ResetColor();

        string sourceName = source != null ? source.name : "未知来源";
        Debug.Log($"<color=#00FF00>[生存状态] 🍖 成功进食，[{sourceName}] 已被消灭，饱食度重回巅峰！</color>");
    }

    /// <summary>
    /// 🌟 受到伤害结算：目前由 EnemyController 的攻击调用。
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (Health <= 0f) return; // 已经死亡，不再重复结算

        Health -= amount;
        hitFlash?.Flash();

        Debug.Log($"<color=#CC0000>[生存状态] 💥 受到 {amount:F1} 点伤害，剩余生命值: {Health:F1}</color>");

        if (Health <= 0f)
        {
            Debug.Log($"<color=#FF0000>💀 [生存状态] 生命值归零，NPC 已死亡，从世界中消失。</color>");

            GetComponent<AIBrainController>()?.InterruptAndClearGoal();
            gameObject.SetActive(false);
        }
    }
}
