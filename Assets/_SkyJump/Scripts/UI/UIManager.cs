using System.Collections;
using SkyJump.Core;
using SkyJump.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SkyJump.UI
{
    /// <summary>
    /// Interface da partida: HUD (vidas, pontos, progresso), aviso de mundo, dica de controles,
    /// pausa e telas de derrota/vitória. Apenas exibe dados; as regras ficam no GameManager.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("Sistemas")]
        [SerializeField] ScoreManager score;
        [SerializeField] LifeManager lives;
        [SerializeField] LevelManager level;

        [Header("HUD")]
        [SerializeField] Image[] hearts;
        [SerializeField] Sprite heartFull;
        [SerializeField] Sprite heartEmpty;
        [SerializeField] PixelText scoreText;
        [SerializeField] PixelText worldText;
        [SerializeField] Image progressFill;
        [SerializeField] CanvasGroup hud;

        [Header("Avisos")]
        [SerializeField] CanvasGroup banner;
        [SerializeField] PixelText bannerTitle;
        [SerializeField] PixelText bannerSubtitle;
        [SerializeField] CanvasGroup hint;
        [SerializeField] PixelText hintText;
        [SerializeField] CanvasGroup touchControls;
        [SerializeField] Image flash;

        [Header("Painéis")]
        [SerializeField] CanvasGroup pausePanel;
        [SerializeField] Selectable pauseFirst;
        [SerializeField] CanvasGroup gameOverPanel;
        [SerializeField] PixelText gameOverScore;
        [SerializeField] PixelText gameOverBest;
        [SerializeField] Selectable gameOverFirst;
        [SerializeField] CanvasGroup victoryPanel;
        [SerializeField] PixelText victoryScore;
        [SerializeField] PixelText victoryBest;
        [SerializeField] PixelText victoryDetail;
        [SerializeField] Selectable victoryFirst;

        Coroutine bannerRoutine;
        Coroutine flashRoutine;
        int shownLives = -1;

        void OnEnable()
        {
            if (score != null) score.ScoreChanged += OnScoreChanged;
            if (lives != null) lives.LivesChanged += OnLivesChanged;
        }

        void OnDisable()
        {
            if (score != null) score.ScoreChanged -= OnScoreChanged;
            if (lives != null) lives.LivesChanged -= OnLivesChanged;
        }

        void Start()
        {
            HideInstant(pausePanel);
            HideInstant(gameOverPanel);
            HideInstant(victoryPanel);
            if (flash != null) flash.color = Color.clear;
            if (banner != null) banner.alpha = 0f;

            bool touch = IsTouchDevice();
            if (touchControls != null) touchControls.alpha = touch ? 0.35f : 0f;
            if (hintText != null)
                hintText.Text = touch ? "SEGURE ESQUERDA OU DIREITA\nPARA MOVER" : "A / D OU SETAS PARA MOVER\nESC PARA PAUSAR";

            OnScoreChanged(score.Score);
            OnLivesChanged(lives.Lives);
            ShowWorldBanner(0, level.GetWorld(0));
            if (hint != null) StartCoroutine(HintRoutine());
        }

        static bool IsTouchDevice() => Touchscreen.current != null && Application.isMobilePlatform;

        void Update()
        {
            if (progressFill != null) progressFill.fillAmount = level.TotalProgress(score.MaxHeight);
            if (worldText != null) worldText.Text = level.CurrentWorld.displayName;
            if (touchControls != null && touchControls.alpha < 0.01f && Touchscreen.current != null &&
                Touchscreen.current.primaryTouch.press.isPressed)
                touchControls.alpha = 0.35f; // mostra as setas assim que um toque é detectado
        }

        void OnScoreChanged(int value)
        {
            if (scoreText != null) scoreText.Text = value.ToString();
        }

        void OnLivesChanged(int value)
        {
            for (int i = 0; i < hearts.Length; i++)
            {
                hearts[i].sprite = i < value ? heartFull : heartEmpty;
                if (shownLives >= 0 && i == value && value < shownLives) StartCoroutine(Punch(hearts[i].rectTransform));
            }
            shownLives = value;
        }

        IEnumerator Punch(RectTransform rt)
        {
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                float s = 1f + 0.5f * Mathf.Sin(t / 0.35f * Mathf.PI);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        IEnumerator HintRoutine()
        {
            hint.alpha = 0f;
            yield return new WaitForSeconds(0.6f);
            yield return FadeGroup(hint, 0f, 1f, 0.4f);
            yield return new WaitForSeconds(3.5f);
            yield return FadeGroup(hint, 1f, 0f, 0.6f);
        }

        public void ShowWorldBanner(int index, WorldData world)
        {
            if (banner == null) return;
            bannerTitle.Text = index == 0 ? "MUNDO 1" : $"MUNDO {index + 1}";
            bannerSubtitle.Text = world.displayName;
            bannerSubtitle.color = world.themeColor;
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine());
            if (index > 0) Flash(new Color(1f, 1f, 1f, 0.55f), 0.9f);
        }

        IEnumerator BannerRoutine()
        {
            yield return FadeGroup(banner, banner.alpha, 1f, 0.35f);
            yield return new WaitForSeconds(1.6f);
            yield return FadeGroup(banner, 1f, 0f, 0.6f);
        }

        public void FlashDamage() => Flash(new Color(0.9f, 0.1f, 0.1f, 0.45f), 0.5f);

        void Flash(Color color, float duration)
        {
            if (flash == null) return;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine(color, duration));
        }

        IEnumerator FlashRoutine(Color color, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                flash.color = new Color(color.r, color.g, color.b, color.a * (1f - t / duration));
                yield return null;
            }
            flash.color = Color.clear;
        }

        public void ShowPause(bool show)
        {
            if (show) StartCoroutine(Show(pausePanel, pauseFirst));
            else HideInstant(pausePanel);
        }

        public void ShowGameOver(int finalScore, int best, bool newRecord)
        {
            gameOverScore.Text = $"PONTUAÇÃO: {finalScore}";
            gameOverBest.Text = newRecord ? "NOVO RECORDE!" : $"RECORDE: {best}";
            if (hud != null) hud.alpha = 0f;
            StartCoroutine(Show(gameOverPanel, gameOverFirst));
        }

        public void ShowVictory(int finalScore, int best, bool newRecord, int livesLeft)
        {
            victoryScore.Text = $"PONTUAÇÃO: {finalScore}";
            victoryBest.Text = newRecord ? "NOVO RECORDE!" : $"RECORDE: {best}";
            victoryDetail.Text = $"VIDAS RESTANTES: {livesLeft}";
            if (hud != null) hud.alpha = 0f;
            StartCoroutine(Show(victoryPanel, victoryFirst));
        }

        IEnumerator Show(CanvasGroup group, Selectable first)
        {
            group.gameObject.SetActive(true);
            group.interactable = true;
            group.blocksRaycasts = true;
            if (first != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
            yield return FadeGroup(group, 0f, 1f, 0.35f);
        }

        static void HideInstant(CanvasGroup group)
        {
            if (group == null) return;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            group.gameObject.SetActive(false);
        }

        static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
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
