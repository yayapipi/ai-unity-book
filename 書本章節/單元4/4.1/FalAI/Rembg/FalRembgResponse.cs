namespace PolarPet.Scripts.AICore.FalAI
{
    using System;

    namespace FalAI.Rembg
    {
        /// <summary>
        /// POST submit 後回傳的資訊（重點是 request_id）
        /// </summary>
        [Serializable]
        public class FalSubmitResponse
        {
            public string request_id;
        }

        /// <summary>
        /// GET status 回傳（輪詢用）
        /// </summary>
        [Serializable]
        public class FalStatusResponse
        {
            public string status; // IN_PROGRESS / COMPLETED / FAILED
            public string error;  // 有些情況會有（不一定）
        }

        /// <summary>
        /// GET result 回傳（真正結果）
        /// </summary>
        [Serializable]
        public class FalRembgResult
        {
            public FalRembgImage image;
        }

        [Serializable]
        public class FalRembgImage
        {
            public string url;
            public string content_type;
            public string file_name;
            public long file_size;
            public int width;
            public int height;
        }
    }

}