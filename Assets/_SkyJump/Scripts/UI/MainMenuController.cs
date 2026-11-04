using System.Collections;
using SkyJump.Audio;
using SkyJump.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SkyJump.UI
{
    /// <summary>Menu principal: Jogar, Créditos e Sair, além do recorde salvo.</summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] CanvasGroup mainGroup;
        [SerializeField] CanvasGroup creditsPanel;
        [SerializeField] Selectable playButton;
        [SerializeField] Selectable creditsBackButton;
        [SerializeField] Selectable creditsButton;
        [SerializeField] PixelText bestScoreText;

        void Start()
        {
            Time.timeScale = 1f;
            AudioManager.SetMusicDucked(false);
            AudioManager.PlayMusic(Music.Menu);
            SetGroup(creditsPanel, false, 0f);
            SetGroup(mainGroup, true, 1f);
            int best = ScoreManager.BestScore;
            if (bestScoreText != null) bestScoreText.Text = best > 0 ? $"RECORDE: {best}" : "";
            Select(playButton);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && creditsPanel.gameObject.activeSelf) CloseCredits();
        }

        public void Play() => ScreenFader.LoadScene(SceneNames.Game);

        public void OpenCredits()
        {
            StopAllCoroutines();
            StartCoroutine(Swap(mainGroup, creditsPanel, creditsBackButton));
        }

        public void CloseCredits()
        {
            StopAllCoroutines();
            StartCoroutine(Swap(creditsPanel, mainGroup, creditsButton));
        }

        public void Quit()
        {
#if UNITY_EDITOR
            // No editor, "Sair" encerra o modo Play.
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        IEnumerator Swap(CanvasGroup from, CanvasGroup to, Selectable select)
        {
            from.interactable = false;
            to.gameObject.SetActive(true);
            to.interactable = false;
            float t = 0f;
            const float d = 0.25f;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                from.alpha = 1f - t / d;
                to.alpha = t / d;
                yield return null;
            }
            SetGroup(from, false, 0f);
            SetGroup(to, true, 1f);
            Select(select);
        }

        static void SetGroup(CanvasGroup g, bool on, float alpha)
        {
            if (g == null) return;
            g.alpha = alpha;
            g.interactable = on;
            g.blocksRaycasts = on;
            g.gameObject.SetActive(on);
        }

        static void Select(Selectable s)
        {
            if (s != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
        }
    }
}
