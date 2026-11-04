using UnityEngine;
using UnityEngine.UI;

namespace SkyJump.UI
{
    /// <summary>
    /// Texto pixel art para uGUI. Cada pixel da fonte vira um quad nítido (sem filtro),
    /// garantindo a estética pixel art em qualquer resolução. Suporta várias linhas,
    /// alinhamento, ajuste automático ao retângulo, sombra e contorno.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("Sky Jump/UI/Pixel Text")]
    public class PixelText : MaskableGraphic
    {
        [TextArea(1, 6)]
        [SerializeField] string text = "TEXTO";
        [Tooltip("Tamanho máximo de cada pixel da fonte, em unidades do Canvas.")]
        [SerializeField] float pixelSize = 6f;
        [Tooltip("Reduz o tamanho do pixel para caber no retângulo, se necessário.")]
        [SerializeField] bool autoFit = true;
        [SerializeField] TextAnchor alignment = TextAnchor.MiddleCenter;
        [SerializeField] int lineSpacing = 3;
        [Header("Sombra")]
        [SerializeField] bool shadow = true;
        [SerializeField] Color shadowColor = new Color(0f, 0f, 0f, 0.65f);
        [SerializeField] Vector2Int shadowOffset = new Vector2Int(1, -1);
        [Header("Contorno")]
        [SerializeField] bool outline;
        [SerializeField] Color outlineColor = new Color(0.08f, 0.05f, 0.15f, 1f);

        public string Text
        {
            get => text;
            set
            {
                if (text == value) return;
                text = value;
                SetVerticesDirty();
            }
        }

        public float PixelSize
        {
            get => pixelSize;
            set { pixelSize = value; SetVerticesDirty(); }
        }

        public void Configure(float size, TextAnchor anchor, bool useOutline, Color outlineCol, bool useShadow)
        {
            pixelSize = size;
            alignment = anchor;
            outline = useOutline;
            outlineColor = outlineCol;
            shadow = useShadow;
            SetVerticesDirty();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (string.IsNullOrEmpty(text)) return;

            string[] lines = text.ToUpperInvariant().Replace("\r", "").Split('\n');
            int lineHeight = PixelFont.GlyphRows;
            int blockWidthPx = 0;
            var widths = new int[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                widths[i] = PixelFont.MeasureLine(lines[i]);
                blockWidthPx = Mathf.Max(blockWidthPx, widths[i]);
            }
            int blockHeightPx = lines.Length * lineHeight + (lines.Length - 1) * lineSpacing;

            // Margem extra para sombra/contorno não serem cortados.
            int extra = (outline ? 2 : 0) + (shadow ? 1 : 0);
            Rect rect = GetPixelAdjustedRect();
            float ps = pixelSize;
            if (autoFit && blockWidthPx > 0)
            {
                float fitW = rect.width / (blockWidthPx + extra);
                float fitH = rect.height / (blockHeightPx + extra);
                ps = Mathf.Min(ps, fitW, fitH);
            }
            if (ps <= 0f) return;

            float blockW = blockWidthPx * ps;
            float blockH = blockHeightPx * ps;

            // Posição vertical do topo do bloco.
            float top;
            switch (alignment)
            {
                case TextAnchor.UpperLeft: case TextAnchor.UpperCenter: case TextAnchor.UpperRight:
                    top = rect.yMax; break;
                case TextAnchor.LowerLeft: case TextAnchor.LowerCenter: case TextAnchor.LowerRight:
                    top = rect.yMin + blockH; break;
                default:
                    top = rect.center.y + blockH * 0.5f; break;
            }

            Color32 main = color;
            Color32 shadowCol = MultiplyAlpha(shadowColor, color.a);
            Color32 outlineCol = MultiplyAlpha(outlineColor, color.a);

            // Ordem de desenho: sombra -> contorno -> texto.
            if (shadow)
            {
                // Com contorno, a sombra vira uma "queda" sólida abaixo do contorno (efeito 3D).
                Vector2 off = outline ? new Vector2(shadowOffset.x, shadowOffset.y * 2) : (Vector2)shadowOffset;
                DrawBlock(vh, lines, widths, rect, top, ps, off, outline ? outlineCol : shadowCol);
            }
            if (outline)
            {
                for (int ox = -1; ox <= 1; ox++)
                    for (int oy = -1; oy <= 1; oy++)
                        if (ox != 0 || oy != 0)
                            DrawBlock(vh, lines, widths, rect, top, ps, new Vector2(ox, oy), outlineCol);
            }
            DrawBlock(vh, lines, widths, rect, top, ps, Vector2.zero, main);
        }

        static Color32 MultiplyAlpha(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        void DrawBlock(VertexHelper vh, string[] lines, int[] widths, Rect rect, float top, float ps, Vector2 offsetPx, Color32 col)
        {
            int lineHeight = PixelFont.GlyphRows;
            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li];
                float lineW = widths[li] * ps;
                float x;
                switch (alignment)
                {
                    case TextAnchor.UpperLeft: case TextAnchor.MiddleLeft: case TextAnchor.LowerLeft:
                        x = rect.xMin; break;
                    case TextAnchor.UpperRight: case TextAnchor.MiddleRight: case TextAnchor.LowerRight:
                        x = rect.xMax - lineW; break;
                    default:
                        x = rect.center.x - lineW * 0.5f; break;
                }
                // Alinha à grade de pixels para manter os pixels uniformes.
                x = Mathf.Round(x / ps) * ps + offsetPx.x * ps;
                float lineTop = top - li * (lineHeight + lineSpacing) * ps + offsetPx.y * ps;

                foreach (char ch in line)
                {
                    if (PixelFont.IsSpace(ch))
                    {
                        x += (PixelFont.SpaceWidth + PixelFont.LetterSpacing) * ps;
                        continue;
                    }
                    var g = PixelFont.Get(ch);
                    for (int r = 0; r < PixelFont.GlyphRows; r++)
                    {
                        int c = 0;
                        while (c < g.Width)
                        {
                            if (!g.Pixels[r, c]) { c++; continue; }
                            int start = c;
                            while (c < g.Width && g.Pixels[r, c]) c++;
                            // Junta pixels consecutivos de uma linha num único quad.
                            float x0 = x + start * ps;
                            float x1 = x + c * ps;
                            float y1 = lineTop - r * ps;
                            float y0 = y1 - ps;
                            AddQuad(vh, x0, y0, x1, y1, col);
                        }
                    }
                    x += (g.Width + PixelFont.LetterSpacing) * ps;
                }
            }
        }

        static void AddQuad(VertexHelper vh, float x0, float y0, float x1, float y1, Color32 col)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(x0, y0), col, Vector2.zero);
            vh.AddVert(new Vector3(x0, y1), col, Vector2.zero);
            vh.AddVert(new Vector3(x1, y1), col, Vector2.zero);
            vh.AddVert(new Vector3(x1, y0), col, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
