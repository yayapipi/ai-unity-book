using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace ElevenLab
{
    public class ElevenLabsCore : MonoBehaviour
    {
        [Header("Auth")]
        [SerializeField] private string xiApiKey = "YOUR_XI_API_KEY";
        [SerializeField] private string voiceId = "JBFqnCBsd6RMkjVDRZzb";

        private const string BASE_URL = "https://api.elevenlabs.io/v1/text-to-speech/";

        public void Speak(
            ElevenLabsRequest request,
            Action<AudioClip> onSuccess,
            Action<string> onError = null,
            Action<float> onProgress = null)
        {
            StartCoroutine(SpeakCoroutine(request, onSuccess, onError, onProgress));
        }

        private IEnumerator SpeakCoroutine(
            ElevenLabsRequest request,
            Action<AudioClip> onSuccess,
            Action<string> onError,
            Action<float> onProgress)
        {
            string url = $"{BASE_URL}{voiceId}?output_format=mp3_44100_128";

            string json = JsonConvert.SerializeObject(request);
            byte[] body = Encoding.UTF8.GetBytes(json);

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();

            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("xi-api-key", xiApiKey);

            var operation = req.SendWebRequest();
            
            // 更新進度
            while (!operation.isDone)
            {
                float progress = operation.progress;
                onProgress?.Invoke(progress);
                yield return null;
            }
            
            onProgress?.Invoke(1f);

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(req.error);
                yield break;
            }

            byte[] audioBytes = req.downloadHandler.data;
            if (audioBytes == null || audioBytes.Length == 0)
            {
                onError?.Invoke("Audio bytes empty");
                yield break;
            }

            string path = Path.Combine(
                Application.persistentDataPath,
                $"elevenlabs_{DateTime.Now.Ticks}.mp3"
            );

            File.WriteAllBytes(path, audioBytes);

            yield return LoadAudioClip(path, onSuccess, onError, onProgress);
        }

        private IEnumerator LoadAudioClip(
            string path,
            Action<AudioClip> onSuccess,
            Action<string> onError,
            Action<float> onProgress)
        {
            using var req =
                UnityWebRequestMultimedia.GetAudioClip(
                    "file://" + path,
                    AudioType.MPEG
                );

            var operation = req.SendWebRequest();
            
            // 更新加載進度（從 50% 到 100%，因為下載已經完成）
            while (!operation.isDone)
            {
                float progress = 0.5f + (operation.progress * 0.5f);
                onProgress?.Invoke(progress);
                yield return null;
            }
            
            onProgress?.Invoke(1f);

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(req.error);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
            onSuccess?.Invoke(clip);
        }
    }
}
