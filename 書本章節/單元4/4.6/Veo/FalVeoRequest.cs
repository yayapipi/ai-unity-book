using System;

namespace FalAI.Veo
{
    [Serializable]
    public class FalVeoRequest
    {
        public string prompt;

        // Optional
        public string aspect_ratio = "16:9";   // "16:9" or "9:16"
        public string duration = "8s";          // "4s" | "6s" | "8s"
        public string resolution = "720p";      // "720p" | "1080p" | "4k"
        public bool generate_audio = true;
        public bool auto_fix = true;

        // Optional advanced
        public string negative_prompt;
        public int? seed;
    }
    
    [Serializable]
    public class FalSubmitResponse
    {
        public string request_id;
    }
    
    [Serializable]
    public class FalStatusResponse
    {
        public string status;   // "IN_PROGRESS" | "COMPLETED" | "FAILED"
        public string error;    // status == FAILED 時才會有
    }
    
}
