namespace PolarPet.Scripts.AICore.Gemini
{
    using System.Collections.Generic;

    namespace Gemini.API
    {
        [System.Serializable]
        public class GenerateContentRequest
        {
            public List<Content> contents;
            public GenerationConfig generationConfig;
        }

        [System.Serializable]
        public class Content
        {
            public string role;
            public List<Part> parts;
        }

        [System.Serializable]
        public class Part
        {
            public string text;
            public InlineData inlineData;
        }

        [System.Serializable]
        public class InlineData
        {
            public string mimeType;
            public string data;
        }

        [System.Serializable]
        public class GenerationConfig
        {
            public List<string> responseModalities;
            public ImageConfig imageConfig;
        }

        [System.Serializable]
        public class ImageConfig
        {
            public string aspectRatio;
        }
    }

}