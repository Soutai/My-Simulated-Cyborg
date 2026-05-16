using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System.IO;

// =================================================================
// 【数据传输对象 DTO】
// 彻底挪到文件最顶层，确保整个项目和下方的 HttpClient 在编译阶段第一眼就能识别到它
// =================================================================
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


// =================================================================
// 【HTTP 纯净通信客户端】
// =================================================================
public class GeminiHttpClient : MonoBehaviour
{
    [Header("Gemini 配置")]
    public string model = "models/gemini-3.1-flash-lite";
    private string apiKey = "";

    void Awake()
    {
        string keyFilePath = Path.Combine(Application.dataPath, "../gemini_key.txt");
        if (File.Exists(keyFilePath)) apiKey = File.ReadAllText(keyFilePath).Trim();
    }

    // 纯粹的通信接口
    public IEnumerator SendPostRequest(string prompt, System.Action<NPCAction> onCallback)
    {
        if (string.IsNullOrEmpty(apiKey)) yield break;

        GeminiRequest reqData = new GeminiRequest
        {
            contents = new GeminiRequest.RequestContent[] {
                new GeminiRequest.RequestContent { parts = new GeminiRequest.RequestPart[] { new GeminiRequest.RequestPart { text = prompt } } }
            }
        };
        string jsonPayload = JsonUtility.ToJson(reqData);

        // 🟢 【色彩日志】打印发送给 Gemini API 的 JSON 载荷
        Debug.Log($"<color=#A020F0>[3. 发送给 Gemini API 的 JSON 载荷 (Payload)]</color>\n{jsonPayload}");
        Debug.Log("<color=#00FFFF>[4. HTTP 网络传输] 正在上行握手，请求发送中...</color>");

        string url = $"https://generativelanguage.googleapis.com/v1beta/{model}:generateContent?key={apiKey}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string rawJson = request.downloadHandler.text;

                // 🟢 【色彩日志】打印接收到的原始响应报文
                Debug.Log($"<color=#9400D3>[5. 收到 Google API 原始响应报文 (Raw Response)]</color>\n{rawJson}");

                NPCAction parsedData = ParseResponse(rawJson);
                if (parsedData != null) onCallback?.Invoke(parsedData);
            }
            else
            {
                Debug.LogError($"<color=red>[Error] API 请求失败: {request.error}</color>");
            }
        }
    }

    private NPCAction ParseResponse(string rawJson)
    {
        try
        {
            var res = JsonUtility.FromJson<GeminiResponse>(rawJson);
            string aiTextContent = res.candidates[0].content.parts[0].text;

            // 🟢 【色彩日志】打印剥离外壳后的文本
            Debug.Log($"<color=#1E90FF>[6. 提取层] 剥离外壳，拿到 AI 的文本回复内容：</color>\n{aiTextContent}");

            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(aiTextContent, @"\{.*\}", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (match.Success)
            {
                string cleanJson = match.Value;
                // 🟢 【色彩日志】打印清洗 Markdown 后的纯净 JSON
                Debug.Log($"<color=#FF1493>[7. 正则清洗完毕] 已成功剔除 Markdown 干扰，净化出纯净 JSON 字符串：</color>\n{cleanJson}");

                return JsonUtility.FromJson<NPCAction>(cleanJson);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GeminiHttpClient] 解析异常: {ex.Message}");
        }
        return null;
    }
}