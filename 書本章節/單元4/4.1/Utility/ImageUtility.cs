using System;
using UnityEngine;

namespace PolarPet.Scripts.AICore.Utility
{
    public class ImageUtility
    {
        public static Sprite TextureToSprite(Texture2D texture)
        {
            if (texture == null)
            {
                Debug.LogError("TextureToSprite: texture is null");
                return null;
            }

            return Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
        }

        public static Texture2D SpriteToTexture(Sprite sprite)
        {
            if (sprite == null)
            {
                Debug.LogError("SpriteToTexture: sprite is null");
                return null;
            }

            if (!Mathf.Approximately(sprite.rect.width, sprite.texture.width) ||
                !Mathf.Approximately(sprite.rect.height, sprite.texture.height))
            {
                Texture2D newTex = new Texture2D(
                    (int)sprite.rect.width,
                    (int)sprite.rect.height
                );

                Rect rect = sprite.rect;
                Color[] pixels = sprite.texture.GetPixels(
                    (int)rect.x,
                    (int)rect.y,
                    (int)rect.width,
                    (int)rect.height
                );

                newTex.SetPixels(pixels);
                newTex.Apply();
                return newTex;
            }

            return sprite.texture;
        }

        /// <summary>
        /// Texture2D 轉 base64（預設 PNG）。
        /// </summary>
        public static string TextureToBase64(Texture2D texture, bool usePng = true)
        {
            if (texture == null)
            {
                Debug.LogError("TextureToBase64: texture is null");
                return null;
            }

            byte[] bytes = usePng ? texture.EncodeToPNG() : texture.EncodeToJPG();
            if (bytes == null || bytes.Length == 0)
            {
                Debug.LogError("TextureToBase64: encode failed (empty bytes)");
                return null;
            }

            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// base64 轉 Texture2D（常用於 API 回傳圖片）。
        /// </summary>
        public static Texture2D Base64ToTexture(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
            {
                Debug.LogError("Base64ToTexture: base64 is empty");
                return null;
            }

            byte[] bytes = Convert.FromBase64String(base64);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(bytes);
            return tex;
        }
        
        public static string TextureToDataUri(Texture2D texture, bool usePng = true)
        {
            if (texture == null)
            {
                Debug.LogError("TextureToDataUri: texture is null");
                return null;
            }

            byte[] bytes = usePng ? texture.EncodeToPNG() : texture.EncodeToJPG();
            if (bytes == null || bytes.Length == 0)
            {
                Debug.LogError("TextureToDataUri: encode failed (empty bytes)");
                return null;
            }

            string mime = usePng ? "image/png" : "image/jpeg";
            string base64 = Convert.ToBase64String(bytes);
            return $"data:{mime};base64,{base64}";
        }

    }
}