namespace PolarPet.Scripts.AICore.OpenAI
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class ChatGPTResponse
    {
        public string id { get; set; }

        [JsonProperty("object")]
        public string @object { get; set; }  

        public string status { get; set; }
        public string model { get; set; }

        public List<ChatGPTOutput> output { get; set; }
    }

    public class ChatGPTOutput
    {
        public string id { get; set; }
        public string type { get; set; } 
        public string role { get; set; } 
        public string status { get; set; }
        public List<ChatGPTContent> content { get; set; }
        public List<object> summary { get; set; }
    }

    public class ChatGPTContent
    {
        public string type { get; set; } 
        public string text { get; set; }
        public List<object> annotations { get; set; }
        public List<object> logprobs { get; set; }
    }

    // OpenAI API 錯誤響應結構
    public class OpenAIErrorResponse
    {
        public OpenAIError error { get; set; }
    }

    public class OpenAIError
    {
        public string message { get; set; }
        public string type { get; set; }
        public string param { get; set; }
        public string code { get; set; }
    }
}