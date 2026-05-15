using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

public class GeminiConnectionTest : MonoBehaviour
{
    // 填入你刚才测试用的 API KEY
    private string apiKey = "AIzaSyDSpiRf1p_Fxw6kzwHByrrmVQ2m7fI6nBc";
    // 使用你列表中的 3.1 flash lite 模型
    private string model = "models/gemini-3.1-flash-lite";

    void Start()
    {
        StartCoroutine(TestConnection());
    }

    IEnumerator TestConnection()
    {
        Debug.Log("正在尝试连接 Gemini...");

        string url = $"https://generativelanguage.googleapis.com/v1beta/{model}:generateContent?key={apiKey}";

        // 最简化的 JSON 结构
        string jsonPayload = "{ \"contents\": [{ \"parts\": [{ \"text\": \"你好，这是一条来自 Unity 的测试消息。请回复：连接成功。\" }] }] }";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("<color=green>连接成功！</color>");
                Debug.Log("Gemini 回复: " + request.downloadHandler.text);
            }
            else
            {
                Debug.LogError("连接失败: " + request.error);
                Debug.LogError("错误详情: " + request.downloadHandler.text);
            }
        }
    }
}