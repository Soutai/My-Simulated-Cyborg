using UnityEngine;
using UnityEngine.UI; // 👈 既存保持：确保能使用 Slider 和 Text

public class NPCAttributes : MonoBehaviour
{
    [Header("生存属性 (0 - 100)")]
    [Tooltip("100表示彻底饱腹，0表示彻底饿死。替代原有的饥饿值逻辑")]
    public float satiety = 100f;

    [Header("性格/本能倾向设定")]
    [Tooltip("RISK_AVOIDANT: 极度怕死，优先防身; " +
             "RISK_TAKER: 亡命之徒，饿了就要吃；" +
             "GLUTTONS: 贪吃的人，只要不饱就想吃东西；")]
    public string personality = "GLUTTONS";

    [Header("UI 绑定")]
    public Slider satietySlider;
    public Text satietyPercentageText; // 👈 🌟新追加：用来显示百分比的文本组件

    [Header("生态自然扣减系数")]
    [Tooltip("游戏世界中每过去一个小时，自动跌落多少点饱食度")]
    public float satietyLossPerHour = 10f;

    private float initialSatiety;
    private Vector3 startPosition;
    private Color startColor;
    private Renderer myRenderer;

    void Awake()
    {
        startPosition = transform.position;
        myRenderer = GetComponent<Renderer>();
        if (myRenderer != null) startColor = myRenderer.material.color;
        initialSatiety = satiety;

        if (satietySlider != null)
        {
            satietySlider.minValue = 0;
            satietySlider.maxValue = 100;
        }
    }

    void Update()
    {
        // 生态自减核心算法
        if (TimeManager.Instance != null && satiety > 0)
        {
            float lossPerSecond = satietyLossPerHour / TimeManager.Instance.realSecondsPerHour;
            satiety -= Time.deltaTime * lossPerSecond;
            satiety = Mathf.Clamp(satiety, 0f, 100f);
        }

        UpdateUI();

        // 既存兼容逻辑：当饱食度接近崩溃(<=5)相当于以前的饥饿度达95以上，触发身体发黑表现
        if (satiety <= 5f)
        {
            SetColor(Color.black);
        }
    }

    private void UpdateUI()
    {
        // 1. 刷新进度条
        if (satietySlider != null)
        {
            satietySlider.value = satiety;
        }

        // 2. 🌟新追加：动态计算百分比并刷新文本（Mathf.CeilToInt 向上取整防止显示0%但其实还没挂）
        if (satietyPercentageText != null)
        {
            int percent = Mathf.CeilToInt(satiety);
            satietyPercentageText.text = percent + "%";
        }
    }

    // 重置属性与物理位置
    public void ResetAttributes()
    {
        transform.position = startPosition;
        satiety = initialSatiety;
        ResetColor();
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
    /// 🌟 升级版进食结算（支持接收具体的食物对象并销毁）
    /// </summary>
    public void ConsumeFood(GameObject foodTarget = null)
    {
        satiety = Mathf.Clamp(satiety + 15f, 0f, 100f);
        ResetColor();

        // 动态识别吃掉的水果对象名称
        string foodName = foodTarget != null ? foodTarget.name : "食物";
        Debug.Log($"<color=#00FF00>[生存状态] 🍖 成功进食，[{foodName}] 已被消灭，饱食度重回巅峰！</color>");
    }
}