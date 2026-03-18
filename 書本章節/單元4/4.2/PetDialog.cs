using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// PetDialog - 寵物對話氣泡UI組件
/// 功能：
/// 1. 可以跟隨SpriteRenderer的遊戲對象移動（可以自己設定Offset）
/// 2. 可以根據字數自動縮放Frame的大小
/// 3. 提供Function可以給外部代碼調用改變文字（Text）
/// 4. 顯示特定秒數後會自動隱藏該物件
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class PetDialog : MonoBehaviour
{
    [Header("跟隨目標設置")]
    [Tooltip("要跟隨的SpriteRenderer遊戲對象（如果為空則不跟隨）")]
    [SerializeField] private SpriteRenderer targetSpriteRenderer;
    
    [Tooltip("相對於目標對象的偏移量（世界座標）")]
    [SerializeField] private Vector3 offset = new Vector3(0, 1, 0);
    
    [Header("UI組件引用")]
    [Tooltip("對話框背景（Frame），用於自動調整大小")]
    [SerializeField] private RectTransform frameRectTransform;
    
    [Tooltip("顯示文字的Text組件")]
    [SerializeField] private Text dialogText;
    
    [Tooltip("Canvas組件（用於世界座標到UI座標的轉換）")]
    [SerializeField] private Canvas canvas;
    
    [Header("尺寸設置")]
    [Tooltip("文字內邊距")]
    [SerializeField] private Vector2 padding = new Vector2(20, 15);
    
    [Tooltip("最小寬度")]
    [SerializeField] private float minWidth = 100f;
    
    [Tooltip("最大寬度")]
    [SerializeField] private float maxWidth = 400f;
    
    [Tooltip("最小高度")]
    [SerializeField] private float minHeight = 50f;
    
    [Header("自動隱藏設置")]
    [Tooltip("顯示多少秒後自動隱藏（0表示不自動隱藏）")]
    [SerializeField] private float autoHideSeconds = 3f;
    
    [Tooltip("是否在顯示時自動開始計時")]
    [SerializeField] private bool autoStartHideTimer = true;
    
    [Header("跟隨設置")]
    [Tooltip("是否在隱藏時也更新位置（建議開啟，這樣顯示時位置就是正確的）")]
    [SerializeField] private bool updatePositionWhenHidden = true;
    
    [Tooltip("是否啟用調試日誌")]
    [SerializeField] private bool enableDebugLog = false;
    
    // 私有变量
    private RectTransform rectTransform;
    private Camera mainCamera;
    private Coroutine hideCoroutine;
    private bool isVisible = true;
    
    private void Awake()
    {
        // 獲取組件引用
        rectTransform = GetComponent<RectTransform>();
        
        // 如果沒有指定Canvas，嘗試從父對象查找
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }
        
        // 如果沒有指定frameRectTransform，使用自己的RectTransform
        if (frameRectTransform == null)
        {
            frameRectTransform = rectTransform;
        }
        
        // 如果沒有指定dialogText，嘗試查找子對象中的Text組件
        if (dialogText == null)
        {
            dialogText = GetComponentInChildren<Text>();
        }
        
        // 獲取主攝影機
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindObjectOfType<Camera>();
            }
        }
    }
    
    private void Start()
    {
        // 初始化時，如果已經有文字且設置了自動隱藏，開始計時
        if (autoHideSeconds > 0 && !string.IsNullOrEmpty(dialogText?.text))
        {
            if (autoStartHideTimer)
            {
                StartHideTimer();
            }
        }
    }
    
    private void LateUpdate()
    {
        // 每幀更新位置，跟隨目標對象
        bool shouldUpdate = (isVisible || updatePositionWhenHidden);
        if (shouldUpdate && targetSpriteRenderer != null && mainCamera != null && canvas != null)
        {
            UpdatePosition();
        }
        else if (enableDebugLog)
        {
            // 調試信息：檢查為什麼沒有更新位置
            if (targetSpriteRenderer == null)
            {
                Debug.LogWarning("PetDialog: targetSpriteRenderer未設置，無法跟隨目標！");
            }
            if (mainCamera == null)
            {
                Debug.LogWarning("PetDialog: mainCamera未找到！");
            }
            if (canvas == null)
            {
                Debug.LogWarning("PetDialog: canvas未設置！");
            }
        }
    }
    
    /// <summary>
    /// 更新UI位置，使其跟隨目標SpriteRenderer
    /// </summary>
    private void UpdatePosition()
    {
        // 獲取目標對象的世界座標位置
        Vector3 targetWorldPos = targetSpriteRenderer.transform.position + offset;
        
        // 根據Canvas的Render Mode選擇不同的轉換方式
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Screen Space - Overlay模式：直接使用屏幕座標
            Vector2 screenPoint = mainCamera.WorldToScreenPoint(targetWorldPos);
            rectTransform.position = screenPoint;
        }
        else if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
        {
            // Screen Space - Camera 或 World Space模式：需要轉換為Canvas本地座標
            Vector2 screenPoint = mainCamera.WorldToScreenPoint(targetWorldPos);
            
            Camera canvasCamera = canvas.worldCamera ?? mainCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                screenPoint,
                canvasCamera,
                out Vector2 localPoint
            );
            
            rectTransform.anchoredPosition = localPoint;
        }
        else
        {
            // 默認使用Screen Space - Camera方式
            Vector2 screenPoint = mainCamera.WorldToScreenPoint(targetWorldPos);
            Camera canvasCamera = canvas.worldCamera ?? mainCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                screenPoint,
                canvasCamera,
                out Vector2 localPoint
            );
            rectTransform.anchoredPosition = localPoint;
        }
    }
    
    /// <summary>
    /// 根據文字內容自動調整Frame大小
    /// </summary>
    private void UpdateFrameSize()
    {
        if (dialogText == null || frameRectTransform == null)
        {
            return;
        }
        
        RectTransform textRectTransform = dialogText.rectTransform;
        
        // 先臨時設置Text的最大寬度限制，這樣preferredHeight才能正確計算換行
        float maxTextWidth = maxWidth - padding.x * 2;
        Vector2 originalSizeDelta = textRectTransform.sizeDelta;
        
        // 臨時設置Text寬度為最大可用寬度，強制換行計算
        textRectTransform.sizeDelta = new Vector2(maxTextWidth, 0);
        
        // 強制刷新Text組件的佈局，確保preferredWidth和preferredHeight是最新的
        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(textRectTransform);
        
        // 獲取文字的首選大小（Preferred Size）
        // preferredWidth會考慮換行，不會超過設置的寬度
        float preferredWidth = dialogText.preferredWidth;
        float preferredHeight = dialogText.preferredHeight;
        
        // 計算包含內邊距後的尺寸
        // 寬度：使用preferredWidth（已經考慮了maxWidth限制），但確保在minWidth和maxWidth之間
        float newWidth = Mathf.Clamp(preferredWidth + padding.x * 2, minWidth, maxWidth);
        // 高度：使用preferredHeight（已經考慮了換行），確保不小於minHeight
        float newHeight = Mathf.Max(preferredHeight + padding.y * 2, minHeight);
        
        // 更新Frame大小
        frameRectTransform.sizeDelta = new Vector2(newWidth, newHeight);
        
        // 設置Text的實際寬度（使用計算出的寬度減去padding）
        float actualTextWidth = newWidth - padding.x * 2;
        textRectTransform.sizeDelta = new Vector2(actualTextWidth, 0);
        
        // 再次強制刷新，確保佈局正確
        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(frameRectTransform);
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(textRectTransform);
    }
    
    /// <summary>
    /// 設置對話文字（外部調用接口）
    /// </summary>
    /// <param name="text">要顯示的文字</param>
    /// <param name="enableAutoHide">是否啟用自動隱藏（默認使用autoHideSeconds設置）</param>
    public void SetText(string text, bool? enableAutoHide = null)
    {
        if (dialogText == null)
        {
            Debug.LogWarning("PetDialog: Text組件未設置！");
            return;
        }
        
        // 設置文字
        dialogText.text = text;
        
        // 更新Frame大小（內部已經包含了強制刷新）
        UpdateFrameSize();
        
        // 顯示對象
        Show();
        
        // 決定是否啟用自動隱藏
        bool shouldAutoHide = enableAutoHide ?? (autoHideSeconds > 0);
        
        // 如果設置了自動隱藏，重新開始計時
        if (shouldAutoHide && autoHideSeconds > 0)
        {
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
            }
            hideCoroutine = StartCoroutine(HideAfterSeconds(autoHideSeconds));
        }
        else
        {
            // 如果禁用自動隱藏，停止當前的計時器
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
                hideCoroutine = null;
            }
        }
    }
    
    /// <summary>
    /// 顯示對話氣泡
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        isVisible = true;
    }
    
    /// <summary>
    /// 隱藏對話氣泡
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
        isVisible = false;
        
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
    }
    
    /// <summary>
    /// 開始自動隱藏計時器
    /// </summary>
    private void StartHideTimer()
    {
        if (autoHideSeconds > 0)
        {
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
            }
            hideCoroutine = StartCoroutine(HideAfterSeconds(autoHideSeconds));
        }
    }
    
    /// <summary>
    /// 在指定秒數後隱藏對象
    /// </summary>
    private IEnumerator HideAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Hide();
    }
    
    /// <summary>
    /// 設置跟隨目標（外部調用接口）
    /// </summary>
    /// <param name="target">要跟隨的SpriteRenderer對象</param>
    public void SetTarget(SpriteRenderer target)
    {
        targetSpriteRenderer = target;
    }
    
    /// <summary>
    /// 設置偏移量（外部調用接口）
    /// </summary>
    /// <param name="newOffset">新的偏移量</param>
    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }
    
    /// <summary>
    /// 設置自動隱藏時間（外部調用接口）
    /// </summary>
    /// <param name="seconds">隱藏前的秒數（0表示不自動隱藏）</param>
    public void SetAutoHideSeconds(float seconds)
    {
        autoHideSeconds = seconds;
        
        // 如果正在顯示且設置了新的時間，重新開始計時
        if (isVisible && autoHideSeconds > 0)
        {
            StartHideTimer();
        }
    }
    
    /// <summary>
    /// 取消自動隱藏（外部調用接口）
    /// </summary>
    public void CancelAutoHide()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
    }
    
    // 在編輯器中修改文字時，自動更新大小
    private void OnValidate()
    {
        if (Application.isPlaying && dialogText != null && frameRectTransform != null)
        {
            UpdateFrameSize();
        }
    }
}

