using PolarPet.Scripts.AICore.Utility;

namespace PolarPet.Scripts.AICore.Gemini.Example
{
    using UnityEngine;
    using UnityEngine.UI;

    public class GeminiEditDemo : MonoBehaviour
    {
        [Header("References")]
        public GeminiCore gemini;
        public Image outputImage;  

        [Header("Input")]
        public Sprite inputSprite; 

        [Header("Edit Prompt")]
        [TextArea(2, 6)]
        public string instruction =
            "Keep everything the same, but add a small golden star floating above the character's head.";

        void Start()
        {
            Texture2D inputTexture = ImageUtility.SpriteToTexture(inputSprite);
            gemini.EditImage(instruction, inputTexture, OnEdited);
        }

        void OnEdited(Texture2D editedTexture)
        {
            // Texture → Sprite → UI.Image
            Sprite resultSprite = ImageUtility.TextureToSprite(editedTexture);

            outputImage.sprite = resultSprite;
            outputImage.preserveAspect = true;
        }
    }

}