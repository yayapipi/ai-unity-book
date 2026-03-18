namespace PolarPet.Scripts.AICore.Gemini
{
    using System.Collections.Generic;

    namespace Gemini.API
    {
        [System.Serializable]
        public class GenerateContentResponse
        {
            public List<Candidate> candidates;
        }

        [System.Serializable]
        public class Candidate
        {
            public Content content;
            public string finishReason;
        }
    }

}