using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 北極熊寵物控制：
/// 1. 可被拖拽（Drag 動畫，顯示陰影）
/// 2. 平時會在指定範圍內隨機走動（Walk），偶爾睡覺（Sleep）、偶爾發呆思考（Think）
/// 3. 拖拽、吃東西、搓澡等互動期間暫停自動移動
/// 4. 在 Scene 視窗用 Gizmos 顯示可活動範圍
/// </summary>
public class DragPolar : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private enum BearState
    {
        Idle,
        Walk,
        Sleep,
        Think
    }

    [Header("基本設定")]
    [SerializeField] private Camera mainCamera;

    [Header("動畫")]
    [SerializeField] private Animator animator;
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string walkStateName = "Walk";
    [SerializeField] private string dragStateName = "Drag";
    [SerializeField] private string sleepStateName = "Sleep";
    [SerializeField] private string thinkStateName = "Think";

    [Header("外觀")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private GameObject shadowObject;

    [Header("自動移動設定")]
    [Tooltip("北極熊在場景中行走的速度單位 / 秒")]
    [SerializeField] private float moveSpeed = 1.5f;

    [Tooltip("可活動範圍的中心點（世界座標，建議設在地面上）")]
    [SerializeField] private Vector2 moveAreaCenter;

    [Tooltip("可活動範圍的寬度與高度")]
    [SerializeField] private Vector2 moveAreaSize = new Vector2(5f, 3f);

    [Tooltip("每個狀態持續的最短秒數")]
    [SerializeField] private float minStateDuration = 2f;

    [Tooltip("每個狀態持續的最長秒數")]
    [SerializeField] private float maxStateDuration = 5f;

    [Header("互動鎖定設定")]
    [Tooltip("外部互動（吃東西、搓澡等）期間是否暫停自動移動")]
    [SerializeField] private bool lockMovementDuringInteraction = true;

    // 是否正在被拖拽
    private bool isDragging;
    // 物件與滑鼠位置的位移，用來維持拖拽時的相對位置
    private Vector3 dragOffset;

    // 自動行為狀態
    private BearState currentState = BearState.Walk;
    private float stateTimer;
    private Vector3 currentTargetPosition;

    // 是否處於吃東西 / 搓澡等互動狀態（由其他腳本呼叫控制）
    private bool isInInteraction;

    private void Awake()
    {
        // 快取主要相機與 SpriteRenderer
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // 預設陰影關閉（只有拖拽時顯示）
        if (shadowObject != null)
        {
            shadowObject.SetActive(false);
        }

        // 若尚未在 Inspector 設定活動範圍中心，預設以當前位置為中心
        if (Mathf.Approximately(moveAreaCenter.x, 0f) && Mathf.Approximately(moveAreaCenter.y, 0f))
        {
            moveAreaCenter = transform.position;
        }

        // 初始化狀態
        ChooseNextState();
    }

    private void Update()
    {
        // 拖拽中或互動中時不做自動行為
        if (isDragging)
        {
            return;
        }

        if (lockMovementDuringInteraction && isInInteraction)
        {
            return;
        }

        UpdateState(Time.deltaTime);
    }

    /// <summary>
    /// 對外提供：開始互動（吃東西、搓澡等），暫停自動移動。
    /// 互動腳本在開始時呼叫 BeginInteraction()，結束時呼叫 EndInteraction()。
    /// </summary>
    public void BeginInteraction()
    {
        isInInteraction = true;
    }

    /// <summary>
    /// 對外提供：結束互動，恢復自動移動。
    /// </summary>
    public void EndInteraction()
    {
        isInInteraction = false;
    }

    /// <summary>
    /// 狀態更新：走路 / 睡覺 / 思考。
    /// </summary>
    private void UpdateState(float deltaTime)
    {
        stateTimer -= deltaTime;

        if (currentState == BearState.Walk)
        {
            UpdateWalk(deltaTime);
        }

        if (stateTimer <= 0f)
        {
            ChooseNextState();
        }
    }

    /// <summary>
    /// 走路狀態下朝目標位置移動，超出範圍或到達目標時重新選一個目標點。
    /// </summary>
    private void UpdateWalk(float deltaTime)
    {
        Vector3 currentPos = transform.position;
        Vector3 direction = currentTargetPosition - currentPos;

        // 若已接近目標位置，重新挑選下一個目標
        if (direction.sqrMagnitude < 0.01f)
        {
            currentTargetPosition = GetRandomPointInArea();
            direction = currentTargetPosition - currentPos;
        }

        if (direction.sqrMagnitude > 0.0001f)
        {
            Vector3 moveDir = direction.normalized;
            Vector3 nextPos = currentPos + moveDir * moveSpeed * deltaTime;

            // 限制在可活動範圍內
            nextPos = ClampPositionToArea(nextPos);

            float previousX = transform.position.x;
            transform.position = nextPos;

            float deltaX = transform.position.x - previousX;
            UpdateFlipByDirection(deltaX);
        }
    }

    /// <summary>
    /// 選擇下一個狀態：大部分時間走路，偶爾發呆（Idle）、睡覺或思考。
    /// </summary>
    private void ChooseNextState()
    {
        float duration = Random.Range(minStateDuration, maxStateDuration);
        stateTimer = duration;

        float r = Random.value;
        // 約 50% 走路、20% 發呆、15% 睡覺、15% 思考
        if (r < 0.5f)
        {
            currentState = BearState.Walk;
            currentTargetPosition = GetRandomPointInArea();
        }
        else if (r < 0.7f)
        {
            currentState = BearState.Idle;
        }
        else if (r < 0.85f)
        {
            currentState = BearState.Sleep;
        }
        else
        {
            currentState = BearState.Think;
        }

        PlayAnimationForCurrentState();
    }

    /// <summary>
    /// 依照目前狀態播放對應的動畫。
    /// </summary>
    private void PlayAnimationForCurrentState()
    {
        if (animator == null)
        {
            return;
        }

        string stateName = null;

        switch (currentState)
        {
            case BearState.Idle:
                stateName = idleStateName;
                break;
            case BearState.Walk:
                stateName = walkStateName;
                break;
            case BearState.Sleep:
                stateName = sleepStateName;
                break;
            case BearState.Think:
                stateName = thinkStateName;
                break;
        }

        if (!string.IsNullOrEmpty(stateName))
        {
            animator.CrossFade(stateName, 0.1f);
        }
    }

    // Pointer 按下時開始拖拽（需搭配 EventSystem + 對應的 Raycaster）
    public void OnPointerDown(PointerEventData eventData)
    {
        if (mainCamera == null)
        {
            return;
        }

        isDragging = true;

        Vector3 mouseWorldPos = GetWorldPositionFromEvent(eventData);

        dragOffset = transform.position - mouseWorldPos;

        SetDraggingAnimation(true);
        SetShadowVisible(true);
    }
    // 拖拽過程中更新位置與方向
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || mainCamera == null)
        {
            return;
        }

        Vector3 mouseWorldPos = GetWorldPositionFromEvent(eventData);

        float previousX = transform.position.x;
        Vector3 targetPos = mouseWorldPos + dragOffset;

        transform.position = targetPos;

        float deltaX = transform.position.x - previousX;
        UpdateFlipByDirection(deltaX);
    }
    // Pointer 放開時結束拖拽
    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        SetDraggingAnimation(false);
        SetShadowVisible(false);
    }

    // 將 PointerEventData 的螢幕座標轉成世界座標（保留與物件相同的 Z 深度）
    private Vector3 GetWorldPositionFromEvent(PointerEventData eventData)
    {
        Vector3 screenPos = eventData.position;
        screenPos.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        return mainCamera.ScreenToWorldPoint(screenPos);
    }


    private void UpdateFlipByDirection(float deltaX)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (Mathf.Approximately(deltaX, 0f))
        {
            return;
        }

        // 假設預設面向右：往右移動 -> 不 Flip；往左移動 -> Flip X
        if (deltaX > 0f)
        {
            spriteRenderer.flipX = false;
        }
        else if (deltaX < 0f)
        {
            spriteRenderer.flipX = true;
        }
    }


    /// <summary>
    /// 根據是否拖拽切換動畫。
    /// 拖拽時播放 Drag，不拖拽時回到目前自動狀態對應的動畫。
    /// </summary>
    private void SetDraggingAnimation(bool dragging)
    {
        if (animator == null)
        {
            return;
        }

        if (dragging)
        {
            if (!string.IsNullOrEmpty(dragStateName))
            {
                animator.CrossFade(dragStateName, 0.1f);
            }
        }
        else
        {
            // 拖拽結束，恢復自動行為狀態動畫
            PlayAnimationForCurrentState();
        }
    }

    private void SetShadowVisible(bool visible)
    {
        if (shadowObject == null)
        {
            return;
        }

        shadowObject.SetActive(visible);
    }

    /// <summary>
    /// 取得可活動範圍內的一個隨機點（世界座標）。
    /// </summary>
    private Vector3 GetRandomPointInArea()
    {
        float halfWidth = moveAreaSize.x * 0.5f;
        float halfHeight = moveAreaSize.y * 0.5f;

        float x = Random.Range(moveAreaCenter.x - halfWidth, moveAreaCenter.x + halfWidth);
        float y = Random.Range(moveAreaCenter.y - halfHeight, moveAreaCenter.y + halfHeight);

        return new Vector3(x, y, transform.position.z);
    }

    /// <summary>
    /// 將位置限制在可活動範圍內。
    /// </summary>
    private Vector3 ClampPositionToArea(Vector3 position)
    {
        float halfWidth = moveAreaSize.x * 0.5f;
        float halfHeight = moveAreaSize.y * 0.5f;

        float clampedX = Mathf.Clamp(position.x, moveAreaCenter.x - halfWidth, moveAreaCenter.x + halfWidth);
        float clampedY = Mathf.Clamp(position.y, moveAreaCenter.y - halfHeight, moveAreaCenter.y + halfHeight);

        return new Vector3(clampedX, clampedY, position.z);
    }

    /// <summary>
    /// 在 Scene 視窗中繪製可活動範圍（只在選到物件時顯示）。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.7f, 1f, 0.4f);
        Vector3 center = new Vector3(moveAreaCenter.x, moveAreaCenter.y, transform.position.z);
        Vector3 size = new Vector3(moveAreaSize.x, moveAreaSize.y, 0f);
        Gizmos.DrawWireCube(center, size);
    }
}
