using UnityEngine;
using UnityEngine.UI;

namespace SkyJump.UI
{
    /// <summary>
    /// Imagem de tela cheia que usa a arte vertical em retrato e a horizontal em paisagem,
    /// sempre cobrindo a tela sem distorcer (AspectRatioFitter em modo EnvelopeParent).
    /// </summary>
    [RequireComponent(typeof(Image), typeof(AspectRatioFitter))]
    [ExecuteAlways]
    public class OrientationImage : MonoBehaviour
    {
        [SerializeField] Sprite portraitSprite;
        [SerializeField] Sprite landscapeSprite;

        Image image;
        AspectRatioFitter fitter;
        int lastW, lastH;

        public void SetSprites(Sprite portrait, Sprite landscape)
        {
            portraitSprite = portrait;
            landscapeSprite = landscape;
            lastW = -1;
            Apply();
        }

        void OnEnable()
        {
            image = GetComponent<Image>();
            fitter = GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            lastW = -1;
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            if (image == null) return;
            if (SkyJump.Core.ScreenInfo.Width == lastW && SkyJump.Core.ScreenInfo.Height == lastH) return;
            lastW = SkyJump.Core.ScreenInfo.Width;
            lastH = SkyJump.Core.ScreenInfo.Height;
            bool portrait = SkyJump.Core.ScreenInfo.Height >= SkyJump.Core.ScreenInfo.Width;
            Sprite s = portrait ? portraitSprite : landscapeSprite;
            if (s == null) s = portraitSprite != null ? portraitSprite : landscapeSprite;
            if (s == null) return;
            image.sprite = s;
            fitter.aspectRatio = s.rect.width / s.rect.height;
        }
    }
}
