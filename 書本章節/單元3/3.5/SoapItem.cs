using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PolarPet
{
    /// <summary>
    /// 肥皂道具：可在場景中拖拽，放到寵物身上來回搓澡。
    /// - 拖拽到 Tag=Pet 上時：播放寵物 Bath 動畫，持續隨機生成泡泡
    /// - 離開或放開時：停止搓澡，切回 Idle 動畫
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoapItem : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("References")]
        [SerializeField] private Camera worldCamera;
        [Tooltip("泡泡的 Prefab（請掛上 Bubble 腳本）。")]
        [SerializeField] private GameObject bubblePrefab;

        [Header("Pet Detect")]
        [Tooltip("用於判定『寵物』的 Tag（北極熊請設成 Pet）。")]
        [SerializeField] private string petTag = "Pet";
        [Tooltip("拖拽時用來判定是否正在寵物身上搓澡的範圍半徑（世界座標）。")]
        [SerializeField] private float bathCheckRadius = 0.3f;

        [Header("Pet Animation")]
        [Tooltip("搓澡時寵物要播放的動畫狀態名稱。")]
        [SerializeField] private string bathStateName = "Bath";
        [Tooltip("停止搓澡後切回的動畫狀態名稱。")]
        [SerializeField] private string idleStateName = "Idle";

        [Header("Bubble Spawn")]
        [Tooltip("搓澡時生成泡泡的時間間隔（秒）。")]
        [Min(0.05f)]
        [SerializeField] private float bubbleInterval = 0.2f;
        [Tooltip("泡泡相對肥皂的隨機偏移範圍（X,Y）。")]
        [SerializeField] private Vector2 bubbleOffsetRange = new Vector2(0.3f, 0.2f);

        [Header("Drag")]
        [Tooltip("拖拽時是否維持原本 Z 值（2D 常用）。")]
        [SerializeField] private bool keepOriginalZ = true;

        private bool isDragging;
        private bool isBathing;
        private Vector3 dragOffsetWorld;
        private float originalZ;
        private Animator currentPetAnimator;
        private Coroutine bubbleRoutine;
        private readonly Collider2D[] overlapResults = new Collider2D[8];

        private void Reset()
        {
            worldCamera = Camera.main;
        }

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            originalZ = transform.position.z;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (worldCamera == null) return;
            if (eventData == null) return;

            isDragging = true;

            Vector3 pointerWorld = ScreenToWorld(eventData.position);
            dragOffsetWorld = transform.position - pointerWorld;
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

            UpdateBathingState();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isDragging) return;

            isDragging = false;
            StopBathing();
        }

        private void UpdateBathingState()
        {
            Animator petAnimator = TryGetPetAnimatorUnderSoap();

            if (petAnimator != null)
            {
                if (!isBathing || petAnimator != currentPetAnimator)
                {
                    StartBathing(petAnimator);
                }
            }
            else
            {
                if (isBathing)
                {
                    StopBathing();
                }
            }
        }

        private Animator TryGetPetAnimatorUnderSoap()
        {
            if (string.IsNullOrEmpty(petTag)) return null;

            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, bathCheckRadius, overlapResults);
            if (hitCount <= 0) return null;

            Collider2D petCollider = null;
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D c = overlapResults[i];
                if (c == null) continue;
                if (c.CompareTag(petTag))
                {
                    petCollider = c;
                    break;
                }
            }

            if (petCollider == null) return null;
            return petCollider.GetComponentInChildren<Animator>();
        }

        private void StartBathing(Animator petAnimator)
        {
            isBathing = true;
            currentPetAnimator = petAnimator;

            if (currentPetAnimator != null && !string.IsNullOrEmpty(bathStateName))
            {
                currentPetAnimator.Play(bathStateName, 0, 0f);
            }

            if (bubbleRoutine != null)
            {
                StopCoroutine(bubbleRoutine);
            }

            bubbleRoutine = StartCoroutine(BubbleSpawnRoutine());
        }

        private void StopBathing()
        {
            isBathing = false;

            if (bubbleRoutine != null)
            {
                StopCoroutine(bubbleRoutine);
                bubbleRoutine = null;
            }

            if (currentPetAnimator != null && !string.IsNullOrEmpty(idleStateName))
            {
                currentPetAnimator.Play(idleStateName, 0, 0f);
            }

            currentPetAnimator = null;
        }

        private IEnumerator BubbleSpawnRoutine()
        {
            if (bubblePrefab == null)
            {
                yield break;
            }

            WaitForSeconds wait = new WaitForSeconds(bubbleInterval);

            while (isBathing)
            {
                SpawnBubble();
                yield return wait;
            }
        }

        private void SpawnBubble()
        {
            if (bubblePrefab == null) return;

            Vector3 offset = new Vector3(
                Random.Range(-bubbleOffsetRange.x, bubbleOffsetRange.x),
                Random.Range(-bubbleOffsetRange.y, bubbleOffsetRange.y),
                0f
            );

            Instantiate(bubblePrefab, transform.position + offset, Quaternion.identity);
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            Vector3 p = new Vector3(screenPosition.x, screenPosition.y, 0f);

            float zFromCamera = 0f;
            if (worldCamera != null)
            {
                zFromCamera = transform.position.z - worldCamera.transform.position.z;
            }

            p.z = zFromCamera;
            return worldCamera.ScreenToWorldPoint(p);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, bathCheckRadius);
        }
    }
}

