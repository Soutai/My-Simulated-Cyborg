// GeminiHttpClient.cs
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
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

    public void PostPrompt(string prompt, System.Action<string> onRawTextReceived, System.Action onError = null)
    {
        StartCoroutine(PostPromptRoutine(prompt, onRawTextReceived, onError));
    }

    private IEnumerator PostPromptRoutine(string prompt, System.Action<string> onRawTextReceived, System.Action onError)
    {
        // 🌟【追加的 LOG 1】：显式高亮打印发送给 AI 的原始具身语义 Prompt
        Debug.Log(
            $"<color=#FF00FF>============ 🚀 [网络层] 正在向 Gemini 引擎提交当前环境 Tick 数据 ============\n" +
            $"{prompt}\n" +
            $"========================================================================</color>"
        );

        string url = string.IsNullOrEmpty(apiKey) ? apiUrl : $"{apiUrl}?key={apiKey}";

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
            // 🌟 网络卡死时兜底：没有超时的话，请求可能永远挂起，导致 isThinking 永远卡在 true，NPC 彻底停止思考
            request.timeout = 15;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string rawJson = request.downloadHandler.text;
                try
                {
                    var res = JsonUtility.FromJson<GeminiResponse>(rawJson);

                    if (res.candidates == null || res.candidates.Length == 0)
                    {
                        string blockReason = res.promptFeedback != null && !string.IsNullOrEmpty(res.promptFeedback.blockReason)
                            ? res.promptFeedback.blockReason
                            : "未知原因";
                        Debug.LogError($"[Client] ❌ Gemini 未返回候选结果（可能被安全策略拦截，拦截原因: {blockReason}）");
                        onError?.Invoke();
                        yield break;
                    }

                    string aiTextContent = res.candidates[0].content.parts[0].text;

                    // 🌟【追加的 LOG 2】：显式高亮打印 AI 回复的原始决策内容
                    Debug.Log(
                        $"<color=#00FFFF>============ 📩 [网络层] 成功收到 Gemini 引擎决策响应 ============\n" +
                        $"{aiTextContent}\n" +
                        $"========================================================================</color>"
                    );

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
                // 🌟 请求超时/网络失败是已经被优雅兜底处理的预期情况（15s 超时 + onError 回调走正常降级流程），
                // 不用 LogError——否则如果 Unity Console 的"Error Pause"意外被打开，
                // 每次网络抖动都会把整个 Play Mode 直接冻结，表现得像莫名其妙的死机。
                Debug.LogWarning($"[Client] API 请求失败: {request.error}");
                onError?.Invoke();
            }
        }
    }
}