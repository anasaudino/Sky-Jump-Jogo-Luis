using UnityEngine;

namespace SkyJump.UI
{
    /// <summary>Mantém o conteúdo dentro da área segura (notch/bordas arredondadas de celulares).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        RectTransform rect;
        Rect lastSafe;
        Vector2Int lastSize;

        void Awake()
        {
            rect = GetComponent<RectTransform>();
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            Rect safe = SkyJump.Core.ScreenInfo.SafeArea;
            var size = new Vector2Int(SkyJump.Core.ScreenInfo.Width, SkyJump.Core.ScreenInfo.Height);
            if (safe == lastSafe && size == lastSize) return;
            lastSafe = safe;
            lastSize = size;
            if (size.x <= 0 || size.y <= 0) return;

            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= size.x; min.y /= size.y;
            max.x /= size.x; max.y /= size.y;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
