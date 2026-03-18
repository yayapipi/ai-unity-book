using System;
using System.Collections;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace FalAI.Veo
{
    public class FalVeoCore : MonoBehaviour
    {
        [Header("Fal Settings")]
        [SerializeField] private string falApiKey = "YOUR_FAL_KEY";

        [Header("Veo Settings")]
        [SerializeField] private float pollIntervalSeconds = 1.0f;
        [SerializeField] private float timeoutSeconds = 120f;

        // Endpoints
        private const string SUBMIT_URL = "https://queue.fal.run/fal-ai/veo3.1/fast";
        private const string REQUEST_URL_PREFIX = "https://queue.fal.run/fal-ai/veo3.1/requests/";

        // --------------------------------------------------
        // Public API
        // --------------------------------------------------

        /// <summary>
        /// 生成影片並回傳 video URL（推薦：直接丟給 VideoPlayer.url）
        /// </summary>
        public void GenerateVideoUrl(
            FalVeoRequest request,
            Action<string> onDoneUrl,
            Action<string> onError = null)
        {
            StartCoroutine(GenerateVideoUrlCoroutine(request, onDoneUrl, onError));
        }

        /// <summary>
        /// 生成影片並下載成 mp4，回傳本地檔案路徑
        /// </summary>
        public void GenerateAndDownloadVideo(
            FalVeoRequest request,
            Action<string> onDoneLocalPath,
            Action<string> onError = null,
            string fileName = "fal_veo_output.mp4")
        {
            StartCoroutine(GenerateAndDownloadVideoCoroutine(
                request, onDoneLocalPath, onError, fileName));
        }

        // --------------------------------------------------
        // Coroutines
        // --------------------------------------------------

        private IEnumerator GenerateVideoUrlCoroutine(
            FalVeoRequest request,
            Action<string> onDoneUrl,
            Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(falApiKey))
            {
                onError?.Invoke("Fal API Key is empty.");
                yield break;
            }

            if (request == null || string.IsNullOrWhiteSpace(request.prompt))
            {
                onError?.Invoke("FalVeoRequest is null or prompt is empty.");
                yield break;
            }

            // 1️⃣ Submit
            string requestId;
            {
                string json = JsonConvert.SerializeObject(request);

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

                var submit = JsonConvert.DeserializeObject<FalSubmitResponse>(req.downloadHandler.text);
                requestId = submit?.request_id;

                if (string.IsNullOrWhiteSpace(requestId))
                {
                    onError?.Invoke("Submit response missing request_id.\n" + req.downloadHandler.text);
                    yield break;
                }
            }

            // 2️⃣ Poll Status
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

                var status = JsonConvert.DeserializeObject<FalStatusResponse>(statusReq.downloadHandler.text);

                if (status == null || string.IsNullOrWhiteSpace(status.status))
                {
                    onError?.Invoke("Invalid status response.\n" + statusReq.downloadHandler.text);
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

            // 3️⃣ Get Result
            string videoUrl;
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

                var result = JsonConvert.DeserializeObject<FalVeoResponse>(resultReq.downloadHandler.text);
                videoUrl = result?.video?.url;

                if (string.IsNullOrWhiteSpace(videoUrl))
                {
                    onError?.Invoke("Result missing video.url\n" + resultReq.downloadHandler.text);
                    yield break;
                }
            }

            onDoneUrl?.Invoke(videoUrl);
        }

        private IEnumerator GenerateAndDownloadVideoCoroutine(
            FalVeoRequest request,
            Action<string> onDoneLocalPath,
            Action<string> onError,
            string fileName)
        {
            string videoUrl = null;

            yield return GenerateVideoUrlCoroutine(
                request,
                url => videoUrl = url,
                onError
            );

            if (string.IsNullOrWhiteSpace(videoUrl))
                yield break;

            string localPath = Path.Combine(Application.persistentDataPath, fileName);

            using var videoReq = UnityWebRequest.Get(videoUrl);
            videoReq.downloadHandler = new DownloadHandlerBuffer();
            yield return videoReq.SendWebRequest();

            if (videoReq.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Video download failed: {videoReq.error}");
                yield break;
            }

            File.WriteAllBytes(localPath, videoReq.downloadHandler.data);
            onDoneLocalPath?.Invoke(localPath);
        }
    }
}
