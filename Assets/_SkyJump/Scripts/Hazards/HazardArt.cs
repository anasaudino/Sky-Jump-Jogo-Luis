using System.Collections.Generic;
using SkyJump.World;
using UnityEngine;

namespace SkyJump.Hazards
{
    /// <summary>
    /// Sprites em pixel art dos perigos, desenhados por código a partir de pequenas grades de
    /// caracteres (cada caractere é uma cor da paleta, '.' é transparente). Gerados uma vez e reutilizados.
    /// </summary>
    public static class HazardArt
    {
        const float PixelsPerUnit = 12f;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        // Pássaro virado para a direita; as asas são desenhadas por cima do corpo em cada quadro.
        static readonly string[] BirdBody =
        {
            "................",
            "................",
            "................",
            "..........kkkk..",
            ".........kbbbbk.",
            "..kkkkkkkbbbpbko",
            ".kbbbbbbbbbbbbko",
            "kbbbllllllbbbbk.",
            ".kkbllllllllbk..",
            "...kkkkkkkkkk...",
            "......k..k......",
        };
        static readonly string[] BirdWingUp =
        {
            "...kk", "..kwwk", "..kwwwk", "...kwwwk", "....kwwwk", "....kwwwk",
        };
        static readonly string[] BirdWingDown =
        {
            "", "", "", "", "", "", "....kwwwk", "....kwwwwk", "...kwwwwk", "...kwwk", "....kk",
        };

        static readonly string[] BatBody =
        {
            "................",
            "................",
            "......k..k......",
            "......kbbk......",
            ".....kbeebk.....",
            ".....kbbbbk.....",
            "......kbbk......",
            ".......kk.......",
            "................",
        };
        static readonly string[] BatWingUp =
        {
            "", "k..............k", "kk............kk", "kwk..........kwk", "kwwk........kwwk", ".kwwk......kwwk.", "..kkk......kkk..",
        };
        static readonly string[] BatWingDown =
        {
            "", "", "", "", "..kkk......kkk..", ".kwwk......kwwk.", "kwwwk......kwwwk", "kwk..........kwk", "k..............k",
        };

        static readonly string[] Rock =
        {
            "....kkkk....",
            "..kkrrrrkk..",
            ".krrllrrrrk.",
            ".krllrrrrdk.",
            "krrlrrrrrrdk",
            "krrrrrrrdrdk",
            "krrrrrrrrddk",
            "krrdrrrrrddk",
            ".krrrrrrddk.",
            ".kdrrrrdddk.",
            "..kkdddkkk..",
            "....kkkk....",
        };

        static readonly string[] Ice =
        {
            ".kkkkkk.",
            "kwwllldk",
            "kwlllddk",
            ".kwlldk.",
            ".kwlldk.",
            ".kwlddk.",
            "..klldk.",
            "..kldk..",
            "..kldk..",
            "...kdk..",
            "...kdk..",
            "...kk...",
            "...k....",
        };

        static readonly string[] Star =
        {
            "......k......",
            ".....kyk.....",
            ".....kyk.....",
            "....kyyyk....",
            "kkkkkywykkkkk",
            ".kyyyywyyyyk.",
            "..kyyyyyyyk..",
            "...kyyyyyk...",
            "...kyyyyyk...",
            "..kyyykyyyk..",
            "..kyyk.kyyk..",
            ".kyk.....kyk.",
            ".kk.......kk.",
        };

        static readonly string[] Warning =
        {
            ".kkkkkkk.",
            "kyyyyyyyk",
            "kyyykyyyk",
            "kyyykyyyk",
            "kyyykyyyk",
            "kyyykyyyk",
            "kyyyyyyyk",
            "kyyykyyyk",
            "kyyyyyyyk",
            ".kkkkkkk.",
        };

        /// <summary>Dois quadros de animação (asas para cima / para baixo), virados para a direita.</summary>
        public static Sprite[] Flyer(FlyerStyle style, Color body)
        {
            string key = $"{style}_{ColorUtility.ToHtmlStringRGB(body)}";
            var palette = new Dictionary<char, Color>
            {
                ['k'] = Color.Lerp(body, Color.black, 0.78f),
                ['b'] = body,
                ['l'] = Color.Lerp(body, Color.white, 0.55f),
                ['w'] = Color.Lerp(body, Color.black, 0.3f),
                ['p'] = new Color(0.06f, 0.05f, 0.08f),
                ['o'] = new Color(1f, 0.62f, 0.15f),
                ['e'] = new Color(1f, 0.3f, 0.25f),
            };
            return style == FlyerStyle.Bat
                ? new[] { Build(key + "_0", BatBody, BatWingUp, palette), Build(key + "_1", BatBody, BatWingDown, palette) }
                : new[] { Build(key + "_0", BirdBody, BirdWingUp, palette), Build(key + "_1", BirdBody, BirdWingDown, palette) };
        }

        public static Sprite Falling(FallingStyle style)
        {
            switch (style)
            {
                case FallingStyle.Ice:
                    return Build("ice", Ice, null, new Dictionary<char, Color>
                    {
                        ['k'] = new Color(0.12f, 0.25f, 0.45f), ['w'] = Color.white,
                        ['l'] = new Color(0.72f, 0.92f, 1f), ['d'] = new Color(0.4f, 0.7f, 0.95f),
                    });
                case FallingStyle.Star:
                    return Build("star", Star, null, new Dictionary<char, Color>
                    {
                        ['k'] = new Color(0.55f, 0.3f, 0.05f), ['y'] = new Color(1f, 0.85f, 0.25f), ['w'] = new Color(1f, 1f, 0.85f),
                    });
                default:
                    return Build("rock", Rock, null, new Dictionary<char, Color>
                    {
                        ['k'] = new Color(0.15f, 0.1f, 0.08f), ['r'] = new Color(0.52f, 0.43f, 0.37f),
                        ['l'] = new Color(0.72f, 0.64f, 0.56f), ['d'] = new Color(0.34f, 0.28f, 0.24f),
                    });
            }
        }

        public static Sprite WarningSign() => Build("warning", Warning, null, new Dictionary<char, Color>
        {
            ['k'] = new Color(0.45f, 0.05f, 0.05f), ['y'] = new Color(1f, 0.85f, 0.2f),
        });

        static Sprite Build(string key, string[] rows, string[] overlay, Dictionary<char, Color> palette)
        {
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            int h = rows.Length, w = 0;
            foreach (var r in rows) w = Mathf.Max(w, r.Length);
            var px = new Color[w * h];
            Paint(px, w, h, rows, palette);
            if (overlay != null) Paint(px, w, h, overlay, palette);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Hazard_" + key,
            };
            tex.SetPixels(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            cache[key] = sprite;
            return sprite;
        }

        /// <summary>Linha 0 é o topo do desenho. '.' (ou fora da linha) não altera o pixel.</summary>
        static void Paint(Color[] px, int w, int h, string[] rows, Dictionary<char, Color> palette)
        {
            for (int row = 0; row < rows.Length && row < h; row++)
            {
                string line = rows[row];
                for (int x = 0; x < line.Length && x < w; x++)
                    if (palette.TryGetValue(line[x], out var c)) px[(h - 1 - row) * w + x] = c;
            }
        }
    }
}
