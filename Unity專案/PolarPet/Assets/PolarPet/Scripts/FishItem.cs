using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 可拖拽的 Fish 道具：
/// 1. 可以在場景中拖拽
/// 2. 拖到寵物上時觸發寵物吃的動畫與音效，然後自己消失
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FishItem : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const float IdleReturnDelaySeconds = 1f;

    [Header("基本設定")]
    [SerializeField] private Camera mainCamera;

    [Header("外觀")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("吃東西設定")]
    [Tooltip("寵物的 Tag，碰到這個 Tag 的物件就視為被餵食")]
    [SerializeField] private string petTag = "Pet";

    [Tooltip("寵物 Animator 裡面『吃東西』的動畫 State 名稱")]
    [SerializeField] private string petEatStateName = "Eat";

    [Tooltip("吃完後要回到的 Idle 動畫 State 名稱")]
    [SerializeField] private string petIdleStateName = "Idle";

    [Tooltip("寵物吃東西動畫的大約持續秒數，用來暫停自動移動")]
    [SerializeField] private float petEatDuration = 2f;

    [Header("音效")]
    [Tooltip("播放吃東西音效的 AudioSource（建議掛在 Fish 上或寵物上）")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("吃東西音效剪輯")]
    [SerializeField] private AudioClip eatClip;

    // 拖拽狀態
    private bool isDragging;
    private Vector3 dragOffset;

    // 是否已經被吃掉（避免重複觸發）
    private bool hasBeenEaten;
    private Animator lastPetAnimator;
    private DragPolar lastPetController;

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // 確保 Collider2D 是 Trigger，方便做餵食判定
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    /// <summary>
    /// 滑鼠按下開始準備拖拽（需要 EventSystem + 對應 Raycaster）
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (mainCamera == null || hasBeenEaten)
        {
            return;
        }

        isDragging = true;

        Vector3 mouseWorldPos = GetWorldPositionFromEvent(eventData);
        dragOffset = transform.position - mouseWorldPos;
    }

    /// <summary>
    /// 拖拽中更新位置
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || mainCamera == null || hasBeenEaten)
        {
            return;
        }

        Vector3 mouseWorldPos = GetWorldPositionFromEvent(eventData);
        Vector3 targetPos = mouseWorldPos + dragOffset;

        transform.position = targetPos;
    }

    /// <summary>
    /// 放開滑鼠結束拖拽
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
    }

    /// <summary>
    /// 2D 觸發判定：拖拽中的魚碰到寵物就觸發餵食
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasBeenEaten)
        {
            return;
        }

        if (!other.CompareTag(petTag))
        {
            return;
        }

        FeedPet(other);
    }

    /// <summary>
    /// 把 PointerEvent 的螢幕座標轉為世界座標
    /// </summary>
    private Vector3 GetWorldPositionFromEvent(PointerEventData eventData)
    {
        Vector3 screenPos = eventData.position;
        // 2D 遊戲通常 Z = 0，以主攝影機與物件的 Z 差距決定深度
        screenPos.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        return mainCamera.ScreenToWorldPoint(screenPos);
    }

    /// <summary>
    /// 餵食：播放寵物吃東西動畫與音效，然後讓魚消失
    /// </summary>
    private void FeedPet(Collider2D petCollider)
    {
        hasBeenEaten = true;

        // 嘗試暫停寵物的自動移動（若寵物上有 NewMonoBehaviourScript）
        DragPolar petController = petCollider.GetComponent<DragPolar>();
        if (petController != null)
        {
            petController.BeginInteraction();
        }
        lastPetController = petController;

        // 1. 播放寵物吃東西動畫
        Animator petAnimator = petCollider.GetComponent<Animator>();
        if (petAnimator != null && !string.IsNullOrEmpty(petEatStateName))
        {
            petAnimator.CrossFade(petEatStateName, 0.1f);
        }
        lastPetAnimator = petAnimator;

        // 2. 播放吃東西音效
        float destroyDelay = 0.1f;
        if (audioSource != null && eatClip != null)
        {
            audioSource.PlayOneShot(eatClip);
            destroyDelay = Mathf.Max(destroyDelay, eatClip.length);
        }

        float unlockDelay = -1f;
        float idleDelay = -1f;

        // 若有寵物控制腳本，於吃東西動畫結束後恢復自動移動
        if (petController != null)
        {
            unlockDelay = Mathf.Max(petEatDuration, destroyDelay);
            Invoke(nameof(EndPetInteractionWrapper), unlockDelay);
        }

        idleDelay = Mathf.Max(petEatDuration, destroyDelay) + IdleReturnDelaySeconds;
        if (lastPetAnimator != null && !string.IsNullOrEmpty(petIdleStateName) && idleDelay >= 0f)
        {
            Invoke(nameof(ReturnPetToIdleWrapper), idleDelay);
        }

        // 3. 隱藏魚的外觀與關閉碰撞
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }

        isDragging = false;

        // 4. 延遲一點時間後銷毀物件（讓音效播完）
        float finalDestroyDelay = destroyDelay;
        if (unlockDelay >= 0f) finalDestroyDelay = Mathf.Max(finalDestroyDelay, unlockDelay + 0.05f);
        if (idleDelay >= 0f) finalDestroyDelay = Mathf.Max(finalDestroyDelay, idleDelay + 0.05f);
        Destroy(gameObject, finalDestroyDelay);
    }

    private void ReturnPetToIdleWrapper()
    {
        if (lastPetAnimator == null) return;
        if (string.IsNullOrEmpty(petIdleStateName)) return;

        lastPetAnimator.CrossFade(petIdleStateName, 0.1f);
    }

    /// <summary>
    /// 供 Invoke 使用的包裝方法：尋找場景中的寵物並解除互動鎖定。
    /// 注意：這是簡單實作，適用於場景中只有一隻寵物的情況。
    /// </summary>
    private void EndPetInteractionWrapper()
    {
        if (lastPetController == null) return;
        lastPetController.EndInteraction();
    }
}

