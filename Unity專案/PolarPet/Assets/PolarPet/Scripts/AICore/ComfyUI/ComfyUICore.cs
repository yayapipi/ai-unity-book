// ComfyUICore.cs (short + working + ImageUI)
using System;
using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ComfyUICore : MonoBehaviour
{
    [Header("ComfyUI")]
    public string baseUrl = "http://127.0.0.1:8188";
    public float pollIntervalSeconds = 1.0f;
    public float maxWaitSeconds = 120f;
    public string clientId = "UnityClient";

    [Header("UI")]
    public Image imageUI;

    [Header("Test Prompt")]
    [TextArea] public string positivePrompt = "a cute polar bear wizard, cinematic lighting, high detail";
    [TextArea] public string negativePrompt = "lowres, blurry, bad anatomy";
    public string checkpoint = "sd_xl_base_1.0.safetensors";   // 改成你本機存在的 ckpt 名稱
    public int width = 768;
    public int height = 768;
    public int steps = 20;
    public float cfg = 7f;
    public float denoise = 1f;
    public string samplerName = "euler";
    public string scheduler = "normal";
    public int seed = 12345;
    public string filenamePrefix = "UnityComfyUI";

    void Start()
    {
        var req = new ComfyUIRequest
        {
            positivePrompt = positivePrompt,
            negativePrompt = negativePrompt,
            checkpoint = checkpoint,
            width = width,
            height = height,
            steps = steps,
            cfg = cfg,
            denoise = denoise,
            samplerName = samplerName,
            scheduler = scheduler,
            seed = seed,
            filenamePrefix = filenamePrefix
        };

        StartCoroutine(GenerateToImageUI(req));
    }

    public IEnumerator GenerateToImageUI(ComfyUIRequest request)
    {
        // 1) POST /prompt
        var promptJson = BuildMinimalText2ImgWorkflowJson(request);
        var body = "{\"prompt\":" + promptJson + ",\"client_id\":\"" + EscapeJson(clientId) + "\"}";

        using var req = new UnityWebRequest($"{baseUrl}/prompt", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"POST /prompt 失敗: {req.error}\n{req.downloadHandler.text}");
            yield break;
        }

        PromptIdResponse resp;
        try { resp = JsonUtility.FromJson<PromptIdResponse>(req.downloadHandler.text); }
        catch (Exception ex)
        {
            Debug.LogError($"解析 prompt_id 失敗: {ex.Message}\n原始回應:\n{req.downloadHandler.text}");
            yield break;
        }

        if (resp == null || string.IsNullOrEmpty(resp.prompt_id))
        {
            Debug.LogError($"回應未包含 prompt_id。\n原始回應:\n{req.downloadHandler.text}");
            yield break;
        }

        // 2) Poll /history/{prompt_id}
        string filename = null, subfolder = "", type = "output";
        float waited = 0f;

        while (waited < maxWaitSeconds)
        {
            using var histReq = UnityWebRequest.Get($"{baseUrl}/history/{resp.prompt_id}");
            yield return histReq.SendWebRequest();

            if (histReq.result == UnityWebRequest.Result.Success)
            {
                var json = histReq.downloadHandler.text;
                if (TryExtractFirstImageInfo(json, out filename, out subfolder, out type))
                    break;

                // 若 ComfyUI 回了 node_errors，你也會想立刻看到（不然就一直等到逾時）
                if (json.Contains("\"node_errors\"", StringComparison.Ordinal))
                    Debug.LogWarning("history 回傳包含 node_errors，代表 workflow/模型/節點可能有錯。\n" + json);
            }

            yield return new WaitForSecondsRealtime(pollIntervalSeconds);
            waited += pollIntervalSeconds;
        }

        if (string.IsNullOrEmpty(filename))
        {
            Debug.LogError($"Timed out: no image found in /history for prompt_id={resp.prompt_id}");
            yield break;
        }

        // 3) GET /view 下載圖片
        string viewUrl = $"{baseUrl}/view?filename={UnityWebRequest.EscapeURL(filename)}" +
                         $"&subfolder={UnityWebRequest.EscapeURL(subfolder ?? string.Empty)}" +
                         $"&type={UnityWebRequest.EscapeURL(type ?? "output")}";

        using var texReq = UnityWebRequestTexture.GetTexture(viewUrl);
        yield return texReq.SendWebRequest();

        if (texReq.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"下載圖片失敗: {texReq.error}\nURL:\n{viewUrl}");
            yield break;
        }

        var tex = DownloadHandlerTexture.GetContent(texReq);
        if (tex == null) { Debug.LogError("下載成功但 Texture2D 為 null"); yield break; }

        // 顯示到 Image UI
        if (imageUI)
        {
            var spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            imageUI.sprite = spr;
            imageUI.preserveAspect = true;
        }
        else
        {
            Debug.LogWarning("imageUI 未指定，已生成但無法顯示。");
        }
    }

    // -------- Models --------
    [Serializable] public class PromptIdResponse { public string prompt_id; }

    [Serializable]
    public class ComfyUIRequest
    {
        public string positivePrompt, negativePrompt, checkpoint, samplerName, scheduler, filenamePrefix;
        public int width, height, steps, seed;
        public float cfg, denoise;
    }

    // -------- Workflow (沿用你「可用的」節點骨架) --------
    static string BuildMinimalText2ImgWorkflowJson(ComfyUIRequest r)
    {
        long sd = r.seed >= 0 ? r.seed : UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        var ic = CultureInfo.InvariantCulture;

        return $@"
{{
  ""2"": {{
    ""inputs"": {{ ""ckpt_name"": ""{EscapeJson(r.checkpoint)}"" }},
    ""class_type"": ""CheckpointLoaderSimple""
  }},
  ""3"": {{
    ""inputs"": {{
      ""text"": ""{EscapeJson(r.positivePrompt)}"",
      ""clip"": [ ""2"", 1 ]
    }},
    ""class_type"": ""CLIPTextEncode""
  }},
  ""4"": {{
    ""inputs"": {{
      ""text"": ""{EscapeJson(r.negativePrompt)}"",
      ""clip"": [ ""2"", 1 ]
    }},
    ""class_type"": ""CLIPTextEncode""
  }},
  ""6"": {{
    ""inputs"": {{ ""width"": {r.width}, ""height"": {r.height}, ""batch_size"": 1 }},
    ""class_type"": ""EmptyLatentImage""
  }},
  ""5"": {{
    ""inputs"": {{
      ""seed"": {sd},
      ""steps"": {r.steps},
      ""cfg"": {r.cfg.ToString(ic)},
      ""sampler_name"": ""{EscapeJson(r.samplerName)}"",
      ""scheduler"": ""{EscapeJson(r.scheduler)}"",
      ""denoise"": {r.denoise.ToString(ic)},
      ""model"": [ ""2"", 0 ],
      ""positive"": [ ""3"", 0 ],
      ""negative"": [ ""4"", 0 ],
      ""latent_image"": [ ""6"", 0 ]
    }},
    ""class_type"": ""KSampler""
  }},
  ""7"": {{
    ""inputs"": {{ ""samples"": [ ""5"", 0 ], ""vae"": [ ""2"", 2 ] }},
    ""class_type"": ""VAEDecode""
  }},
  ""8"": {{
    ""inputs"": {{
      ""images"": [ ""7"", 0 ],
      ""filename_prefix"": ""{EscapeJson(string.IsNullOrWhiteSpace(r.filenamePrefix) ? "UnityComfyUI" : r.filenamePrefix)}""
    }},
    ""class_type"": ""SaveImage""
  }}
}}";
    }

    static string EscapeJson(string s)
        => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // -------- history parser (沿用你可用的 regex) --------
    static bool TryExtractFirstImageInfo(string json, out string filename, out string subfolder, out string type)
    {
        filename = null; subfolder = ""; type = "output";
        if (string.IsNullOrEmpty(json)) return false;

        var imagesMatch = Regex.Match(json, "\"images\"\\s*:\\s*\\[\\s*\\{([\\s\\S]*?)\\}\\s*\\]");
        if (!imagesMatch.Success) return false;

        var block = imagesMatch.Groups[1].Value;

        var fnameMatch = Regex.Match(block, "\"filename\"\\s*:\\s*\"([^\"]+)\"");
        if (fnameMatch.Success) filename = fnameMatch.Groups[1].Value;

        var subMatch = Regex.Match(block, "\"subfolder\"\\s*:\\s*\"([^\"]*)\"");
        if (subMatch.Success) subfolder = subMatch.Groups[1].Value;

        var typeMatch = Regex.Match(block, "\"type\"\\s*:\\s*\"([^\"]*)\"");
        if (typeMatch.Success) type = typeMatch.Groups[1].Value;

        return !string.IsNullOrEmpty(filename);
    }
}
