using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyJump.UI
{
    /// <summary>Fade preto entre cenas (fade in ao abrir, fade out antes de trocar).</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenFader : MonoBehaviour
    {
        [SerializeField] float duration = 0.45f;

        static ScreenFader current;
        CanvasGroup group;
        bool loading;

        void Awake()
        {
            current = this;
            group = GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }

        IEnumerator Start()
        {
            yield return Fade(1f, 0f);
            group.blocksRaycasts = false;
        }

        public static void LoadScene(string sceneName)
        {
            if (current != null) current.StartLoad(sceneName);
            else SceneManager.LoadScene(sceneName);
        }

        void StartLoad(string sceneName)
        {
            if (loading) return;
            loading = true;
            StopAllCoroutines();
            StartCoroutine(LoadRoutine(sceneName));
        }

        IEnumerator LoadRoutine(string sceneName)
        {
            group.blocksRaycasts = true;
            yield return Fade(group.alpha, 1f);
            SceneManager.LoadScene(sceneName);
        }

        IEnumerator Fade(float from, float to)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            group.alpha = to;
        }
    }
}
