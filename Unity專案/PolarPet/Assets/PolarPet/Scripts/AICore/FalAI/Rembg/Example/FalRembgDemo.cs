using UnityEngine;

namespace PolarPet.Scripts.AICore.FalAI.Example
{
    using UnityEngine.UI;
    using FalAI.Rembg;
    using PolarPet.Scripts.AICore.Utility;

    public class FalRembgDemo : MonoBehaviour
    {
        [Header("References")] [SerializeField]
        private FalRembgCore rembg;

        [Header("Input (擇一)")] [Tooltip("要去背的輸入 Sprite（若有設定，會優先使用）")] [SerializeField]
        private Sprite inputSprite;

        [Tooltip("要去背的圖片 URL（當 inputSprite 沒設定時使用）")] [SerializeField]
        private string imageUrl = "https://your-domain.com/your-image.png";

        [Header("UI")] [SerializeField] private Image previewImage;
        [SerializeField] private Text statusText;

        private void Start()
        {
            if (rembg == null)
            {
                SetStatus("FalRembgCore 尚未設定");
                Debug.LogError("FalRembgDemo: rembg is null");
                return;
            }

            if (previewImage == null)
            {
                SetStatus("Preview Image 尚未設定");
                Debug.LogError("FalRembgDemo: previewImage is null");
                return;
            }

            // 優先用 Sprite，其次用 URL
            if (inputSprite != null)
            {
                Texture2D inputTexture = ImageUtility.SpriteToTexture(inputSprite);
                if (inputTexture == null)
                {
                    SetStatus("無法從 Sprite 取得 Texture2D");
                    Debug.LogError("FalRembgDemo: failed to convert sprite to texture");
                    return;
                }

                SetStatus("送出去背任務中（Sprite）…");

                rembg.RemoveBackground(
                    ImageUtility.TextureToDataUri(inputTexture),
                    onDone: OnImageReady,
                    onError: OnError,
                    cropToBbox: true
                );

                return;
            }

            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                SetStatus("送出去背任務中（URL）…");

                rembg.RemoveBackground(
                    imageUrl.Trim(),
                    onDone: OnImageReady,
                    onError: OnError,
                    cropToBbox: true
                );

                return;
            }

            SetStatus("請輸入 Sprite 或 URL");
            Debug.LogError("FalRembgDemo: no input (sprite/url) provided.");
        }

        private void OnImageReady(Texture2D texture)
        {
            if (texture == null)
            {
                OnError("回傳的 Texture 為 null");
                return;
            }

            Sprite sprite = FalRembgCore.TextureToSprite(texture);

            previewImage.sprite = sprite;
            previewImage.preserveAspect = true;

            SetStatus("完成");
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
    }
}