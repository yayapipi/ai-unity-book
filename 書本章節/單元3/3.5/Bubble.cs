using System.Collections;
using UnityEngine;

namespace PolarPet
{
    /// <summary>
    /// 泡泡效果：生成後會往上漂浮，一段時間後慢慢淡出並自動銷毀。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Bubble : MonoBehaviour
    {
        [Header("Life")]
        [Tooltip("泡泡存在多久開始淡出（秒）。")]
        [Min(0f)]
        [SerializeField] private float lifeTime = 1.2f;
        [Tooltip("淡出時間（秒），總存活時間約為 lifeTime + fadeDuration。")]
        [Min(0.1f)]
        [SerializeField] private float fadeDuration = 0.6f;

        [Header("Movement")]
        [Tooltip("往上漂浮的速度。")]
        [SerializeField] private float riseSpeed = 0.6f;
        [Tooltip("隨機水平漂移範圍（負為左，正為右）。")]
        [SerializeField] private float horizontalDriftRange = 0.15f;

        private SpriteRenderer[] spriteRenderers;
        private Vector3 moveVelocity;

        private void Awake()
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

            float horizontal = Random.Range(-horizontalDriftRange, horizontalDriftRange);
            moveVelocity = new Vector3(horizontal, riseSpeed, 0f);

            StartCoroutine(LifeRoutine());
        }

        private IEnumerator LifeRoutine()
        {
            float elapsed = 0f;

            while (elapsed < lifeTime)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                transform.position += moveVelocity * dt;
                yield return null;
            }

            float fadeElapsed = 0f;
            Color[] originalColors = null;

            if (spriteRenderers != null && spriteRenderers.Length > 0)
            {
                originalColors = new Color[spriteRenderers.Length];
                for (int i = 0; i < spriteRenderers.Length; i++)
                {
                    if (spriteRenderers[i] != null)
                    {
                        originalColors[i] = spriteRenderers[i].color;
                    }
                }
            }

            while (fadeElapsed < fadeDuration)
            {
                float dt = Time.deltaTime;
                fadeElapsed += dt;

                float t = Mathf.Clamp01(fadeElapsed / fadeDuration);
                float alpha = 1f - t;

                if (spriteRenderers != null && originalColors != null)
                {
                    for (int i = 0; i < spriteRenderers.Length; i++)
                    {
                        if (spriteRenderers[i] == null) continue;
                        Color c = originalColors[i];
                        c.a = alpha * c.a;
                        spriteRenderers[i].color = c;
                    }
                }

                transform.position += moveVelocity * Time.deltaTime;
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}

