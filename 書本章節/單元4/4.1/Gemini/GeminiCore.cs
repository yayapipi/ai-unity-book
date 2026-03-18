namespace PolarPet.Scripts.AICore.Gemini
{
    
using System;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Gemini.API;

public class GeminiCore : MonoBehaviour
{
    [Header("Gemini Settings")]
    [Tooltip("Your Gemini API Key")]
    [SerializeField] private string apiKey;

    [Tooltip("gemini-2.5-flash-image or gemini-3-pro-image-preview")]
    [SerializeField] private string model = "gemini-2.5-flash-image";

    private string Endpoint =>
        $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

    public void GenerateImage(string prompt, Action<Texture2D> onDone)
    {
        var request = BuildTextToImageRequest(prompt);
        string json = JsonConvert.SerializeObject(request);

        StartCoroutine(Post(json, onDone));
    }

    public void EditImage(string instruction, Texture2D image, Action<Texture2D> onDone)
    {
        string base64 = Convert.ToBase64String(image.EncodeToPNG());

        var request = BuildEditImageRequest(instruction, base64);
        string json = JsonConvert.SerializeObject(request);

        StartCoroutine(Post(json, onDone));
    }

    /// <summary>
    /// 合成兩個圖片：將衣服圖片合成到北極熊圖片上
    /// </summary>
    public void CompositeImages(string instruction, Texture2D baseImage, Texture2D clothingImage, Action<Texture2D> onDone)
    {
        string base64Base = Convert.ToBase64String(baseImage.EncodeToPNG());
        string base64Clothing = Convert.ToBase64String(clothingImage.EncodeToPNG());

        var request = BuildCompositeImageRequest(instruction, base64Base, base64Clothing);
        string json = JsonConvert.SerializeObject(request);

        StartCoroutine(Post(json, onDone));
    }

    GenerateContentRequest BuildTextToImageRequest(string prompt)
    {
        return new GenerateContentRequest
        {
            contents = new()
            {
                new Content
                {
                    role = "user",
                    parts = new()
                    {
                        new Part { text = prompt }
                    }
                }
            },
            generationConfig = new GenerationConfig
            {
                responseModalities = new() { "IMAGE" },
                imageConfig = new ImageConfig
                {
                    aspectRatio = "1:1"
                }
            }
        };
    }

    GenerateContentRequest BuildEditImageRequest(string instruction, string base64)
    {
        return new GenerateContentRequest
        {
            contents = new()
            {
                new Content
                {
                    role = "user",
                    parts = new()
                    {
                        new Part { text = instruction },
                        new Part
                        {
                            inlineData = new InlineData
                            {
                                mimeType = "image/png",
                                data = base64
                            }
                        }
                    }
                }
            },
            generationConfig = new GenerationConfig
            {
                responseModalities = new() { "IMAGE" }
            }
        };
    }

    GenerateContentRequest BuildCompositeImageRequest(string instruction, string base64Base, string base64Clothing)
    {
        return new GenerateContentRequest
        {
            contents = new()
            {
                new Content
                {
                    role = "user",
                    parts = new()
                    {
                        new Part { text = instruction },
                        new Part
                        {
                            inlineData = new InlineData
                            {
                                mimeType = "image/png",
                                data = base64Base
                            }
                        },
                        new Part
                        {
                            inlineData = new InlineData
                            {
                                mimeType = "image/png",
                                data = base64Clothing
                            }
                        }
                    }
                }
            },
            generationConfig = new GenerationConfig
            {
                responseModalities = new() { "IMAGE" }
            }
        };
    }


    IEnumerator Post(string json, Action<Texture2D> onDone)
    {
        var req = new UnityWebRequest(Endpoint, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();

        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("x-goog-api-key", apiKey);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Gemini Error: {req.error}\n{req.downloadHandler.text}");
            yield break;
        }

        var response = JsonConvert.DeserializeObject<GenerateContentResponse>(req.downloadHandler.text);

        if (response.candidates == null || response.candidates.Count == 0)
        {
            Debug.LogWarning("Gemini returned no candidates. Check API key or model availability.");
            yield break;
        }

        var candidate = response.candidates[0];

        if (candidate.finishReason != "STOP" && !string.IsNullOrEmpty(candidate.finishReason))
        {
            Debug.LogWarning($"Gemini generation finished with reason: {candidate.finishReason}");
        }

        var parts = candidate.content.parts;
        foreach (var p in parts)
        {
            if (p.inlineData != null)
            {
                byte[] bytes = Convert.FromBase64String(p.inlineData.data);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                onDone?.Invoke(tex);
                yield break;
            }
            else if (!string.IsNullOrEmpty(p.text))
            {
                Debug.Log($"Gemini Text Response: {p.text}");
            }
        }

        Debug.LogWarning("Gemini returned no image data in parts.");
    }
}

}