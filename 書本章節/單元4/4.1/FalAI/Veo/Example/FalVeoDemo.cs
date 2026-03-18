using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using FalAI.Veo;

namespace FalAI.Veo.Example
{
    public class FalVeoDemo : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FalVeoCore veo;

        [Header("Video")]
        [SerializeField] private VideoPlayer videoPlayer;

        [Header("Prompt Input")]
        [SerializeField] private InputField promptInput;
        [TextArea(3, 6)]
        [SerializeField] private string defaultPrompt =
            "A cute polar bear desktop pet in a cozy room, waving and smiling. Soft lighting, cinematic.";

        [Header("Options (Optional)")]
        [SerializeField] private string aspectRatio = "16:9";
        [SerializeField] private string duration = "8s";
        [SerializeField] private string resolution = "720p";
        [SerializeField] private bool generateAudio = true;
        [SerializeField] private bool autoFix = true;

        [Header("UI")]
        [SerializeField] private Text statusText;
        [SerializeField] private Button generateButton;

        private void Start()
        {
            if (promptInput != null && string.IsNullOrWhiteSpace(promptInput.text))
                promptInput.text = defaultPrompt;

            if (generateButton != null)
                generateButton.onClick.AddListener(OnGenerateClicked);

            SetStatus("請輸入 Prompt，然後點擊 Generate");
        }

        private void OnGenerateClicked()
        {
            if (veo == null || videoPlayer == null)
            {
                OnError("FalVeoCore 或 VideoPlayer 尚未設定");
                return;
            }

            string prompt = promptInput != null ? promptInput.text : defaultPrompt;

            if (string.IsNullOrWhiteSpace(prompt))
            {
                OnError("Prompt 為空");
                return;
            }

            SetStatus("送出影片生成任務中…");

            var req = new FalVeoRequest
            {
                prompt = prompt.Trim(),
                aspect_ratio = aspectRatio,
                duration = duration,
                resolution = resolution,
                generate_audio = generateAudio,
                auto_fix = autoFix
            };

            veo.GenerateVideoUrl(
                req,
                onDoneUrl: OnVideoUrlReady,
                onError: OnError
            );
        }

        private void OnVideoUrlReady(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                OnError("回傳的 video url 為空");
                return;
            }

            SetStatus("取得影片，準備播放…");

            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = url;
            videoPlayer.playOnAwake = false;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

            videoPlayer.errorReceived += OnVideoError;
            videoPlayer.prepareCompleted += OnPrepared;
            videoPlayer.loopPointReached += OnFinished;

            videoPlayer.Prepare();
        }

        private void OnPrepared(VideoPlayer vp)
        {
            SetStatus("播放中");
            vp.Play();
        }

        private void OnFinished(VideoPlayer vp)
        {
            SetStatus("播放完成");
        }

        private void OnVideoError(VideoPlayer vp, string message)
        {
            OnError("VideoPlayer Error: " + message);
        }

        private void OnError(string error)
        {
            SetStatus("失敗");
            Debug.LogError(error);
        }

        private void SetStatus(string msg)
        {
            if (statusText != null)
                statusText.text = msg;
        }

        private void OnDestroy()
        {
            if (generateButton != null)
                generateButton.onClick.RemoveListener(OnGenerateClicked);

            if (videoPlayer != null)
            {
                videoPlayer.errorReceived -= OnVideoError;
                videoPlayer.prepareCompleted -= OnPrepared;
                videoPlayer.loopPointReached -= OnFinished;
            }
        }
    }
}
