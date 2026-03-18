using System;

namespace ElevenLab
{
    [Serializable]
    public class ElevenLabsResponse
    {
        public bool success;

        public byte[] audioBytes;
        public string contentType;
        public string savedFilePath;

        public long httpStatusCode;
        public string errorMessage;
        public string rawErrorBody;
    }
}