// GeminiHttpClient.cs
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System.IO;
using EmbodiedAI.DTO;

public class GeminiHttpClient : MonoBehaviour
{
    private string apiKey = ""; // 初始留空
    private string apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

    void Awake()
    {
        // 🌟【修改点】：直接从系统环境变量中读取 Key
        // 注意：EnvironmentVariableTarget.User 表示读取当前用户的环境变量
        string envKey = System.Environment.GetEnvironmentVariable("GEMINI_API_KEY", System.EnvironmentVariableTarget.User);

        // 如果用户变量没拿到，尝试拿系统全局变量
        if (string.IsNullOrEmpty(envKey))
        {
            envKey = System.Environment.GetEnvironmentVariable("GEMINI_API_KEY", System.EnvironmentVariableTarget.Machine);
        }

        if (!string.IsNullOrEmpty(envKey))
        {
            apiKey = envKey.Trim();
        }
        else
        {
            Debug.LogError("[Client] ❌ 未能在系统环境变量中找到 'GEMINI_API_KEY'！API 请求将无法正常工作。");
        }
    }

    // 后续的 PostPrompt 和 PostPromptRoutine 代码保持不变...
    public void PostPrompt(string prompt, System.Action<string> onRawTextReceived, System.Action onError = null)
    {
        StartCoroutine(PostPromptRoutine(prompt, onRawTextReceived, onError));
    }

    private IEnumerator PostPromptRoutine(string prompt, System.Action<string> onRawTextReceived, System.Action onError)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("[Client] API Key 为空，拒绝发送请求。");
            onError?.Invoke();
            yield break;
        }

        string url = $"{apiUrl}?key={apiKey}";

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
                try
                {
                    var res = JsonUtility.FromJson<GeminiResponse>(rawJson);
                    string aiTextContent = res.candidates[0].content.parts[0].text;
                    onRawTextReceived?.Invoke(aiTextContent);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Client] 基础外壳 JSON 解析失败: {e.Message}");
                    onError?.Invoke();
                }
            }
            else
            {
                Debug.LogError($"[Client] API 请求失败: {request.error}");
                onError?.Invoke();
            }
        }
    }
}