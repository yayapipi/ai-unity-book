using System;

namespace FalAI.Veo
{
    [Serializable]
    public class FalVeoResponse
    {
        public VeoVideo video;
    }

    [Serializable]
    public class VeoVideo
    {
        public string url;
        // 有些模型會補這些欄位，先留著不傷身
        public string content_type;
        public string file_name;
        public long file_size;
    }
}
