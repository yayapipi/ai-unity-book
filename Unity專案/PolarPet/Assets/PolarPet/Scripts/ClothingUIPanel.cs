using UnityEngine;
using UnityEngine.UI;
using PolarPet.Scripts.AICore.Gemini;
using PolarPet.Scripts.AICore.Utility;
using PolarPet.Scripts.AICore.FalAI.FalAI.Rembg;

/// <summary>
/// 衣服UI面板控制器：
/// 1. 包含關閉按鈕、3個不同的衣服按鈕、北極熊的圖片、Loading的Mask圖片
/// 2. 當點擊其中一個衣服按鈕時，進行圖片編輯，把北極熊和衣服的圖片進行合成
/// 3. 圖片生成時顯示Loading Mask，生成後關閉
/// </summary>
public class ClothingUIPanel : MonoBehaviour
{
    [Header("UI組件引用")]
    [Tooltip("UI面板的根物件（整個面板）")]
    [SerializeField] private GameObject panelObject;

    [Tooltip("關閉按鈕")]
    [SerializeField] private Button closeButton;

    [Tooltip("3個不同的衣服按鈕")]
    [SerializeField] private Button[] clothingButtons = new Button[3];

    [Tooltip("北極熊的圖片（Image組件）")]
    [SerializeField] private Image polarBearImage;

    [Tooltip("Loading的Mask圖片（GameObject，用於顯示/隱藏）")]
    [SerializeField] private GameObject loadingMask;

    [Header("AI核心引用")]
    [Tooltip("GeminiCore組件")]
    [SerializeField] private GeminiCore geminiCore;

    [Tooltip("FalRembgCore組件（用於去背）")]
    [SerializeField] private FalRembgCore falRembgCore;

    [Header("衣服設定")]
    [Tooltip("3個不同的衣服Sprite（對應3個按鈕）")]
    [SerializeField] private Sprite[] clothingSprites = new Sprite[3];

    [Tooltip("圖片編輯的提示詞模板（{0}會被替換為衣服描述）")]
    [TextArea(2, 6)]
    [SerializeField] private string editInstructionTemplate = 
        "Keep everything the same, but make the polar bear wear this clothing item: {0}. The clothing should fit naturally on the polar bear's body.";

    [Tooltip("衣服描述（對應3個衣服）")]
    [SerializeField] private string[] clothingDescriptions = new string[3]
    {
        "a red winter jacket",
        "a blue sweater",
        "a green scarf"
    };

    // 原始北極熊Sprite（用於恢復）
    private Sprite originalPolarBearSprite;

    private void Awake()
    {
        // 初始化時隱藏面板
        if (panelObject != null)
        {
            panelObject.SetActive(false);
        }

        // 隱藏Loading Mask
        if (loadingMask != null)
        {
            loadingMask.SetActive(false);
        }

        // 保存原始北極熊Sprite
        if (polarBearImage != null && polarBearImage.sprite != null)
        {
            originalPolarBearSprite = polarBearImage.sprite;
        }

        // 設置關閉按鈕
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }

        // 設置衣服按鈕
        for (int i = 0; i < clothingButtons.Length && i < clothingSprites.Length; i++)
        {
            int index = i; // 閉包變量
            if (clothingButtons[i] != null)
            {
                clothingButtons[i].onClick.AddListener(() => OnClothingButtonClicked(index));
            }
        }

        // 如果沒有指定 geminiCore，嘗試查找
        if (geminiCore == null)
        {
            geminiCore = FindObjectOfType<GeminiCore>();
        }

        // 如果沒有指定 falRembgCore，嘗試查找
        if (falRembgCore == null)
        {
            falRembgCore = FindObjectOfType<FalRembgCore>();
        }
    }

    /// <summary>
    /// 打開UI面板
    /// </summary>
    public void OpenPanel()
    {
        if (panelObject != null)
        {
            panelObject.SetActive(true);
        }
    }

    /// <summary>
    /// 關閉UI面板
    /// </summary>
    public void ClosePanel()
    {
        if (panelObject != null)
        {
            panelObject.SetActive(false);
        }

        // 關閉Loading Mask
        if (loadingMask != null)
        {
            loadingMask.SetActive(false);
        }
    }

    /// <summary>
    /// 當點擊衣服按鈕時調用
    /// </summary>
    private void OnClothingButtonClicked(int clothingIndex)
    {
        if (clothingIndex < 0 || clothingIndex >= clothingSprites.Length)
        {
            Debug.LogWarning($"ClothingUIPanel: 無效的衣服索引 {clothingIndex}");
            return;
        }

        if (geminiCore == null)
        {
            Debug.LogError("ClothingUIPanel: GeminiCore 未設置！");
            return;
        }

        if (polarBearImage == null || polarBearImage.sprite == null)
        {
            Debug.LogError("ClothingUIPanel: 北極熊圖片未設置！");
            return;
        }

        // 顯示Loading Mask
        if (loadingMask != null)
        {
            loadingMask.SetActive(true);
        }

        // 禁用所有按鈕，防止重複點擊
        SetButtonsInteractable(false);

        // 獲取當前北極熊的Texture2D
        Texture2D currentTexture = ImageUtility.SpriteToTexture(polarBearImage.sprite);

        if (currentTexture == null)
        {
            Debug.LogError("ClothingUIPanel: 無法將Sprite轉換為Texture2D！");
            // 恢復按鈕狀態
            SetButtonsInteractable(true);
            if (loadingMask != null)
            {
                loadingMask.SetActive(false);
            }
            return;
        }

        // 獲取衣服的Texture2D
        Texture2D clothingTexture = null;
        if (clothingIndex < clothingSprites.Length && clothingSprites[clothingIndex] != null)
        {
            clothingTexture = ImageUtility.SpriteToTexture(clothingSprites[clothingIndex]);
        }

        if (clothingTexture == null)
        {
            Debug.LogError($"ClothingUIPanel: 無法獲取衣服圖片（索引 {clothingIndex}）！");
            // 恢復按鈕狀態
            SetButtonsInteractable(true);
            if (loadingMask != null)
            {
                loadingMask.SetActive(false);
            }
            return;
        }

        // 構建編輯指令
        string description = clothingIndex < clothingDescriptions.Length 
            ? clothingDescriptions[clothingIndex] 
            : $"clothing item {clothingIndex + 1}";
        string instruction = string.Format(editInstructionTemplate, description);

        // 調用GeminiCore進行圖片合成（使用兩個圖片）
        geminiCore.CompositeImages(instruction, currentTexture, clothingTexture, OnImageEdited);
    }

    /// <summary>
    /// 圖片編輯完成後的回調
    /// </summary>
    private void OnImageEdited(Texture2D editedTexture)
    {
        if (editedTexture == null)
        {
            Debug.LogError("ClothingUIPanel: 圖片編輯失敗，返回的Texture2D為空！");
            // 恢復按鈕狀態
            SetButtonsInteractable(true);
            if (loadingMask != null)
            {
                loadingMask.SetActive(false);
            }
            return;
        }

        // 檢查是否有 FalRembgCore，如果有則進行去背處理
        if (falRembgCore != null)
        {
            // 將 Texture2D 轉換為 data URI
            string imageDataUri = ImageUtility.TextureToDataUri(editedTexture);
            
            if (string.IsNullOrEmpty(imageDataUri))
            {
                Debug.LogError("ClothingUIPanel: 無法將Texture2D轉換為Data URI！");
                // 如果轉換失敗，直接顯示原圖
                DisplayFinalImage(editedTexture);
                return;
            }

            Debug.Log("ClothingUIPanel: 開始進行去背處理...");
            
            // 調用 FalRembgCore 進行去背
            falRembgCore.RemoveBackground(
                imageDataUri,
                onDone: OnBackgroundRemoved,
                onError: (error) =>
                {
                    Debug.LogError($"ClothingUIPanel: 去背失敗: {error}");
                    // 去背失敗時，仍然顯示原圖
                    DisplayFinalImage(editedTexture);
                },
                cropToBbox: true
            );
        }
        else
        {
            // 如果沒有 FalRembgCore，直接顯示圖片
            Debug.LogWarning("ClothingUIPanel: FalRembgCore 未設置，跳過去背處理");
            DisplayFinalImage(editedTexture);
        }
    }

    /// <summary>
    /// 去背完成後的回調
    /// </summary>
    private void OnBackgroundRemoved(Texture2D backgroundRemovedTexture)
    {
        if (backgroundRemovedTexture == null)
        {
            Debug.LogWarning("ClothingUIPanel: 去背返回的Texture2D為空，使用原圖");
            return;
        }

        Debug.Log("ClothingUIPanel: 去背完成！");
        DisplayFinalImage(backgroundRemovedTexture);
    }

    /// <summary>
    /// 顯示最終圖片
    /// </summary>
    private void DisplayFinalImage(Texture2D finalTexture)
    {
        if (finalTexture == null)
        {
            Debug.LogError("ClothingUIPanel: 最終Texture2D為空！");
            // 恢復按鈕狀態
            SetButtonsInteractable(true);
            if (loadingMask != null)
            {
                loadingMask.SetActive(false);
            }
            return;
        }

        // 將Texture2D轉換為Sprite
        Sprite resultSprite = ImageUtility.TextureToSprite(finalTexture);

        if (resultSprite == null)
        {
            Debug.LogError("ClothingUIPanel: 無法將Texture2D轉換為Sprite！");
            // 恢復按鈕狀態
            SetButtonsInteractable(true);
            if (loadingMask != null)
            {
                loadingMask.SetActive(false);
            }
            return;
        }

        // 替換北極熊的圖片
        if (polarBearImage != null)
        {
            polarBearImage.sprite = resultSprite;
            polarBearImage.preserveAspect = true;
        }

        // 關閉Loading Mask
        if (loadingMask != null)
        {
            loadingMask.SetActive(false);
        }

        // 恢復按鈕狀態
        SetButtonsInteractable(true);

        Debug.Log("ClothingUIPanel: 圖片處理完成！");
    }

    /// <summary>
    /// 設置所有按鈕的可交互狀態
    /// </summary>
    private void SetButtonsInteractable(bool interactable)
    {
        if (closeButton != null)
        {
            closeButton.interactable = interactable;
        }

        foreach (var button in clothingButtons)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }
    }

    private void OnDestroy()
    {
        // 清理事件監聽
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
        }

        foreach (var button in clothingButtons)
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
            }
        }
    }
}

