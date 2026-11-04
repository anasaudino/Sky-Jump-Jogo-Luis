using System;
using System.Collections;
using SkyJump.Audio;
using SkyJump.CameraSystem;
using SkyJump.FX;
using SkyJump.Platforms;
using SkyJump.Player;
using SkyJump.UI;
using SkyJump.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SkyJump.Core
{
    public static class SceneNames
    {
        public const string MainMenu = "MainMenu";
        public const string Game = "Game";
    }

    /// <summary>
    /// Controla o estado da partida (Playing, Paused, GameOver, Victory) e coordena os sistemas:
    /// vidas, pontuação, mundos, câmera, plataformas e interface.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Sistemas")]
        [SerializeField] PlayerController player;
        [SerializeField] PlatformSpawner spawner;
        [SerializeField] CameraController cameraController;
        [SerializeField] LevelManager level;
        [SerializeField] ScoreManager score;
        [SerializeField] LifeManager lives;
        [SerializeField] UIManager ui;

        [Header("Regras")]
        [Tooltip("Quanto abaixo da borda inferior da câmera o jogador pode cair antes de perder vida.")]
        [SerializeField] float fallMargin = 0.8f;
        [SerializeField] int bonusPerRemainingLife = 500;
        [SerializeField] float gameOverDelay = 0.8f;

        public GameState State { get; private set; } = GameState.MainMenu;
        public event Action<GameState> StateChanged;

        void OnEnable()
        {
            if (level != null) level.WorldChanged += OnWorldChanged;
            if (player != null) player.Bounced += OnPlayerBounced;
        }

        void OnDisable()
        {
            if (level != null) level.WorldChanged -= OnWorldChanged;
            if (player != null) player.Bounced -= OnPlayerBounced;
        }

        void Start() => StartNewGame();

        public void StartNewGame()
        {
            Time.timeScale = 1f;
            float baseY = level.BaseHeight;
            lives.ResetLives();
            score.ResetScore(baseY);
            cameraController.ResetTo(baseY);
            cameraController.MaxY = level.GoalHeight + 2f;
            spawner.Begin();

            player.transform.position = new Vector3(0f, baseY + 1.5f, 0f);
            player.SetActiveControl(true);
            SetState(GameState.Playing);
            AudioManager.SetMusicDucked(false);
            AudioManager.PlayMusic(AudioManager.WorldMusic(0));
        }

        void Update()
        {
            if (WasPausePressed()) TogglePause();
            if (State != GameState.Playing) return;

            float y = player.transform.position.y;
            score.ReportHeight(y);
            level.ReportHeight(score.MaxHeight);

            if (player.ControlEnabled && y < cameraController.BottomY - fallMargin)
                HandleFall();
        }

        static bool WasPausePressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) return true;
            var pad = Gamepad.current;
            return pad != null && pad.startButton.wasPressedThisFrame;
        }

        void OnPlayerBounced(Platform platform)
        {
            AudioManager.Play(Sfx.Jump, 0.6f);
            if (platform != null && (platform.Type == PlatformType.Crumbling)) AudioManager.Play(Sfx.Break, 0.8f);
        }

        void OnWorldChanged(int index, int previous)
        {
            if (State != GameState.Playing) return;
            for (int i = previous; i < index; i++) score.AddBonus(level.GetWorld(i).completionBonus);
            AudioManager.Play(Sfx.WorldChange);
            AudioManager.PlayMusic(AudioManager.WorldMusic(index));
            PixelParticles.Burst(player.transform.position, level.GetWorld(index).themeColor, 24, 5f);
            ui.ShowWorldBanner(index, level.GetWorld(index));
        }

        void HandleFall()
        {
            int remaining = lives.LoseLife();
            AudioManager.Play(Sfx.LoseLife);
            ui.FlashDamage();

            if (remaining <= 0)
            {
                StartCoroutine(GameOverRoutine());
                return;
            }

            Vector2 respawn = spawner.CreateRescuePlatform();
            player.Respawn(respawn);
        }

        IEnumerator GameOverRoutine()
        {
            SetState(GameState.GameOver);
            AudioManager.StopMusic();
            player.SetActiveControl(false);
            spawner.Stop();
            cameraController.SetFollowing(false);
            yield return new WaitForSeconds(gameOverDelay);
            bool record = score.SaveBest();
            AudioManager.Play(Sfx.Defeat);
            ui.ShowGameOver(score.Score, ScoreManager.BestScore, record);
        }

        public void Victory()
        {
            if (State != GameState.Playing) return;
            SetState(GameState.Victory);
            AudioManager.StopMusic();
            score.AddBonus(level.GetWorld(level.WorldCount - 1).completionBonus);
            score.AddBonus(lives.Lives * bonusPerRemainingLife);
            player.SetActiveControl(false);
            spawner.Stop();
            PixelParticles.Burst(player.transform.position, new Color(1f, 0.9f, 0.4f), 40, 6f);
            bool record = score.SaveBest();
            AudioManager.Play(Sfx.Victory);
            ui.ShowVictory(score.Score, ScoreManager.BestScore, record, lives.Lives);
        }

        public void TogglePause()
        {
            if (State == GameState.Playing)
            {
                Time.timeScale = 0f;
                SetState(GameState.Paused);
                AudioManager.SetMusicDucked(true);
                ui.ShowPause(true);
            }
            else if (State == GameState.Paused)
            {
                Time.timeScale = 1f;
                SetState(GameState.Playing);
                AudioManager.SetMusicDucked(false);
                ui.ShowPause(false);
            }
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            ScreenFader.LoadScene(SceneNames.Game);
        }

        public void GoToMainMenu()
        {
            Time.timeScale = 1f;
            ScreenFader.LoadScene(SceneNames.MainMenu);
        }

        void SetState(GameState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
