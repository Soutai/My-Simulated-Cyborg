// GeminiHttpClient.cs
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System.IO; // 支持 File.Exists 和 File.ReadAllText
using EmbodiedAI.DTO;

public class GeminiHttpClient : MonoBehaviour
{
    private string apiKey = "YOUR_GEMINI_API_KEY";
    // 🌟【已修复】：修正了行尾多余的反斜杠闭合错误
    private string apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

    // 在游戏启动时，最小限度地从外部 txt 文件载入真正的 API Key
    void Awake()
    {
        string keyFilePath = Path.Combine(Application.dataPath, "../gemini_key.txt");
        if (File.Exists(keyFilePath))
        {
            // 读取文件并去掉可能存在的前后空格或换行符
            apiKey = File.ReadAllText(keyFilePath).Trim();
            Debug.Log("<color=#00FF00>[Client] 🔑 已成功从外部 gemini_key.txt 载入 API Key！</color>");
        }
        else
        {
            Debug.LogWarning($"[Client] ⚠️ 未在路径找到 API Key 文件: {keyFilePath}，将使用面板/默认配置。");
        }
    }

    // 回调返回纯文本（Raw Text）
    // 🌟 确保末尾多了一个 System.Action onError = null
    public void PostPrompt(string prompt, System.Action<string> onRawTextReceived, System.Action onError = null)
    {
        // 🌟 确保这里把 onError 作为第三个参数传给了协程！
        StartCoroutine(PostPromptRoutine(prompt, onRawTextReceived, onError));
    }

    private IEnumerator PostPromptRoutine(string prompt, System.Action<string> onRawTextReceived, System.Action onError)
    {
        string url = $"{apiUrl}?key={apiKey}";

        // 组装通用的 Google API 请求外壳
        GeminiRequest requestBody = new GeminiRequest
        {
            contents = new GeminiRequest.RequestContent[]
            {
                new GeminiRequest.RequestContent
                {
                    parts = new GeminiRequest.RequestPart[] { new GeminiRequest.RequestPart { text = prompt } }
                }
            }
        };

        string jsonPayload = JsonUtility.ToJson(requestBody);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string rawJson = request.downloadHandler.text;

                // 在底层只做最基础的外壳剥离，拿到 AI 的纯文本回复
                try
                {
                    var res = JsonUtility.FromJson<GeminiResponse>(rawJson);
                    string aiTextContent = res.candidates[0].content.parts[0].text;

                    // 将纯文本回调给上层大脑去解析
                    onRawTextReceived?.Invoke(aiTextContent);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Client] 基础外壳 JSON 解析失败: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[Client] API 请求失败: {request.error}");
                // 🌟 这样里面就能认得 onError 了！
                onError?.Invoke();
            }
        }
    }
}