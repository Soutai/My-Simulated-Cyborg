using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnvironmentManager : MonoBehaviour
{
    public static EnvironmentManager Instance { get; private set; }

    [Header("水果（Food）生成配置")]
    [Tooltip("拖入你的 Fruit 预制体（Prefab）或者场景中的 Fruit 原始物体")]
    public GameObject foodPrefab;

    [Tooltip("每隔多少游戏小时刷新一次水果")]
    public float spawnIntervalInGameHours = 0.5f;

    [Header("随机生成区域边界")]
    public float minX = -22f;
    public float maxX = 22f;
    public float minZ = -22f;
    public float maxZ = 22f;
    [Tooltip("水果贴近地面的 Y 轴高度")]
    public float groundY = 0.25f;

    // 用来记录动态生成出来的所有水果，方便一键重置销毁
    private List<GameObject> spawnedFoods = new List<GameObject>();
    private Coroutine spawnCoroutine;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        StartEnvironmentSystem();
    }

    public void StartEnvironmentSystem()
    {
        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnFoodRoutine());
    }

    private IEnumerator SpawnFoodRoutine()
    {
        // 诊断日志 1
        Debug.Log("<color=yellow>[环境系统诊断] ⌛ 正在等待时钟单例就绪...</color>");

        while (TimeManager.Instance == null) yield return null;

        // 诊断日志 2
        Debug.Log($"<color=green>[环境系统诊断] ✨ 时钟单例已接通！当前每小时流逝速度为：{TimeManager.Instance.realSecondsPerHour}秒。定时生成器正式启动！</color>");

        while (true)
        {
            float realSeconds = spawnIntervalInGameHours * (TimeManager.Instance.realSecondsPerHour);

            // 诊断日志 3
            Debug.Log($"<color=white>[环境系统诊断] ⏱️ 定时器归零，进入倒计时：将在这个现实世界等待 {realSeconds} 秒后长出果子...</color>");

            yield return new WaitForSeconds(realSeconds);

            SpawnOneFood();
        }
    }

    /// <summary>
    /// 核心生成逻辑
    /// </summary>
    public void SpawnOneFood()
    {
        if (foodPrefab == null) return;

        // 1. 在设定的边界内随机挑一个平面坐标
        float randomX = Random.Range(minX, maxX);
        float randomZ = Random.Range(minZ, maxZ);
        Vector3 spawnPos = new Vector3(randomX, groundY, randomZ);

        // 2. 实例化水果
        GameObject newFood = Instantiate(foodPrefab, spawnPos, Quaternion.identity);

        // 3. 🌟硬性保障：确保生成出的新水果具有 "Food" 的 Tag，这样 NPC 的雷达和身体才能完美识别！
        newFood.tag = "Food";
        newFood.name = $"Fruit_Spawned_{spawnedFoods.Count + 1}";

        spawnedFoods.Add(newFood);
        Debug.Log($"<color=#33CCFF>[世界系统] 🌳 土地孕育了新的果实！生成于：{spawnPos}</color>");
    }

    /// <summary>
    /// 🌟 给重置系统调用的清场后门：一键拔除所有临时生成的水果
    /// </summary>
    public void ClearAllSpawnedFoods()
    {
        foreach (GameObject food in spawnedFoods)
        {
            if (food != null) Destroy(food);
        }
        spawnedFoods.Clear();
    }
}