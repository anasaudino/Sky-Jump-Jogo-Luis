using UnityEngine;

namespace SkyJump.UI
{
    /// <summary>Animação decorativa: elemento de UI quicando (ex.: a bola no menu).</summary>
    public class UIBounce : MonoBehaviour
    {
        [SerializeField] float height = 40f;
        [SerializeField] float period = 0.9f;
        [SerializeField] bool squash = true;

        RectTransform rect;
        Vector2 basePos;

        void Awake()
        {
            rect = GetComponent<RectTransform>();
            basePos = rect.anchoredPosition;
        }

        void Update()
        {
            float t = Mathf.Repeat(Time.unscaledTime / period, 1f);
            float h = 4f * t * (1f - t); // parábola: 0 → 1 → 0
            rect.anchoredPosition = basePos + Vector2.up * (h * height);
            if (squash)
            {
                float s = h < 0.12f ? (0.12f - h) * 1.6f : 0f;
                rect.localScale = new Vector3(1f + s, 1f - s, 1f);
            }
        }
    }
}
