using System;

namespace ElevenLab
{
    [Serializable]
    public class ElevenLabsRequest
    {
        public string text;
        public string model_id = "eleven_multilingual_v2";
    }
}
