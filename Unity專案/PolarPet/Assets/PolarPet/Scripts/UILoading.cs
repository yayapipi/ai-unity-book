using UnityEngine;

namespace YFrame.Runtime.UIFrame.Y_UIAnim
{
    public class UILoading : MonoBehaviour
    {
        public enum AnimationType
        {
            Once,       
            Loop,      
            PingPong    
        }

        public AnimationType animationType = AnimationType.Loop;
        public float duration = 0.7f;
        public float startOffsetX = 0f; 
        public float endOffsetX = 0f;  

        private RectTransform rectTransform;
        private Vector2 startPosition;
        private Vector2 endPosition;
        private float elapsedTime = 0f;
        private bool isReversing = false; 

        void Start()
        {
            InitializePositions();
        }

        void Update()
        {
            elapsedTime += Time.deltaTime;
            var t = Mathf.Clamp01(elapsedTime / duration);
            if (isReversing)
            {
                t = 1 - t;
            }
            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, endPosition, t);
            if (elapsedTime >= duration)
            {
                switch (animationType)
                {
                    case AnimationType.Once:
                        rectTransform.anchoredPosition = endPosition;
                        enabled = false; 
                        break;

                    case AnimationType.Loop:
                        elapsedTime = 0f;
                        rectTransform.anchoredPosition = startPosition;
                        break;

                    case AnimationType.PingPong:
                        isReversing = !isReversing;
                        elapsedTime = 0f;
                        break;
                }
            }
        }

        public void SetOffsets(float newStartOffsetX, float newEndOffsetX)
        {
            startOffsetX = newStartOffsetX;
            endOffsetX = newEndOffsetX;
            InitializePositions();
        }

        private void InitializePositions()
        {
            rectTransform = GetComponent<RectTransform>();
            var parentRectTransform = rectTransform.parent.GetComponent<RectTransform>();
            var halfWidth = parentRectTransform.rect.width / 2;
            startPosition = new Vector2(-halfWidth + startOffsetX, 0);
            endPosition = new Vector2( halfWidth + endOffsetX, 0);
            rectTransform.anchoredPosition = startPosition;
        }
    }
}
