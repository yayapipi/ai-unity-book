using UnityEngine;
using UnityEngine.EventSystems;

namespace PolarPet
{
    /// <summary>
    /// 魚道具：可在場景中拖拽，放到寵物身上即可餵食。
    /// - 餵食成功：播放寵物 Eat 動畫 + 吃東西音效，然後道具消失
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishItem : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("References")]
        [SerializeField] private Camera worldCamera;
        [Tooltip("播放吃東西音效用（可放在魚上或寵物上）。")]
        [SerializeField] private AudioSource audioSource;

        [Header("Feed Target")]
        [Tooltip("用於判定『寵物』的 Tag（北極熊請設成 Pet）。")]
        [SerializeField] private string petTag = "Pet";
        [Tooltip("放開時用來判定是否放到寵物身上的範圍半徑（世界座標）。")]
        [SerializeField] private float feedCheckRadius = 0.25f;

        [Header("Pet Animation")]
        [Tooltip("餵食成功時，寵物要播放的動畫狀態名稱。")]
        [SerializeField] private string eatStateName = "Eat";
        [Tooltip("吃完後切回的動畫狀態名稱。")]
        [SerializeField] private string idleStateName = "Idle";
        [Tooltip("吃完後等待多久切回 Idle（秒）。")]
        [Min(0f)]
        [SerializeField] private float returnToIdleDelay = 1f;

        [Header("Audio")]
        [SerializeField] private AudioClip eatSfx;
        [Range(0f, 1f)]
        [SerializeField] private float eatSfxVolume = 1f;

        [Header("Drag")]
        [Tooltip("拖拽時是否維持原本 Z 值（2D 常用）。")]
        [SerializeField] private bool keepOriginalZ = true;

        [Header("AI Integrate")]
        [Tooltip("可選：若有掛 PetAiController，餵食期間會暫停 AI 自動行為。")]
        [SerializeField] private PetAiController petAiController;

        private bool isDragging;
        private bool isConsumed;
        private Vector3 dragOffsetWorld;
        private float originalZ;
        private readonly Collider2D[] overlapResults = new Collider2D[8];
        private Collider2D cachedCollider2D;
        private Renderer[] cachedRenderers;

        private void Reset()
        {
            worldCamera = Camera.main;
            audioSource = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            originalZ = transform.position.z;
            cachedCollider2D = GetComponent<Collider2D>();
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            if (petAiController == null)
            {
                petAiController = FindObjectOfType<PetAiController>();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (isConsumed) return;
            if (worldCamera == null) return;
            if (eventData == null) return;

            isDragging = true;
            Vector3 pointerWorld = ScreenToWorld(eventData.position);
            dragOffsetWorld = transform.position - pointerWorld;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isConsumed) return;
            if (!isDragging) return;
            if (worldCamera == null) return;
            if (eventData == null) return;

            Vector3 pointerWorld = ScreenToWorld(eventData.position);
            Vector3 target = pointerWorld + dragOffsetWorld;
            if (keepOriginalZ) target.z = originalZ;
            transform.position = target;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (isConsumed) return;
            if (!isDragging) return;

            isDragging = false;
            TryFeedPet();
        }

        private void TryFeedPet()
        {
            if (string.IsNullOrEmpty(petTag)) return;

            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, feedCheckRadius, overlapResults);
            if (hitCount <= 0) return;

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

            if (petCollider == null) return;

            isConsumed = true;

            Animator petAnimator = petCollider.GetComponentInChildren<Animator>();
            if (petAiController == null)
            {
                petAiController = petCollider.GetComponentInParent<PetAiController>();
            }

            if (petAiController != null)
            {
                petAiController.SetInteractionLock(true);
            }
            HideSelfImmediately();

            if (eatSfx != null)
            {
                if (audioSource != null)
                {
                    audioSource.PlayOneShot(eatSfx, eatSfxVolume);
                }
                else
                {
                    AudioSource.PlayClipAtPoint(eatSfx, transform.position, eatSfxVolume);
                }
            }

            StartCoroutine(FeedRoutine(petAnimator));
        }

        private System.Collections.IEnumerator FeedRoutine(Animator petAnimator)
        {
            if (petAnimator != null)
            {
                if (!string.IsNullOrEmpty(eatStateName))
                {
                    petAnimator.Play(eatStateName, 0, 0f);
                }

                if (returnToIdleDelay > 0f)
                {
                    yield return new WaitForSeconds(returnToIdleDelay);
                }

                if (!string.IsNullOrEmpty(idleStateName))
                {
                    petAnimator.Play(idleStateName, 0, 0f);
                }
            }

            Destroy(gameObject);

            if (petAiController != null)
            {
                petAiController.SetInteractionLock(false);
            }
        }

        private void HideSelfImmediately()
        {
            if (cachedCollider2D != null) cachedCollider2D.enabled = false;

            if (cachedRenderers == null) return;
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] != null) cachedRenderers[i].enabled = false;
            }
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
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, feedCheckRadius);
        }
    }
}

