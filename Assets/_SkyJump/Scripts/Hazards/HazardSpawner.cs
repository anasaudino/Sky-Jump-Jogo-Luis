using System.Collections.Generic;
using SkyJump.Audio;
using SkyJump.CameraSystem;
using SkyJump.Core;
using SkyJump.FX;
using SkyJump.Player;
using SkyJump.World;
using UnityEngine;

namespace SkyJump.Hazards
{
    /// <summary>
    /// Gera os perigos da subida conforme o mundo atual (frequência, tipo e velocidade vêm do WorldData).
    /// Encostar num perigo empurra o jogador para baixo (pode fazê-lo cair); cair em cima de um voador
    /// derrota o inimigo, dá um quique extra e pontos. Usa pool de objetos.
    /// </summary>
    public class HazardSpawner : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] LevelManager level;
        [SerializeField] PlayerController player;
        [SerializeField] CameraController cameraController;
        [SerializeField] ScoreManager score;

        [Header("Regras")]
        [Tooltip("Altura inicial sem perigos (para o jogador se acostumar).")]
        [SerializeField] float startGrace = 10f;
        [Tooltip("Sem perigos novos tão perto da chegada.")]
        [SerializeField] float goalGrace = 8f;
        [Tooltip("Duração do aviso \"!\" antes do perigo aparecer.")]
        [SerializeField] float warningTime = 0.9f;
        [SerializeField] float flyerSpeed = 3.6f;
        [SerializeField] float fallSpeed = 8f;
        [SerializeField] int stompBonus = 50;

        readonly List<Hazard> active = new List<Hazard>();
        readonly Stack<Hazard> pool = new Stack<Hazard>();
        float timer;
        bool running;

        public IReadOnlyList<Hazard> Active => active;
        public int StompBonus => stompBonus;

        /// <summary>Preenche as referências quando o componente é criado por código.</summary>
        public void Init(LevelManager level, PlayerController player, CameraController camera, ScoreManager score)
        {
            if (this.level == null) this.level = level;
            if (this.player == null) this.player = player;
            if (cameraController == null) cameraController = camera;
            if (this.score == null) this.score = score;
        }

        public void Begin()
        {
            ClearAll();
            running = true;
        }

        public void Stop()
        {
            running = false;
            ClearAll();
        }

        /// <summary>Remove todos os perigos e reinicia a espera (usado ao perder vida).</summary>
        public void ClearAll()
        {
            for (int i = active.Count - 1; i >= 0; i--) Release(active[i]);
            timer = 2.5f;
        }

        void Update()
        {
            if (!running || player == null || !player.ControlEnabled) return;

            float y = player.transform.position.y;
            if (y < level.BaseHeight + startGrace || y > level.GoalHeight - goalGrace) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            var world = level.GetWorld(level.GetWorldIndexAt(y));
            float interval = world.HazardIntervalAt(level.ProgressInWorld(y));
            if (interval <= 0f) { timer = 1f; return; }
            timer = interval * Random.Range(0.8f, 1.2f);
            if (active.Count >= world.maxHazards) return;

            if (Random.value < world.flyerShare) SpawnFlyer(world);
            else SpawnFalling(world);
        }

        void SpawnFlyer(WorldData world)
        {
            float half = player.PlayHalfWidth;
            float side = Random.value < 0.5f ? -1f : 1f;
            // Aparece acima do jogador, dentro da tela, onde ele está prestes a chegar.
            float py = player.transform.position.y;
            float y = Mathf.Min(py + Random.Range(2.5f, 6f), cameraController.TopY - 1.5f);
            y = Mathf.Max(y, py + 1.5f);
            float speed = flyerSpeed * world.hazardSpeed * Random.Range(0.85f, 1.15f);
            Get().LaunchFlyer(cameraController, world, side, y, speed, half, warningTime);
        }

        void SpawnFalling(WorldData world)
        {
            float half = player.PlayHalfWidth - 0.6f;
            float x = Mathf.Clamp(player.transform.position.x + Random.Range(-3f, 3f), -half, half);
            Get().LaunchFalling(cameraController, world, x, fallSpeed * world.hazardSpeed, warningTime);
            AudioManager.Play(Sfx.Warning, 0.5f);
        }

        Hazard Get()
        {
            var h = pool.Count > 0 ? pool.Pop() : Hazard.Create(this, transform);
            active.Add(h);
            return h;
        }

        public void Release(Hazard h)
        {
            if (!active.Remove(h)) return;
            h.gameObject.SetActive(false);
            pool.Push(h);
        }

        /// <summary>Chamado pelo perigo quando encosta no jogador.</summary>
        public void OnPlayerContact(Hazard h, PlayerController p)
        {
            Vector2 hp = h.transform.position;
            bool fromAbove = p.Velocity.y < 0f && p.transform.position.y > hp.y + 0.15f;

            if (h.IsFlyer && fromAbove && p.ControlEnabled)
            {
                p.StompBounce();
                h.Defeat();
                if (score != null) score.AddBonus(stompBonus);
                AudioManager.Play(Sfx.Stomp);
                PixelParticles.Burst(hp, h.ParticleColor, 14, 4f);
                return;
            }

            if (!p.TryHit(hp)) return;
            AudioManager.Play(Sfx.Hit);
            PixelParticles.Burst(Vector2.Lerp(hp, p.transform.position, 0.5f), h.ParticleColor, 16, 5f);
            if (h.IsFlyer) h.MarkSpent();
            else Release(h);
        }
    }
}
