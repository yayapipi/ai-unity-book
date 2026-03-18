using UnityEngine;

namespace PolarPet.Scripts.AICore.OpenAI.Example
{
    using UnityEngine;
    using UnityEngine.UI;

    public class ChatGPTDemo : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private OpenAICore openAI;

        [Header("UI")]
        [SerializeField] private InputField inputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private Text outputText;

        [Header("Optional")]
        [TextArea(2, 6)]
        [SerializeField] private string systemHint =
            "你是一個遊戲中的 NPC，說話要簡短、有個性、帶一點神祕感。";

        private void Awake()
        {
            if (sendButton != null)
                sendButton.onClick.AddListener(OnClickSend);

            if (outputText != null)
                outputText.text = "";
        }

        private void Start()
        {
            // 如果你不想一開始就跑，把這段刪掉
            Send("你好，請自我介紹一下。");
        }

        private void OnDestroy()
        {
            if (sendButton != null)
                sendButton.onClick.RemoveListener(OnClickSend);
        }

        private void OnClickSend()
        {
            if (inputField == null) return;

            string userText = inputField.text?.Trim();
            if (string.IsNullOrEmpty(userText)) return;

            Send(userText);
            inputField.text = "";
        }

        private void Send(string userText)
        {
            if (openAI == null)
            {
                Debug.LogError("ChatGPTDemo: OpenAICore reference is missing.");
                return;
            }

            // 把 systemHint 跟 userText 合併成一個 prompt（簡單版）
            // 書裡可以先這樣寫，之後再進階到 messages/roles 的架構
            string prompt =
                $"[System]\n{systemHint}\n\n[User]\n{userText}\n\n[Assistant]\n";

            SetOutput("...思考中（等一下下）");

            openAI.SendChat(prompt, (reply) =>
            {
                if (string.IsNullOrEmpty(reply))
                    reply = "(沒有收到文字回應，可能是模型回傳格式或解析位置需要調整)";

                Debug.Log("[OpenAI Reply] " + reply);
                SetOutput(reply);
            });
        }

        private void SetOutput(string text)
        {
            if (outputText != null)
                outputText.text = text;
        }
    }
}