using UnityEngine;

namespace PolarPet
{
    /// <summary>
    /// 控制北極熊在場景中自動切換 Idle / Walk / Sleep / Think 狀態，
    /// 並在限定區域內隨機走動。可被拖拽、餵食、搓澡等互動暫停。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetAiController : MonoBehaviour
    {
        private enum AiState
        {
            Idle,
            Walk,
            Sleep,
            Think
        }

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Animation State Names")]
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string walkStateName = "Walk";
        [SerializeField] private string sleepStateName = "Sleep";
        [SerializeField] private string thinkStateName = "Think";

        [Header("State Durations (Seconds)")]
        [Min(0.5f)]
        [SerializeField] private float minIdleDuration = 2f;
        [SerializeField] private float maxIdleDuration = 4f;
        [Min(1f)]
        [SerializeField] private float minWalkDuration = 3f;
        [SerializeField] private float maxWalkDuration = 6f;
        [Min(2f)]
        [SerializeField] private float minSleepDuration = 5f;
        [SerializeField] private float maxSleepDuration = 10f;
        [Min(1f)]
        [SerializeField] private float minThinkDuration = 2f;
        [SerializeField] private float maxThinkDuration = 5f;

        [Header("Walk Settings")]
        [Tooltip("行走速度（世界座標單位 / 秒）。")]
        [Min(0.1f)]
        [SerializeField] private float walkSpeed = 1.2f;

        [Header("Walk Area")]
        [Tooltip("可移動區域的中心點（世界座標）。")]
        [SerializeField] private Vector2 areaCenter = Vector2.zero;
        [Tooltip("可移動區域的寬度與高度。")]
        [SerializeField] private Vector2 areaSize = new Vector2(4f, 2f);

        [Header("Debug")]
        [Tooltip("在 Scene 視窗中繪製可移動區域的 Gizmos。")]
        [SerializeField] private bool drawAreaGizmos = true;

        private AiState currentState = AiState.Idle;
        private float stateTimer;
        private Vector3 walkTargetPosition;
        private bool isInteractionLocked;

        /// <summary>
        /// 由外部（拖拽、餵食、搓澡）呼叫，啟用或解除互動鎖定。
        /// 鎖定時 AI 不會更新狀態與移動。
        /// </summary>
        public void SetInteractionLock(bool locked)
        {
            isInteractionLocked = locked;
        }

        /// <summary>
        /// 提供給外部查詢當前是否被互動鎖定。
        /// </summary>
        public bool IsInteractionLocked => isInteractionLocked;

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            areaCenter = transform.position;
        }

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (areaSize.x < 0.1f) areaSize.x = 0.1f;
            if (areaSize.y < 0.1f) areaSize.y = 0.1f;

            ChangeState(AiState.Idle);
        }

        private void Update()
        {
            if (isInteractionLocked)
            {
                return;
            }

            float dt = Time.deltaTime;
            stateTimer -= dt;

            switch (currentState)
            {
                case AiState.Idle:
                case AiState.Sleep:
                case AiState.Think:
                    UpdateNonWalkState();
                    break;
                case AiState.Walk:
                    UpdateWalkState(dt);
                    break;
            }
        }

        /// <summary>
        /// 更新 Idle / Sleep / Think 等靜態狀態，在時間結束後切換下一個狀態。
        /// </summary>
        private void UpdateNonWalkState()
        {
            if (stateTimer > 0f)
            {
                return;
            }

            AiState next = ChooseNextState();
            ChangeState(next);
        }

        /// <summary>
        /// 更新行走狀態：往目標點移動，時間到或抵達目標時切換下一個狀態。
        /// </summary>
        private void UpdateWalkState(float deltaTime)
        {
            Vector3 currentPosition = transform.position;
            Vector3 target = walkTargetPosition;

            currentPosition = Vector3.MoveTowards(currentPosition, target, walkSpeed * deltaTime);
            transform.position = currentPosition;

            UpdateFlipByDirection(target.x - currentPosition.x);

            bool reachedTarget = Vector3.Distance(currentPosition, target) < 0.01f;

            if (stateTimer <= 0f || reachedTarget)
            {
                AiState next = ChooseNextState();
                ChangeState(next);
            }
        }

        /// <summary>
        /// 依照簡單權重隨機選擇下一個 AI 狀態。
        /// </summary>
        private AiState ChooseNextState()
        {
            float idleWeight = 1.5f;
            float walkWeight = 2.5f;
            float sleepWeight = 0.6f;
            float thinkWeight = 1.0f;

            float total = idleWeight + walkWeight + sleepWeight + thinkWeight;
            float r = Random.value * total;

            if (r < idleWeight) return AiState.Idle;
            r -= idleWeight;
            if (r < walkWeight) return AiState.Walk;
            r -= walkWeight;
            if (r < sleepWeight) return AiState.Sleep;
            return AiState.Think;
        }

        /// <summary>
        /// 切換到指定狀態，同時設定動畫與狀態持續時間。
        /// </summary>
        private void ChangeState(AiState newState)
        {
            currentState = newState;

            switch (currentState)
            {
                case AiState.Idle:
                    PlayAnimationIfValid(idleStateName);
                    stateTimer = Random.Range(minIdleDuration, maxIdleDuration);
                    break;

                case AiState.Walk:
                    PlayAnimationIfValid(walkStateName);
                    stateTimer = Random.Range(minWalkDuration, maxWalkDuration);
                    walkTargetPosition = GetRandomPointInAreaAtCurrentHeight();
                    break;

                case AiState.Sleep:
                    PlayAnimationIfValid(sleepStateName);
                    stateTimer = Random.Range(minSleepDuration, maxSleepDuration);
                    break;

                case AiState.Think:
                    PlayAnimationIfValid(thinkStateName);
                    stateTimer = Random.Range(minThinkDuration, maxThinkDuration);
                    break;
            }
        }

        /// <summary>
        /// 由目前位置的 Y 高度，取得區域內的隨機目標點。
        /// </summary>
        private Vector3 GetRandomPointInAreaAtCurrentHeight()
        {
            Vector2 halfSize = areaSize * 0.5f;
            float minX = areaCenter.x - halfSize.x;
            float maxX = areaCenter.x + halfSize.x;
            float minY = areaCenter.y - halfSize.y;
            float maxY = areaCenter.y + halfSize.y;

            float x = Random.Range(minX, maxX);
            float y = Mathf.Clamp(transform.position.y, minY, maxY);

            Vector3 result = new Vector3(x, y, transform.position.z);
            return result;
        }

        /// <summary>
        /// 根據移動方向更新左右 Flip。
        /// </summary>
        private void UpdateFlipByDirection(float deltaX)
        {
            if (Mathf.Abs(deltaX) < 0.0001f)
            {
                return;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = deltaX < 0f;
                return;
            }

            Vector3 s = transform.localScale;
            float x = Mathf.Abs(s.x);
            s.x = deltaX < 0f ? -x : x;
            transform.localScale = s;
        }

        /// <summary>
        /// 安全地播放指定名稱的動畫狀態。
        /// </summary>
        private void PlayAnimationIfValid(string stateName)
        {
            if (animator == null) return;
            if (string.IsNullOrEmpty(stateName)) return;

            animator.Play(stateName, 0, 0f);
        }

        private void OnDrawGizmos()
        {
            if (!drawAreaGizmos)
            {
                return;
            }

            Gizmos.color = Color.yellow;

            Vector3 center = new Vector3(areaCenter.x, areaCenter.y, transform.position.z);
            Vector3 size = new Vector3(areaSize.x, areaSize.y, 0f);

            Gizmos.DrawWireCube(center, size);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawAreaGizmos)
            {
                return;
            }

            Gizmos.color = Color.green;

            Vector3 center = new Vector3(areaCenter.x, areaCenter.y, transform.position.z);
            Vector3 size = new Vector3(areaSize.x, areaSize.y, 0f);

            Gizmos.DrawWireCube(center, size);
        }
    }
}

