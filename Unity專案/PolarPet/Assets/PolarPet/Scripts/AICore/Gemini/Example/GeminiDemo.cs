using PolarPet.Scripts.AICore.Utility;

namespace PolarPet.Scripts.AICore.Gemini.Example
{
    using UnityEngine;
    using UnityEngine.UI;

    public class GeminiDemo : MonoBehaviour
    {
        [Header("References")]
        public GeminiCore gemini;
        public Image image;
        public string prompt = "A cute pastel style cat wizard illustration, hand-drawn, soft colors";

        void Start()
        {
            gemini.GenerateImage(prompt, OnImageGenerated);
        }

        void OnImageGenerated(Texture2D texture)
        {
            // Texture → Sprite
            Sprite sprite = ImageUtility.TextureToSprite(texture);

            image.sprite = sprite;
            image.preserveAspect = true;
        }
    }

}