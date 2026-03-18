namespace PolarPet.Scripts.AICore.OpenAI
{
    [System.Serializable]
    public class ChatGPTRequest
    {
        public string model;
        public string input;

        // ⭐ 新增
        public Tool[] tools;
        public string[] include;
    }

    [System.Serializable]
    public class Tool
    {
        public string type;
        public string[] vector_store_ids;
        public int max_num_results;
    }
}
