using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SkyJump.EditorTools
{
    /// <summary>
    /// Prepara a versão para distribuição: nome do jogo, ícone em pixel art gerado a partir
    /// do personagem, sem tela de abertura da Unity, e gera o executável do Windows.
    /// Menu: Sky Jump → Gerar Build Windows.
    /// </summary>
    public static class SkyJumpRelease
    {
        public const string ProductName = "Sky Jump";
        public const string CompanyName = "Ana Luiza S. B. de Souza";
        public const string Version = "1.0";
        public const string OutputDir = "Builds/Sky Jump";
        const string IconPath = "Assets/_SkyJump/Art/Icon/AppIcon.png";
        const string CharacterPath = "Assets/_SkyJump/Art/Character/Personagem.png";

        [MenuItem("Sky Jump/Gerar Build Windows")]
        public static void BuildWindowsMenu()
        {
            var report = BuildWindows();
            EditorUtility.DisplayDialog("Sky Jump", report, "OK");
        }

        public static string BuildWindows()
        {
            ApplyIdentity();
            if (Directory.Exists(OutputDir)) Directory.Delete(OutputDir, true);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_SkyJump/Scenes/MainMenu.unity", "Assets/_SkyJump/Scenes/Game.unity" },
                locationPathName = $"{OutputDir}/{ProductName}.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var s = BuildPipeline.BuildPlayer(options).summary;
            return $"result={s.result} errors={s.totalErrors} size={s.totalSize / (1024f * 1024f):F1}MB time={s.totalTime:mm\\:ss}";
        }

        public static void ApplyIdentity()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;

            var icon = GenerateIcon();
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Ícone 256×256 em pixel art: o personagem com contorno escuro sobre um céu em degradê,
        /// com uma plataforma embaixo e cantos arredondados. Desenhado em 64×64 e ampliado 4×.
        /// </summary>
        static Texture2D GenerateIcon()
        {
            if (!File.Exists(CharacterPath)) return null;
            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(CharacterPath));

            const int N = 64, Scale = 4, CharSize = 44;
            var px = new Color[N * N];
            var skyTop = new Color32(58, 118, 214, 255);
            var skyBottom = new Color32(150, 214, 255, 255);
            var ink = new Color32(26, 18, 46, 255);

            // Fundo: céu com faixas (dither de pixel art) e cantos arredondados.
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float t = Mathf.Floor(y / 8f) / 7f;
                    px[y * N + x] = Color.Lerp(skyBottom, skyTop, t);
                }
            // Nuvenzinhas.
            Blob(px, N, 10, 50, 7, 3, Color.white);
            Blob(px, N, 52, 40, 6, 2, Color.white);

            // Plataforma.
            for (int x = 12; x < 52; x++)
            {
                for (int y = 5; y < 9; y++) px[y * N + x] = new Color32(96, 168, 72, 255);
                for (int y = 2; y < 5; y++) if (x > 14 + (5 - y) && x < 49 - (5 - y)) px[y * N + x] = new Color32(120, 84, 56, 255);
                px[9 * N + x] = new Color32(140, 214, 96, 255);
            }

            // Personagem reduzido (vizinho mais próximo) com contorno.
            int ox = (N - CharSize) / 2, oy = 13;
            var mask = new bool[N * N];
            var charPx = new Color[N * N];
            for (int y = 0; y < CharSize; y++)
                for (int x = 0; x < CharSize; x++)
                {
                    var c = src.GetPixel((int)((x + 0.5f) / CharSize * src.width), (int)((y + 0.5f) / CharSize * src.height));
                    if (c.a < 0.5f) continue;
                    int i = (oy + y) * N + ox + x;
                    mask[i] = true;
                    c.a = 1f;
                    charPx[i] = c;
                }
            for (int y = 1; y < N - 1; y++)
                for (int x = 1; x < N - 1; x++)
                {
                    int i = y * N + x;
                    if (mask[i]) { px[i] = charPx[i]; continue; }
                    if (mask[i - 1] || mask[i + 1] || mask[i - N] || mask[i + N]) px[i] = ink;
                }

            // Cantos arredondados (raio 8) com borda escura.
            const int R = 8;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = CornerDistance(x, y, N, R);
                    if (d > R) px[y * N + x] = Color.clear;
                    else if (d > R - 1.3f || x == 0 || y == 0 || x == N - 1 || y == N - 1) px[y * N + x] = ink;
                }

            var big = new Texture2D(N * Scale, N * Scale, TextureFormat.RGBA32, false);
            var bigPx = new Color[N * Scale * N * Scale];
            for (int y = 0; y < N * Scale; y++)
                for (int x = 0; x < N * Scale; x++)
                    bigPx[y * N * Scale + x] = px[(y / Scale) * N + x / Scale];
            big.SetPixels(bigPx);
            big.Apply();
            Object.DestroyImmediate(src);

            Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
            File.WriteAllBytes(IconPath, big.EncodeToPNG());
            Object.DestroyImmediate(big);
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        }

        static float CornerDistance(int x, int y, int n, int r)
        {
            float cx = x < r ? r : (x >= n - r ? n - r - 1 : x);
            float cy = y < r ? r : (y >= n - r ? n - r - 1 : y);
            if (cx == x && cy == y) return 0f;
            return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        }

        static void Blob(Color[] px, int n, int cx, int cy, int rx, int ry, Color c)
        {
            for (int y = -ry; y <= ry; y++)
                for (int x = -rx; x <= rx; x++)
                {
                    if ((x * x) / (float)(rx * rx) + (y * y) / (float)(ry * ry) > 1f) continue;
                    int px_ = cx + x, py = cy + y;
                    if (px_ >= 0 && px_ < n && py >= 0 && py < n) px[py * n + px_] = c;
                }
        }
    }
}
