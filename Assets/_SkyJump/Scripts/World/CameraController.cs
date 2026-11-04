using System;
using SkyJump.Player;
using UnityEngine;

namespace SkyJump.CameraSystem
{
    /// <summary>
    /// Câmera ortográfica que só sobe, acompanhando suavemente o jogador.
    /// Adapta o enquadramento à proporção da tela: em retrato a coluna jogável ocupa a largura
    /// inteira; em paisagem a altura visível é mantida e as laterais mostram o cenário escurecido.
    /// A altura visível fica parecida nas duas orientações, mantendo a jogabilidade justa.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [Tooltip("Meia-altura mínima visível (orthographicSize mínimo).")]
        [SerializeField] float minHalfHeight = 9f;
        [Tooltip("Margem lateral extra além da coluna jogável.")]
        [SerializeField] float sideMargin = 0.3f;
        [Tooltip("Quanto da meia-altura a câmera fica acima do jogador (mostra as próximas plataformas).")]
        [Range(0f, 0.8f)] [SerializeField] float lookAhead = 0.3f;
        [SerializeField] float smoothTime = 0.18f;

        [Header("Faixas laterais (paisagem)")]
        [SerializeField] SpriteRenderer leftBand;
        [SerializeField] SpriteRenderer rightBand;
        [SerializeField] SpriteRenderer leftEdge;
        [SerializeField] SpriteRenderer rightEdge;

        Camera cam;
        float targetY;
        float velocity;
        int lastW, lastH;
        bool following = true;

        public Camera Camera => cam;
        public float HalfHeight => cam.orthographicSize;
        public float HalfWidth => cam.orthographicSize * cam.aspect;
        public float BottomY => transform.position.y - HalfHeight;
        public float TopY => transform.position.y + HalfHeight;
        public bool IsPortrait => cam.aspect < 1f;
        public float MaxY { get; set; } = float.MaxValue;

        /// <summary>Disparado quando o tamanho/proporção da tela muda.</summary>
        public event Action ViewChanged;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            Fit(true);
        }

        public void ResetTo(float floorY)
        {
            Fit(true);
            targetY = floorY + HalfHeight - 2f;
            velocity = 0f;
            SetY(targetY);
            following = true;
        }

        public void SetFollowing(bool value) => following = value;

        void LateUpdate()
        {
            Fit(false);
            if (!following || player == null) return;

            float desired = player.transform.position.y + HalfHeight * lookAhead;
            // Nunca desce: só acompanha a subida.
            targetY = Mathf.Min(Mathf.Max(targetY, desired), MaxY);
            float y = Mathf.SmoothDamp(transform.position.y, targetY, ref velocity, smoothTime);
            SetY(y);
        }

        void SetY(float y)
        {
            var p = transform.position;
            transform.position = new Vector3(0f, y, p.z);
        }

        void Fit(bool force)
        {
            if (!force && SkyJump.Core.ScreenInfo.Width == lastW && SkyJump.Core.ScreenInfo.Height == lastH) return;
            lastW = SkyJump.Core.ScreenInfo.Width;
            lastH = SkyJump.Core.ScreenInfo.Height;

            float halfWidthNeeded = (player != null ? player.PlayHalfWidth : 5f) + sideMargin;
            float aspect = Mathf.Max(0.1f, cam.aspect);
            cam.orthographicSize = Mathf.Max(minHalfHeight, halfWidthNeeded / aspect);
            UpdateBands();
            ViewChanged?.Invoke();
        }

        void UpdateBands()
        {
            float playHalf = player != null ? player.PlayHalfWidth : 5f;
            float halfW = HalfWidth;
            float bandWidth = Mathf.Max(0f, halfW - playHalf);
            bool show = bandWidth > 0.35f;
            float h = HalfHeight * 2f + 2f;

            SetBand(leftBand, show, new Vector3(-playHalf - bandWidth * 0.5f, 0f, 5f), new Vector3(bandWidth, h, 1f));
            SetBand(rightBand, show, new Vector3(playHalf + bandWidth * 0.5f, 0f, 5f), new Vector3(bandWidth, h, 1f));
            SetBand(leftEdge, show, new Vector3(-playHalf - 0.06f, 0f, 5f), new Vector3(0.12f, h, 1f));
            SetBand(rightEdge, show, new Vector3(playHalf + 0.06f, 0f, 5f), new Vector3(0.12f, h, 1f));
        }

        static void SetBand(SpriteRenderer sr, bool show, Vector3 localPos, Vector3 size)
        {
            if (sr == null) return;
            sr.gameObject.SetActive(show);
            if (!show) return;
            sr.transform.localPosition = localPos;
            // O sprite base mede 1x1 unidade (pixel branco com PPU = tamanho).
            Vector2 s = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
            sr.transform.localScale = new Vector3(size.x / s.x, size.y / s.y, 1f);
        }
    }
}
