using UnityEngine;
using UnityEngine.UI;

namespace SkyJump.UI
{
    /// <summary>
    /// Ajusta o CanvasScaler para que o MENOR lado da tela sempre equivalha à resolução de
    /// referência (1080). Assim a interface mantém o mesmo tamanho relativo em retrato e paisagem,
    /// sem esticar, e os elementos ancorados se reorganizam no espaço extra.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    [ExecuteAlways]
    public class ResponsiveCanvas : MonoBehaviour
    {
        [SerializeField] float referenceShortSide = 1080f;

        CanvasScaler scaler;
        int lastW, lastH;

        public static bool IsPortrait => SkyJump.Core.ScreenInfo.Height >= SkyJump.Core.ScreenInfo.Width;

        void OnEnable()
        {
            scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(referenceShortSide, referenceShortSide);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            lastW = lastH = -1;
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            if (SkyJump.Core.ScreenInfo.Width == lastW && SkyJump.Core.ScreenInfo.Height == lastH) return;
            lastW = SkyJump.Core.ScreenInfo.Width;
            lastH = SkyJump.Core.ScreenInfo.Height;
            // Retrato: casa pela largura (lado menor). Paisagem: casa pela altura.
            scaler.matchWidthOrHeight = IsPortrait ? 0f : 1f;
        }
    }
}
