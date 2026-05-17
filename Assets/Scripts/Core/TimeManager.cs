using UnityEngine;
using UnityEngine.UI;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("UI 绑定")]
    public Text timeText;

    [Header("时间流逝速度")]
    [Tooltip("现实中多少秒 = 游戏1小时。设置为60 → 现实1秒 = 游戏1分钟")]
    public float realSecondsPerHour = 60f;

    // 内部使用更精确的时间（总游戏分钟数）
    private float totalGameMinutes = 8 * 60f;   // 从 8:00 开始

    public System.Action OnGameHourPassed;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        // 计算每帧应该增加的游戏分钟
        float minutesPerRealSecond = 60f / realSecondsPerHour;
        totalGameMinutes += Time.deltaTime * minutesPerRealSecond;

        // 防止一天结束后循环（24小时 = 1440分钟）
        if (totalGameMinutes >= 1440f)
        {
            totalGameMinutes -= 1440f;
        }

        // 每过30游戏分钟触发一次大脑思考（保持原有节奏）
        // 使用 while 防止帧率低时漏掉多次触发
        while (totalGameMinutes >= GetNextTickTime())
        {
            OnGameHourPassed?.Invoke();
        }

        UpdateTimerUI();
    }

    // 计算下一次触发思考的时间点（每30分钟一次）
    private float GetNextTickTime()
    {
        return Mathf.Floor(totalGameMinutes / 30f) * 30f + 30f;
    }

    public string GetCurrentTimeString()
    {
        int totalMinutes = Mathf.FloorToInt(totalGameMinutes);
        int h = (totalMinutes / 60) % 24;
        int m = totalMinutes % 60;
        return string.Format("{0:D2}:{1:D2}", h, m);
    }

    private void UpdateTimerUI()
    {
        if (timeText != null)
        {
            timeText.text = GetCurrentTimeString();
        }
    }

    public void ResetTime()
    {
        totalGameMinutes = 8 * 60f;   // 重置到 08:00
        UpdateTimerUI();
    }
}