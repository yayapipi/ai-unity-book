using UnityEngine;
using UnityEngine.EventSystems;

namespace PolarPet
{
    /// <summary>
    /// 允許玩家用滑鼠點擊並拖拽寵物：
    /// - 按下：切換 Drag 動畫、顯示陰影
    /// - 拖動：跟隨滑鼠位置並依左右移動 Flip X
    /// - 放開：切回 Walk 動畫、隱藏陰影
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetDragController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("References")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("拖拽時要顯示的陰影物件（通常是寵物底下的子物件）。")]
        [SerializeField] private GameObject shadowObject;

        [Header("Animation")]
        [Tooltip("拖拽時播放的狀態名稱（必須存在於 Animator Controller）。")]
        [SerializeField] private string dragStateName = "Drag";
        [Tooltip("放手後播放的狀態名稱（必須存在於 Animator Controller）。")]
        [SerializeField] private string walkStateName = "Walk";

        [Header("Drag")]
        [Tooltip("拖拽時是否維持原本 Z 值（2D 常用）。")]
        [SerializeField] private bool keepOriginalZ = true;

        private bool isDragging;
        private Vector3 dragOffsetWorld;
        private float originalZ;
        private float lastPointerWorldX;

        private void Reset()
        {
            worldCamera = Camera.main;
            animator = GetComponentInChildren<Animator>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            originalZ = transform.position.z;
            SetShadowVisible(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (worldCamera == null) return;
            if (eventData == null) return;

            isDragging = true;

            Vector3 pointerWorld = ScreenToWorld(eventData.position);
            dragOffsetWorld = transform.position - pointerWorld;
            lastPointerWorldX = pointerWorld.x;

            PlayState(dragStateName);
            SetShadowVisible(true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging) return;
            if (worldCamera == null) return;
            if (eventData == null) return;

            Vector3 pointerWorld = ScreenToWorld(eventData.position);
            Vector3 target = pointerWorld + dragOffsetWorld;

            if (keepOriginalZ) target.z = originalZ;
            transform.position = target;

            float deltaX = pointerWorld.x - lastPointerWorldX;
            if (Mathf.Abs(deltaX) > 0.0001f)
            {
                SetFlipByMovingDirection(deltaX);
                lastPointerWorldX = pointerWorld.x;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isDragging) return;

            isDragging = false;
            PlayState(walkStateName);
            SetShadowVisible(false);
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            Vector3 p = new Vector3(screenPosition.x, screenPosition.y, 0f);

            // 2D 常用：把 Z 設成物件與相機的距離，確保 ScreenToWorldPoint 正確
            float zFromCamera = 0f;
            if (worldCamera != null)
            {
                zFromCamera = transform.position.z - worldCamera.transform.position.z;
            }

            p.z = zFromCamera;
            return worldCamera.ScreenToWorldPoint(p);
        }

        private void SetFlipByMovingDirection(float deltaX)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = deltaX < 0f;
                return;
            }

            Vector3 s = transform.localScale;
            float x = Mathf.Abs(s.x);
            s.x = (deltaX < 0f) ? -x : x;
            transform.localScale = s;
        }

        private void PlayState(string stateName)
        {
            if (animator == null) return;
            if (string.IsNullOrEmpty(stateName)) return;

            animator.Play(stateName, 0, 0f);
        }

        private void SetShadowVisible(bool visible)
        {
            if (shadowObject != null) shadowObject.SetActive(visible);
        }
    }
}

