using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

// ================= [数据结构保持不变] =================
[System.Serializable]
public class GeminiRequest
{
    public RequestContent[] contents;
    [System.Serializable] public class RequestContent { public RequestPart[] parts; }
    [System.Serializable] public class RequestPart { public string text; }
}

[System.Serializable]
public class GeminiResponse
{
    public Candidate[] candidates;
    [System.Serializable] public class Candidate { public Content content; }
    [System.Serializable] public class Content { public Part[] parts; }
    [System.Serializable] public class Part { public string text; }
}

[System.Serializable]
public class NPCAction
{
    public string monologue;
    public string action;
}

// ================= [核心控制脚本] =================
public class AIBrainController : MonoBehaviour
{
    [Header("API 设置")]
    private string apiKey = "";
    public string model = "models/gemini-3.1-flash-lite";

    [Header("UI 绑定")]
    public UnityEngine.UI.Text monologueDisplay;
    public UnityEngine.UI.Text actionDisplay;

    [Header("原始人生存属性")]
    public float hunger = 60f;
    private float initialHunger;

    [Header("环境感知设置")]
    public float perceptionRadius = 8f;

    private Vector3 startPosition;
    private Color startColor;
    private Dictionary<GameObject, Vector3> initialScenePositions = new Dictionary<GameObject, Vector3>();
    private Coroutine currentMoveCoroutine;

    // 用于记录雷达本次是否扫描到了对应的物体
    private bool hasFoodInSight = false;
    private bool hasEnemyInSight = false;
    private bool hasWeaponInSight = false;

    void Awake()
    {
        startPosition = transform.position;
        if (TryGetComponent<Renderer>(out var r)) startColor = r.material.color;
        initialHunger = hunger;

        RecordInitialPosition("Food");
        RecordInitialPosition("Enemy");
        RecordInitialPosition("Weapon");
    }

    void RecordInitialPosition(string tag)
    {
        GameObject obj = GameObject.FindWithTag(tag);
        if (obj != null) initialScenePositions[obj] = obj.transform.position;
    }

    // ==== 【修改后的重置仿真环境方法】 ====
    public void ResetSimulation()
    {
        Debug.Log("<color=#FFCC00>[系统管理] 📥 正在重置仿真环境...</color>");

        // 1. 强行停止正在进行的任何思考或移动协程
        StopAllCoroutines();
        currentMoveCoroutine = null;

        // 2. 【修改点 1】只将原始人（人物自身）状态复位，不再循环重置其他场景物体
        transform.position = startPosition;
        hunger = initialHunger;
        if (TryGetComponent<Renderer>(out var r))
        {
            r.material.color = startColor;
        }

        // 3. UI 文字复位
        if (monologueDisplay) monologueDisplay.text = "仿真环境已重置。等待观察...";
        if (actionDisplay) actionDisplay.text = "状态：待机";

        Debug.Log("<color=#00FF00>[系统管理] ✅ 人物已归位！正在清理控制台...</color>");

        // 4. 【修改点 2】一键清空 Unity 编辑器控制台
        ClearUnityConsole();
    }

    // 辅助方法：利用反射强行清空 Unity 控制台日志
    void ClearUnityConsole()
    {
    #if UNITY_EDITOR
        // 直接通过名字加载 UnityEditor 程序集，避开版本类名变更引发的编译失败
        var assembly = System.Reflection.Assembly.Load("UnityEditor");
        var type = assembly.GetType("UnityEditor.LogEntries");
        var method = type.GetMethod("Clear");
        if (method != null)
        {
            method.Invoke(new object(), null);
        }
    #endif
    }

    public void OnDecideAction() => StartCoroutine(Think());

    IEnumerator Think()
    {
        if (monologueDisplay) monologueDisplay.text = "观察四周中...";
        Debug.Log("<color=#FFA500>[1. 神经网络启动] 🧠 原始人AI开始进行环境逻辑推演...</color>");

        string keyFilePath = Path.Combine(Application.dataPath, "../gemini_key.txt");
        if (File.Exists(keyFilePath))
        {
            apiKey = File.ReadAllText(keyFilePath).Trim();
        }
        else
        {
            Debug.LogError($"<color=red>[Error] 未在项目根目录下找到密钥文件！</color>");
            yield break;
        }

        // 1. 物理雷达扫描（并在内部更新感知状态布尔值）
        string environmentDescription = ScanEnvironment();

        string url = $"https://generativelanguage.googleapis.com/v1beta/{model}:generateContent?key={apiKey}";

        // 2. 【核心升级】动态动态构建可行的行动选项 (Available Actions)
        List<string> allowedActions = new List<string>();
        allowedActions.Add("IDLE"); // 任何时候都可以选择原地待机守候

        if (hasFoodInSight) allowedActions.Add("MOVE_TO_FOOD");
        if (hasWeaponInSight) allowedActions.Add("PICKUP_WEAPON");
        if (hasEnemyInSight) allowedActions.Add("EVADE_ENEMY");

        string actionsJsonString = "[" + string.Join(", ", allowedActions.ToArray()) + "]";

        // 3. 【核心升级】动态构建生存常识常识警告提示
        string ruleHints = "";
        if (hasEnemyInSight)
        {
            ruleHints += "注意：周围发现了极具威胁的恶狼（Enemy）。";
            if (hasFoodInSight)
            {
                ruleHints += "如果你空手直接去拿肉，有极大概率会被恶狼拦截并咬死！";
            }
            if (hasWeaponInSight)
            {
                ruleHints += "建议你先拿地上的木棍作为武器保护自己。";
            }
        }
        else
        {
            ruleHints += "当前没有发现直接威胁，环境相对安全。";
        }

        // 4. 彻底组装全动态提示词（Prompt）
        string promptText = $"你是一个在蛮荒时代挣扎求生的原始人。" +
                            $"当前你的饥饿值是 {hunger}（100表示马上要饿死了）。\n\n" +
                            $"【你当前眼睛看到、耳朵听到的雷达实时环境报告】：\n{environmentDescription}\n\n" +
                            $"【生存本能常识】：{ruleHints}\n\n" +
                            $"请基于你的饥饿程度和周边环境，本能地做出最有利于生存的决策。\n" +
                            $"请严格只回复如下格式的纯 JSON 字符串，其中 action 字段**必须且只能**从当前可用的合法动作列表 {actionsJsonString} 中选择一个，绝对不能选择列表以外的动作：\n" +
                            $"{{\"monologue\":\"原始人的内心活动与求生本能推演\",\"action\":\"执行的动作\"}}";

        // 打印动态提示词 Log
        Debug.Log($"<color=#3399FF>[2. 最终全动态组装提示词 (Prompt)]</color>\n--------------------------------------------------\n{promptText}\n--------------------------------------------------");

        // 发送逻辑
        GeminiRequest reqData = new GeminiRequest
        {
            contents = new GeminiRequest.RequestContent[] {
                new GeminiRequest.RequestContent { parts = new GeminiRequest.RequestPart[] { new GeminiRequest.RequestPart { text = promptText } } }
            }
        };
        string jsonPayload = JsonUtility.ToJson(reqData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string rawServerResponse = request.downloadHandler.text;
                var res = JsonUtility.FromJson<GeminiResponse>(rawServerResponse);
                string aiTextContent = res.candidates[0].content.parts[0].text;

                Match match = Regex.Match(aiTextContent, @"\{.*\}", RegexOptions.Singleline);
                if (match.Success)
                {
                    try
                    {
                        NPCAction npcData = JsonUtility.FromJson<NPCAction>(match.Value);
                        Debug.Log($"<color=#00FF00>[8. 解析成功] AI 决策结果</color>\n-> 独白(monologue): {npcData.monologue}\n-> 动作(action): {npcData.action}");
                        Execute(npcData);
                    }
                    catch
                    {
                        if (monologueDisplay) monologueDisplay.text = "数据格式损坏";
                    }
                }
            }
            else
            {
                Debug.LogError($"<color=red>[Error] 请求失败: {request.error}</color>");
            }
        }
    }

    // 核心感知算法：扫描环境，并更新 AI 脑中的全局“可见状态”
    string ScanEnvironment()
    {
        // 每次扫描前，先重置可见状态
        hasFoodInSight = false;
        hasEnemyInSight = false;
        hasWeaponInSight = false;

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, perceptionRadius);
        if (hitColliders.Length == 0) return "四周一片死寂，视野内没有发现任何植物、野兽或工具。";

        List<string> detectedObjects = new List<string>();
        foreach (var col in hitColliders)
        {
            if (col.gameObject == this.gameObject) continue;

            float distance = Vector3.Distance(transform.position, col.transform.position);
            distance = Mathf.Round(distance * 10f) / 10f;

            if (col.CompareTag("Food"))
            {
                detectedObjects.Add($"在距离你 {distance} 米处，有一块【散发着诱人香味的生肉（Food）】。");
                hasFoodInSight = true;
            }
            else if (col.CompareTag("Enemy"))
            {
                detectedObjects.Add($"在距离你 {distance} 米处，有一只【龇牙咧嘴、正在低吼的恶狼（Enemy）】，极度危险。");
                hasEnemyInSight = true;
            }
            else if (col.CompareTag("Weapon"))
            {
                detectedObjects.Add($"在距离你 {distance} 米处，躺着一根【沉重的粗木棍（Weapon）】，可以用来防身打狼。");
                hasWeaponInSight = true;
            }
        }

        if (detectedObjects.Count == 0) return "周围有一些乱石杂草，但没有可以吃的东西、可用的武器，也没有危险。";
        return string.Join("\n", detectedObjects.ToArray());
    }

    void Execute(NPCAction data)
    {
        if (monologueDisplay) monologueDisplay.text = "内心独白：" + data.monologue;
        string act = data.action.ToUpper();

        if (currentMoveCoroutine != null) StopCoroutine(currentMoveCoroutine);

        if (act.Contains("MOVE_TO_FOOD") && hasFoodInSight)
        {
            if (actionDisplay) actionDisplay.text = "行动：跑向生肉";
            currentMoveCoroutine = StartCoroutine(MoveToTag("Food", Color.red));
        }
        else if (act.Contains("PICKUP_WEAPON") && hasWeaponInSight)
        {
            if (actionDisplay) actionDisplay.text = "行动：捡起木棍";
            currentMoveCoroutine = StartCoroutine(MoveToTag("Weapon", Color.green));
        }
        else if (act.Contains("EVADE_ENEMY") && hasEnemyInSight)
        {
            if (actionDisplay) actionDisplay.text = "行动：惊恐后退，躲避恶狼";
            if (TryGetComponent<Renderer>(out var r)) r.material.color = Color.yellow;

            GameObject enemy = GameObject.FindWithTag("Enemy");
            if (enemy)
            {
                Vector3 runAwayPos = transform.position + (transform.position - enemy.transform.position).normalized * 4f;
                currentMoveCoroutine = StartCoroutine(MoveTo(runAwayPos));
            }
        }
        else
        {
            if (actionDisplay) actionDisplay.text = "行动：警惕地在原地蹲守";
            if (TryGetComponent<Renderer>(out var r)) r.material.color = Color.gray;
        }
    }

    IEnumerator MoveToTag(string tag, Color feedbackColor)
    {
        if (TryGetComponent<Renderer>(out var r)) r.material.color = feedbackColor;
        GameObject targetObj = GameObject.FindWithTag(tag);
        if (targetObj != null)
        {
            yield return StartCoroutine(MoveTo(targetObj.transform.position));
        }
    }

    IEnumerator MoveTo(Vector3 target)
    {
        float speed = 3.5f;
        while (Vector3.Distance(transform.position, target) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            yield return null;
        }
        if (actionDisplay) actionDisplay.text = "当前动作完成";

        if (Vector3.Distance(transform.position, GameObject.FindWithTag("Food")?.transform.position ?? Vector3.one * 999f) < 0.2f)
        {
            actionDisplay.text = "吃下了生肉，肚子饱了！";
            hunger = 0;
            Debug.Log("<color=#00FF00>[生存状态] 🍖 成功进食，饥饿值归零！</color>");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, perceptionRadius);
    }
}