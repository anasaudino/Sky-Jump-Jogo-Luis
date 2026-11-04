using UnityEngine;

namespace SkyJump.Core
{
    /// <summary>
    /// Tamanho da tela usado pelos sistemas responsivos. Permite simular outra resolução
    /// (ex.: testes automatizados em retrato e paisagem); no jogo normal repassa Screen.*.
    /// </summary>
    public static class ScreenInfo
    {
        public static Vector2Int? Override;

        public static int Width => Override?.x ?? Screen.width;
        public static int Height => Override?.y ?? Screen.height;
        public static bool IsPortrait => Height >= Width;
        public static Rect SafeArea => Override.HasValue ? new Rect(0, 0, Width, Height) : Screen.safeArea;
    }
}
