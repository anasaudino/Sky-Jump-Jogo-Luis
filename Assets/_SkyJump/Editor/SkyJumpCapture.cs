using System.Collections.Generic;
using System.IO;
using SkyJump.Core;
using UnityEngine;

namespace SkyJump.EditorTools
{
    /// <summary>
    /// Ferramenta de teste: renderiza o jogo (cena + UI) numa resolução simulada, para validar
    /// o layout em retrato e paisagem durante o Play Mode. Uso em duas etapas (Prepare → Finish)
    /// para que os sistemas responsivos atualizem entre elas.
    /// </summary>
    public static class SkyJumpCapture
    {
        static RenderTexture rt;
        static Camera cam;
        static readonly List<(Canvas canvas, RenderMode mode, Camera cam)> canvases = new List<(Canvas, RenderMode, Camera)>();

        public static void Prepare(int width, int height)
        {
            Restore();
            ScreenInfo.Override = new Vector2Int(width, height);
            cam = Camera.main;
            rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas) continue;
                canvases.Add((c, c.renderMode, c.worldCamera));
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
                c.sortingOrder = 1000; // UI acima dos sprites, como no modo Overlay
            }
        }

        public static string Finish(string fileName)
        {
            if (cam == null || rt == null) return "Prepare não foi chamado";
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            string dir = Path.Combine(Application.dataPath, "../Temp/Captures");
            Directory.CreateDirectory(dir);
            string path = Path.GetFullPath(Path.Combine(dir, fileName));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Restore();
            return path;
        }

        public static void Restore()
        {
            foreach (var (canvas, mode, c) in canvases)
            {
                if (canvas == null) continue;
                canvas.renderMode = mode;
                canvas.worldCamera = c;
            }
            canvases.Clear();
            if (cam != null) cam.targetTexture = null;
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            rt = null;
            cam = null;
            ScreenInfo.Override = null;
        }
    }
}
