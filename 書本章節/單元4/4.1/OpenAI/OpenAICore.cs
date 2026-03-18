using System;
using System.Collections;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace PolarPet.Scripts.AICore.OpenAI
{
    public class OpenAICore : MonoBehaviour
    {
        [Header("OpenAI")]
        [SerializeField] private string apiKey;
        [SerializeField] private string model = "gpt-5-nano";
        private const string Endpoint = "https://api.openai.com/v1/responses";

        /// <summary>
        /// 一般聊天：vectorStoreId = null/empty
        /// RAG聊天：vectorStoreId = "vs_xxx"
        /// </summary>
        public void SendChat(
            string prompt,
            Action<string> onDone = null,
            string vectorStoreId = null,
            int maxNumResults = 3,
            Action<string> onRetrievedRawJson = null // 可選：回傳檢索結果（原始 JSON，方便你先 debug）
        )
        {
            StartCoroutine(SendCoroutine(prompt, onDone, vectorStoreId, maxNumResults, onRetrievedRawJson));
        }

        private IEnumerator SendCoroutine(
            string prompt,
            Action<string> onDone,
            string vectorStoreId,
            int maxNumResults,
            Action<string> onRetrievedRawJson
        )
        {
            // 1) 準備 Request
            var requestData = new ChatGPTRequest
            {
                model = model,
                input = prompt
            };

            // 2) 如果有 vectorStoreId，就掛上 file_search 工具（A 方法 RAG）
            if (!string.IsNullOrWhiteSpace(vectorStoreId))
            {
                requestData.tools = new[]
                {
                    new Tool
                    {
                        type = "file_search",
                        vector_store_ids = new[] { vectorStoreId },
                        max_num_results = Mathf.Max(1, maxNumResults)
                    }
                };

                // Debug：把檢索到的 chunks 也帶回來
                requestData.include = new[]
                {
                    "file_search_call.results"
                };
            }

            // 3) 送出
            string json = JsonConvert.SerializeObject(requestData);
            using var request = new UnityWebRequest(Endpoint, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string errorMessage = "請求失敗";
                string responseText = request.downloadHandler.text;
                
                // 嘗試解析錯誤響應
                try
                {
                    var errorResponse = JsonConvert.DeserializeObject<OpenAIErrorResponse>(responseText);
                    if (errorResponse?.error != null)
                    {
                        errorMessage = errorResponse.error.message ?? errorMessage;
                        Debug.LogError($"[OpenAI Error] {errorResponse.error.type}: {errorMessage}");
                    }
                    else
                    {
                        Debug.LogError($"[OpenAI Error] {request.error}\n{responseText}");
                    }
                }
                catch
                {
                    Debug.LogError($"[OpenAI Error] {request.error}\n{responseText}");
                }
                
                // 將錯誤信息傳遞給回調，讓上層處理
                onDone?.Invoke(null);
                yield break;
            }

            // 4) 解析回應
            var response = JsonConvert.DeserializeObject<ChatGPTResponse>(request.downloadHandler.text);

            // 5) 抽出 AI 最終輸出文字（你的原本邏輯保留）
            string text = response?.output?
                .FirstOrDefault(o => o.type == "message")?.content?
                .FirstOrDefault(c => c.type == "output_text")?.text;

            // 6) 如果你想拿檢索結果（chunks）做 debug/UI，這裡把原始 JSON 回傳
            // 注意：不同 SDK/回應格式可能會把 include 結果掛在不同欄位，
            // 你先拿 raw json 看結構最穩。
            if (onRetrievedRawJson != null && !string.IsNullOrWhiteSpace(vectorStoreId))
            {
                // 直接回傳整包，最不會踩格式差異
                onRetrievedRawJson.Invoke(request.downloadHandler.text);
            }

            onDone?.Invoke(text);
        }
    }
}
