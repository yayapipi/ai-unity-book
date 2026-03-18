namespace PolarPet.Scripts.AICore.FalAI
{
    using System;
    using System.Collections;
    using System.Text;
    using Newtonsoft.Json;
    using UnityEngine;
    using UnityEngine.Networking;

    namespace FalAI.Rembg
    {
        public class FalRembgCore : MonoBehaviour
        {
        [Header("Fal Settings")]
        [Tooltip("Fal API Key")]
        [SerializeField] private string falApiKey = "YOUR_FAL_KEY";

        [Header("Rembg Settings")]
        [Tooltip("輪詢間隔（秒）")]
        [SerializeField] private float pollIntervalSeconds = 1.0f;

        [Tooltip("最久等待多久（秒），避免卡死")]
        [SerializeField] private float timeoutSeconds = 60f;

        // Fal queue endpoints
        private const string SUBMIT_URL = "https://queue.fal.run/fal-ai/imageutils/rembg";
        private const string REQUEST_URL_PREFIX = "https://queue.fal.run/fal-ai/imageutils/requests/";

        // -------------------------
        // Public API
        // -------------------------

        public void RemoveBackground(string imageUrl, Action<Texture2D> onDone, Action<string> onError = null,
            bool cropToBbox = true)
        {
            StartCoroutine(RemoveBackgroundCoroutine(imageUrl, onDone, onError, cropToBbox));
        }

        public IEnumerator RemoveBackgroundCoroutine(string imageUrl, Action<Texture2D> onDone, Action<string> onError = null,
            bool cropToBbox = true)
        {
            if (string.IsNullOrWhiteSpace(falApiKey))
            {
                onError?.Invoke("Fal API Key is empty.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                onError?.Invoke("imageUrl is empty.");
                yield break;
            }

            // 1) Submit
            string requestId;
            {
                var reqData = new FalRembgRequest
                {
                    image_url = imageUrl,
                };

                string json = JsonConvert.SerializeObject(reqData);

                using var req = new UnityWebRequest(SUBMIT_URL, "POST");
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", "Key " + falApiKey);
                req.SetRequestHeader("Content-Type", "application/json");

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Submit failed: {req.error}\n{req.downloadHandler.text}");
                    yield break;
                }

                FalSubmitResponse submit;
                try
                {
                    submit = JsonConvert.DeserializeObject<FalSubmitResponse>(req.downloadHandler.text);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Submit parse failed: {e.Message}\n{req.downloadHandler.text}");
                    yield break;
                }

                requestId = submit?.request_id;
                if (string.IsNullOrWhiteSpace(requestId))
                {
                    onError?.Invoke("Submit response missing request_id.\n" + req.downloadHandler.text);
                    yield break;
                }
            }

            // 2) Poll Status
            float startTime = Time.realtimeSinceStartup;
            while (true)
            {
                if (Time.realtimeSinceStartup - startTime > timeoutSeconds)
                {
                    onError?.Invoke($"Timeout after {timeoutSeconds}s. request_id={requestId}");
                    yield break;
                }

                string statusUrl = REQUEST_URL_PREFIX + requestId + "/status";
                using var statusReq = UnityWebRequest.Get(statusUrl);
                statusReq.downloadHandler = new DownloadHandlerBuffer();
                statusReq.SetRequestHeader("Authorization", "Key " + falApiKey);

                yield return statusReq.SendWebRequest();

                if (statusReq.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Status failed: {statusReq.error}\n{statusReq.downloadHandler.text}");
                    yield break;
                }

                FalStatusResponse status;
                try
                {
                    status = JsonConvert.DeserializeObject<FalStatusResponse>(statusReq.downloadHandler.text);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Status parse failed: {e.Message}\n{statusReq.downloadHandler.text}");
                    yield break;
                }

                if (status == null || string.IsNullOrWhiteSpace(status.status))
                {
                    onError?.Invoke("Status response invalid.\n" + statusReq.downloadHandler.text);
                    yield break;
                }

                if (status.status == "COMPLETED")
                    break;

                if (status.status == "FAILED")
                {
                    onError?.Invoke("Fal job failed: " + (status.error ?? "(no error message)"));
                    yield break;
                }

                yield return new WaitForSeconds(pollIntervalSeconds);
            }

            // 3) Get Result
            string resultImageUrl;
            {
                string resultUrl = REQUEST_URL_PREFIX + requestId;
                using var resultReq = UnityWebRequest.Get(resultUrl);
                resultReq.downloadHandler = new DownloadHandlerBuffer();
                resultReq.SetRequestHeader("Authorization", "Key " + falApiKey);

                yield return resultReq.SendWebRequest();

                if (resultReq.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Result failed: {resultReq.error}\n{resultReq.downloadHandler.text}");
                    yield break;
                }

                FalRembgResult result;
                try
                {
                    result = JsonConvert.DeserializeObject<FalRembgResult>(resultReq.downloadHandler.text);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Result parse failed: {e.Message}\n{resultReq.downloadHandler.text}");
                    yield break;
                }

                resultImageUrl = result?.image?.url;
                if (string.IsNullOrWhiteSpace(resultImageUrl))
                {
                    onError?.Invoke("Result missing image.url\n" + resultReq.downloadHandler.text);
                    yield break;
                }
            }

            // 4) Download Image Texture
            using (var texReq = UnityWebRequestTexture.GetTexture(resultImageUrl))
            {
                yield return texReq.SendWebRequest();

                if (texReq.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Texture download failed: {texReq.error}\n{texReq.downloadHandler.text}");
                    yield break;
                }

                var tex = DownloadHandlerTexture.GetContent(texReq);
                onDone?.Invoke(tex);
            }
        }

        // -------------------------
        // Helpers (Optional)
        // -------------------------

        public static Sprite TextureToSprite(Texture2D texture)
        {
            if (texture == null) return null;
            return Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }
    }
}
}

