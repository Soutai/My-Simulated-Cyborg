using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using System.IO; // 引入文件流命名空间

// ================= [数据结构不变] =================
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
    [Tooltip("现在无需在面板填入Key，代码会自动读取项目根目录下的 gemini_key.txt 文件")]
    private string apiKey = "";
    public string model = "models/gemini-3.1-flash-lite";

    [Header("UI 绑定")]
    public UnityEngine.UI.Text monologueDisplay;
    public UnityEngine.UI.Text actionDisplay;

    [Header("NPC 属性")]
    public float hunger = 90f;

    public void OnDecideAction() => StartCoroutine(Think());

    IEnumerator Think()
    {
        if (monologueDisplay) monologueDisplay.text = "思考中...";
        Debug.Log("<color=#FFA500>[Gemini AI] 开始决策进程...</color>");

        // 【核心修正】动态读取本地 Key 文件
        string keyFilePath = Path.Combine(Application.dataPath, "../gemini_key.txt");
        if (File.Exists(keyFilePath))
        {
            apiKey = File.ReadAllText(keyFilePath).Trim();
        }
        else
        {
            Debug.LogError($"<color=red>[Error] 未在项目根目录找到密钥文件！请在项目文件夹（Assets同级目录）下创建 gemini_key.txt 并粘贴你的Key。</color>");
            if (monologueDisplay) monologueDisplay.text = "缺少API密钥";
            yield break;
        }

        string url = $"https://generativelanguage.googleapis.com/v1beta/{model}:generateContent?key={apiKey}";

        // 1. 构建 Prompt
        string promptText = $"你是一个原始人。饥饿值{hunger}。面前有肉。请只回复如下格式的纯 JSON 字符串：{{\"monologue\":\"独白\",\"action\":\"MOVE_TO_FOOD\"}}";
        Debug.Log($"<color=#3399FF>[1. Prompt构建成功]</color>\n内容: {promptText}");

        // 2. 使用对象安全构建 JSON Payload
        GeminiRequest reqData = new GeminiRequest
        {
            contents = new GeminiRequest.RequestContent[] {
                new GeminiRequest.RequestContent {
                    parts = new GeminiRequest.RequestPart[] {
                        new GeminiRequest.RequestPart { text = promptText }
                    }
                }
            }
        };
        string jsonPayload = JsonUtility.ToJson(reqData);
        Debug.Log($"<color=#3399FF>[2. 请求Payload序列化成功]</color>\n发送的数据: {jsonPayload}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            Debug.Log("<color=#9966FF>[3. 正在向 Gemini 接口发送 POST 请求...]</color>");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("<color=#00FF00>[4. 收到 Google API 响应！服务器返回状态: 200 OK]</color>");

                var res = JsonUtility.FromJson<GeminiResponse>(request.downloadHandler.text);
                string rawText = res.candidates[0].content.parts[0].text;
                Debug.Log($"<color=#00FFFF>[5. 提取到 AI 原始回复文本]</color>:\n{rawText}");

                Match match = Regex.Match(rawText, @"\{.*\}", RegexOptions.Singleline);

                if (match.Success)
                {
                    string cleanJson = match.Value;
                    Debug.Log($"<color=#00FF00>[7. 正则清洗完成，提取到纯 JSON 块]</color>:\n{cleanJson}");

                    try
                    {
                        NPCAction npcData = JsonUtility.FromJson<NPCAction>(cleanJson);
                        Debug.Log($"<color=#00FF00>[8. 业务 JSON 反序列化成功！]</color>\n解析对象 -> 独白: {npcData.monologue}, 动作: {npcData.action}");
                        Execute(npcData);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"<color=red>[Error] JSON 字段结构映射失败！异常: {ex.Message}</color>");
                        if (monologueDisplay) monologueDisplay.text = "数据格式解析失败";
                    }
                }
            }
            else
            {
                Debug.LogError($"<color=red>[Error] 网络请求实际失败: {request.error}</color>\n错误详情: {request.downloadHandler.text}");
                if (monologueDisplay) monologueDisplay.text = "网络错误";
            }
        }
    }

    void Execute(NPCAction data)
    {
        if (monologueDisplay) monologueDisplay.text = "内心独白：" + data.monologue;

        if (data.action.ToUpper().Contains("MOVE_TO_FOOD"))
        {
            if (actionDisplay) actionDisplay.text = "行动：寻找食物";
            if (TryGetComponent<Renderer>(out var r)) r.material.color = Color.red;

            GameObject food = GameObject.FindWithTag("Food");
            if (food != null)
            {
                Debug.Log($"<color=#FF66CC>[NPC 状态机] 检测到动作指令: MOVE_TO_FOOD。开始移步...</color>");
                StartCoroutine(MoveTo(food.transform.position));
            }
        }
        else
        {
            if (actionDisplay) actionDisplay.text = "行动：待机";
        }
    }

    IEnumerator MoveTo(Vector3 target)
    {
        float speed = 3.0f;
        while (Vector3.Distance(transform.position, target) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            yield return null;
        }
        if (actionDisplay) actionDisplay.text = "已吃到肉";
        Debug.Log("<color=#00FF00>[NPC 状态机] 行动抵达，肉已吃完！</color>");
        hunger = 0;
    }
}