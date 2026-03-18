// OllamaCore.cs
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class OllamaCore : MonoBehaviour
{
    [SerializeField] string baseUrl = "http://localhost:11434";
    [SerializeField] string model = "llama3.2";

    public void Ask(string prompt, Action<string> onDone)
        => StartCoroutine(Post(prompt, onDone));

    IEnumerator Post(string prompt, Action<string> onDone)
    {
        var url = $"{baseUrl}/api/generate";
        var body = $"{{\"model\":\"{model}\",\"prompt\":{Json(prompt)},\"stream\":false}}";
        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            onDone?.Invoke($"ERROR: {req.error}\n{req.downloadHandler.text}");
        else
            onDone?.Invoke(Pick(req.downloadHandler.text, "\"response\":\""));
    }

    // 只做最基本的 JSON string escape（夠用、夠短）
    static string Json(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";

    // 極短 JSON 取值：抓 response 字段（避免引入完整 JSON parser）
    static string Pick(string json, string key)
    {
        var i = json.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return json;
        i += key.Length;
        var sb = new StringBuilder();
        for (; i < json.Length; i++)
        {
            char c = json[i];
            if (c == '\\') { if (i + 1 < json.Length) { sb.Append(json[i + 1]); i++; } continue; }
            if (c == '"') break;
            sb.Append(c);
        }
        return sb.ToString();
    }

    // Demo：開場自動問一句
    void Start()
    {
        Ask("用一句中二又詩意的話介紹你自己", r => Debug.Log("[Ollama] " + r));
    }
}