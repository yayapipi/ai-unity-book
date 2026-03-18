using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PolarPet.Scripts.AICore.OpenAI;
using ElevenLab;
using Newtonsoft.Json;

/// <summary>
/// PetAIChatController - 寵物AI對話控制器
/// 功能：
/// 1. 點擊Tab鍵可以打開/關閉輸入框的GameObject，並自動對焦InputField
/// 2. 輸入框Submit之後，發送輸入框的內容到AI API，然後清空輸入框的內容並關閉
/// 3. 回傳的內容顯示在PetDialog的SetText裡
/// 4. 等待回傳的時候，北極熊的對話框Text會顯示思考中的消息
/// </summary>
public class PetAIChatController : MonoBehaviour
{
    [Header("組件引用")]
    [Tooltip("輸入框的GameObject（整個輸入框容器）")]
    [SerializeField] private GameObject inputObj;
    
    [Tooltip("InputField組件")]
    [SerializeField] private InputField inputField;
    
    [Tooltip("OpenAICore組件")]
    [SerializeField] private OpenAICore openAICore;
    
    [Tooltip("PetDialog組件")]
    [SerializeField] private PetDialog petDialog;
    
    [Tooltip("ElevenLabsCore組件")]
    [SerializeField] private ElevenLabsCore elevenLabsCore;
    
    [Tooltip("AudioSource組件（用於播放語音）")]
    [SerializeField] private AudioSource audioSource;
    
    [Header("設置")]
    [Tooltip("思考中的消息文本")]
    [SerializeField] private string thinkingMessage = "思考中...";
    
    [Tooltip("系統提示詞（可選）")]
    [TextArea(2, 6)]
    [SerializeField] private string systemHint = 
        "你是一個遊戲中的NPC，說話要簡短、有個性、帶一點神秘感。";

    [Tooltip("情緒滑桿")]
    [SerializeField] private Slider emotionSlider;
    
    [Header("RAG 設置")]
    [Tooltip("Vector Store ID（用於 RAG 功能，留空則不使用 RAG）")]
    [SerializeField] private string vectorStoreId = "";
    
    [Tooltip("RAG 檢索結果的最大數量")]
    [SerializeField] private int maxNumResults = 3;
    
    // 私有变量
    private bool isInputActive = false;
    private EventSystem eventSystem;
    
    // 對話歷史記錄
    private List<string> historyChat = new List<string>();
    
    private void Awake()
    {
        // 如果沒有指定EventSystem，嘗試查找
        eventSystem = FindObjectOfType<EventSystem>();
        if (eventSystem == null)
        {
            Debug.LogWarning("PetAIChatController: 未找到EventSystem，自動對焦功能可能無法正常工作。");
        }
        
        // 初始化時隱藏輸入框
        if (inputObj != null)
        {
            inputObj.SetActive(false);
            isInputActive = false;
        }
        
        // 如果沒有指定inputField，嘗試從inputObj中查找
        if (inputField == null && inputObj != null)
        {
            inputField = inputObj.GetComponentInChildren<InputField>();
        }
        
        // 如果沒有指定openAICore，嘗試查找
        if (openAICore == null)
        {
            openAICore = FindObjectOfType<OpenAICore>();
        }
        
        // 如果沒有指定petDialog，嘗試查找
        if (petDialog == null)
        {
            petDialog = FindObjectOfType<PetDialog>();
        }
        
        // 如果沒有指定elevenLabsCore，嘗試查找
        if (elevenLabsCore == null)
        {
            elevenLabsCore = FindObjectOfType<ElevenLabsCore>();
        }
        
        // 如果沒有指定audioSource，嘗試從當前GameObject或子對象查找
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = GetComponentInChildren<AudioSource>();
            }
            // 如果還是沒有找到，創建一個新的AudioSource
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
    }
    
    private void Start()
    {
        if (inputField != null)
        {
            inputField.onSubmit.AddListener(OnInputFieldSubmit);
        }
    }
    
    private void Update()
    {
        // 監聽Tab鍵
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleInputField();
        }
    }
    
    /// <summary>
    /// 切換輸入框的顯示/隱藏狀態
    /// </summary>
    private void ToggleInputField()
    {
        if (inputObj == null)
        {
            Debug.LogWarning("PetAIChatController: InputObj未設置！");
            return;
        }
        
        isInputActive = !isInputActive;
        inputObj.SetActive(isInputActive);
        
        // 如果打開輸入框，自動對焦
        if (isInputActive && inputField != null)
        {
            FocusInputField();
        }
    }
    
    /// <summary>
    /// 自動對焦InputField
    /// </summary>
    private void FocusInputField()
    {
        if (inputField == null)
        {
            return;
        }
        
        // 使用EventSystem來激活InputField
        if (eventSystem != null)
        {
            eventSystem.SetSelectedGameObject(inputField.gameObject);
        }
        
        // 激活InputField
        inputField.ActivateInputField();
        inputField.Select();
    }
    
    /// <summary>
    /// InputField提交時的回調（onSubmit事件，當按下Enter時觸發）
    /// </summary>
    private void OnInputFieldSubmit(string text)
    {
        // 如果InputField被取消（按了Esc），則不處理
        if (inputField != null && inputField.wasCanceled)
        {
            return;
        }
        
        // 獲取輸入內容並去除首尾空格
        string userText = text?.Trim();
        
        // 如果輸入為空，不處理
        if (string.IsNullOrEmpty(userText))
        {
            return;
        }
        
        // 發送到AI
        SendToAI(userText);
        
        // 清空輸入框
        if (inputField != null)
        {
            inputField.text = "";
        }
        
        // 關閉輸入框
        if (inputObj != null)
        {
            inputObj.SetActive(false);
            isInputActive = false;
        }
    }
    
    /// <summary>
    /// 發送消息到AI
    /// </summary>
    private void SendToAI(string userText)
    {
        if (openAICore == null)
        {
            Debug.LogError("PetAIChatController: OpenAICore未設置！");
            return;
        }
        
        if (petDialog == null)
        {
            Debug.LogError("PetAIChatController: PetDialog未設置！");
            return;
        }
        
        // 組合prompt（參考ChatGPTDemo的實現）
        // 先組合歷史對話
        string historyPrompt = "";
        if (historyChat.Count > 0)
        {
            historyPrompt = string.Join("\n", historyChat) + "\n";
        }
        
        // 組合完整的prompt（包含歷史記錄）
        string prompt = string.IsNullOrEmpty(systemHint)
            ? historyPrompt + $"[User]\n{userText}\n\n[Assistant]\n"
            : $"[System]\n{systemHint}\n\n" + historyPrompt + $"[User]\n{userText}\n\n[Assistant]\n";
        
        // 顯示思考中的消息，禁用自動隱藏（等待回覆時不要消失）
        petDialog.SetText(thinkingMessage, enableAutoHide: false);
        
        // 發送到AI（如果 vectorStoreId 不為空，則使用 RAG 功能）
        string vsStoreId = string.IsNullOrWhiteSpace(vectorStoreId) ? null : vectorStoreId;
        openAICore.SendChat(
            prompt, 
            (reply) =>
            {
                // 處理AI回覆
                if (string.IsNullOrEmpty(reply))
                {
                    reply = "(沒有收到文字回應，可能是模型回傳格式或解析位置需要調整)";
                }
                
                Debug.Log("[OpenAI Reply] " + reply);

                // 把AI回傳的Json文字格式傳換成C#的Class方便後續調用
                var petResponse = JsonConvert.DeserializeObject<AIPetResponse>(reply);
                
                // 如果解析成功，將用戶輸入和AI回覆添加到歷史記錄
                if (petResponse != null && !string.IsNullOrEmpty(petResponse.content))
                {
                    historyChat.Add($"[User]\n{userText}\n\n[Assistant]\n{petResponse.content}");
                }
                
                // 顯示在PetDialog中，恢復自動隱藏（使用默認設置）
                string displayContent = petResponse?.content ?? reply;
                petDialog.SetText(displayContent);
                
                // 調用ElevenLabsCore進行語音合成
                if (petResponse != null && !string.IsNullOrEmpty(petResponse.content))
                {
                    SpeakWithElevenLabs(petResponse.content);
                }

                // 更新情緒滑桿
                if (petResponse != null)
                {
                    if(petResponse.emotion == "good"){
                        emotionSlider.value +=1;
                    }
                 
                    if(petResponse.emotion == "bad"){
                        emotionSlider.value -=1;
                    }
                }
            },
            vectorStoreId: vsStoreId,
            maxNumResults: maxNumResults
        );
    }
    
    /// <summary>
    /// 手動打開輸入框（外部調用接口）
    /// </summary>
    public void OpenInputField()
    {
        if (inputObj != null && !isInputActive)
        {
            ToggleInputField();
        }
    }
    
    /// <summary>
    /// 手動關閉輸入框（外部調用接口）
    /// </summary>
    public void CloseInputField()
    {
        if (inputObj != null && isInputActive)
        {
            ToggleInputField();
        }
    }
    
    /// <summary>
    /// 使用ElevenLabsCore進行語音合成
    /// </summary>
    private void SpeakWithElevenLabs(string text)
    {
        if (elevenLabsCore == null)
        {
            Debug.LogWarning("PetAIChatController: ElevenLabsCore未設置，跳過語音合成。");
            return;
        }
        
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("PetAIChatController: 文字為空，跳過語音合成。");
            return;
        }
        
        // 創建ElevenLabs請求
        var request = new ElevenLabsRequest
        {
            text = text,
            model_id = "eleven_multilingual_v2"
        };
        
        // 調用ElevenLabsCore的Speak方法
        elevenLabsCore.Speak(
            request,
            onSuccess: (audioClip) =>
            {
                // 語音合成成功，播放音頻
                if (audioSource != null && audioClip != null)
                {
                    audioSource.clip = audioClip;
                    audioSource.Play();
                    Debug.Log("PetAIChatController: 語音播放開始");
                }
                else
                {
                    Debug.LogWarning("PetAIChatController: AudioSource或AudioClip為空，無法播放語音。");
                }
            },
            onError: (error) =>
            {
                Debug.LogError($"PetAIChatController: ElevenLabs語音合成失敗: {error}");
            },
            onProgress: (progress) =>
            {
                // 可選：顯示語音合成進度
                // Debug.Log($"PetAIChatController: 語音合成進度: {progress * 100:F1}%");
            }
        );
    }
    
    private void OnDestroy()
    {
        // 取消註冊事件
        if (inputField != null)
        {
            inputField.onSubmit.RemoveListener(OnInputFieldSubmit);
        }
    }
}

