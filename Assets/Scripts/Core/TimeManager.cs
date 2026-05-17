using UnityEngine;
using UnityEngine.UI;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("UI 绑定")]
    public Text timeText; // 拖入 Canvas 上的时间 Text 组件

    [Header("时间物理流逝比例")]
    [Tooltip("现实中多少秒等于游戏里的1个小时。12分钟24小时 = 720秒/24 = 30秒/一小时")]
    public float realSecondsPerHour = 15f;

    // 系统内部时间（默认从清晨 08:00 开始生活）
    private float gameHour = 8f;
    private float gameMinute = 0f;

    // 独立广播事件：每当游戏时间跨过一个整点，通知大脑观察世界
    public System.Action OnGameHourPassed;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        float minutesPerRealSecond = 60f / realSecondsPerHour;
        gameMinute += Time.deltaTime * minutesPerRealSecond;

        // 🌟 精准扣除步长并推进时钟边界
        if (gameMinute >= 30f)
        {
            gameMinute -= 30f;
            gameHour += 0.5f;

            // 呼叫大模型 Tick
            OnGameHourPassed?.Invoke();
        }

        // 🌟 防止小时数无限制溢出
        if (gameHour >= 24f)
        {
            gameHour -= 24f;
        }

        UpdateTimerUI();
    }

    /// <summary>
    /// 获取 hh:mm 格式的 24小时制纯净文本
    /// </summary>
    public string GetCurrentTimeString()
    {
        int h = Mathf.FloorToInt(gameHour);
        int m = Mathf.FloorToInt(gameMinute);
        return string.Format("{0:D2}:{1:D2}", h, m);
    }

    private void UpdateTimerUI()
    {
        if (timeText != null)
        {
            timeText.text = GetCurrentTimeString();
        }
    }

    /// <summary>
    /// 🌟 最小追加：将世界线强行重置回初始清晨
    /// </summary>
    public void ResetTime()
    {
        gameHour = 8f;
        gameMinute = 0f;
        UpdateTimerUI();
    }
}