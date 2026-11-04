using System.Collections;
using SkyJump.CameraSystem;
using UnityEngine;

namespace SkyJump.World
{
    /// <summary>
    /// Cenário de fundo preso à câmera. Escolhe a versão vertical ou horizontal do mapa conforme
    /// a orientação, escala sem distorcer (modo "cobrir") e desliza levemente com o progresso
    /// dentro do mundo (sensação de subida). Na troca de mundo faz um crossfade suave.
    /// </summary>
    public class BackgroundController : MonoBehaviour
    {
        [SerializeField] CameraController cameraController;
        [SerializeField] LevelManager level;
        [SerializeField] SpriteRenderer layerA;
        [SerializeField] SpriteRenderer layerB;
        [Tooltip("Folga extra de escala para permitir o deslocamento vertical (parallax).")]
        [SerializeField] float parallaxOverscan = 1.18f;
        [SerializeField] float crossfadeDuration = 1.6f;

        int shownWorld = -1;
        int fadingWorld = -1;
        Coroutine fade;

        void OnEnable()
        {
            if (level != null) level.WorldChanged += OnWorldChanged;
        }

        void OnDisable()
        {
            if (level != null) level.WorldChanged -= OnWorldChanged;
        }

        void Start()
        {
            shownWorld = level.CurrentWorldIndex;
            layerB.color = new Color(1, 1, 1, 0);
            Refresh();
        }

        void OnWorldChanged(int index, int previous)
        {
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(Crossfade(index));
        }

        IEnumerator Crossfade(int toWorld)
        {
            fadingWorld = toWorld;
            float t = 0f;
            while (t < crossfadeDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / crossfadeDuration);
                layerB.color = new Color(1, 1, 1, k);
                yield return null;
            }
            shownWorld = toWorld;
            fadingWorld = -1;
            layerB.color = new Color(1, 1, 1, 0);
            Refresh();
            fade = null;
        }

        void LateUpdate() => Refresh();

        void Refresh()
        {
            if (cameraController == null || level == null || shownWorld < 0) return;
            FitLayer(layerA, shownWorld, 10f);
            if (fadingWorld >= 0) FitLayer(layerB, fadingWorld, 9f);
        }

        void FitLayer(SpriteRenderer sr, int worldIndex, float z)
        {
            var world = level.GetWorld(worldIndex);
            bool portrait = cameraController.IsPortrait;
            Sprite sprite = world.BackgroundFor(portrait);
            if (sr.sprite != sprite) sr.sprite = sprite;
            if (sprite == null) return;

            float viewH = cameraController.HalfHeight * 2f;
            float viewW = cameraController.HalfWidth * 2f;
            Vector2 size = sprite.bounds.size;
            // "Cobrir": preenche a tela inteira mantendo a proporção da arte.
            float scale = Mathf.Max(viewW / size.x, viewH / size.y) * parallaxOverscan;
            sr.transform.localScale = new Vector3(scale, scale, 1f);

            // Desliza de baixo para cima conforme o progresso dentro daquele mundo.
            float spare = Mathf.Max(0f, size.y * scale - viewH) * 0.5f;
            float camY = cameraController.transform.position.y;
            float start = level.WorldStart(worldIndex);
            float progress = Mathf.Clamp01((camY - start) / world.length);
            float offsetY = Mathf.Lerp(spare, -spare, progress);
            sr.transform.localPosition = new Vector3(0f, offsetY, z);
        }
    }
}
