using UnityEngine;
using UnityEngine.UI;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("UI 绑定")]
    public Text timeText;

    [Header("时间流逝速度")]
    [Tooltip("现实中多少秒 = 游戏1小时。60 = 现实1秒 = 游戏1分钟")]
    public float realSecondsPerHour = 60f;

    [Header("AI 思考频率")]
    [Tooltip("现实中每多少秒调用一次AI思考")]
    public float aiThinkInterval = 20f;

    private float totalGameMinutes = 8 * 60f;
    private float aiTimer = 0f;

    public System.Action OnAITick;   // ← 新事件

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        // 游戏时间推进
        float minutesPerRealSecond = 60f / realSecondsPerHour;
        totalGameMinutes += Time.deltaTime * minutesPerRealSecond;

        if (totalGameMinutes >= 1440f)
            totalGameMinutes -= 1440f;

        // AI 独立定时器（每20秒触发一次）
        aiTimer += Time.deltaTime;
        if (aiTimer >= aiThinkInterval)
        {
            aiTimer -= aiThinkInterval;
            OnAITick?.Invoke();
        }

        UpdateTimerUI();
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
            timeText.text = GetCurrentTimeString();
    }

    public void ResetTime()
    {
        totalGameMinutes = 8 * 60f;
        aiTimer = 0f;
        UpdateTimerUI();
    }
}