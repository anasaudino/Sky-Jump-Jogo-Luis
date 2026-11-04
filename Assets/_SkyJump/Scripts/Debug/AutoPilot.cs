using SkyJump.Core;
using SkyJump.Platforms;
using SkyJump.Player;
using UnityEngine;

namespace SkyJump.DebugTools
{
    /// <summary>
    /// Ferramenta de TESTE (não faz parte das cenas): joga sozinha, mirando sempre a próxima
    /// plataforma alcançável. Serve para validar que a partida pode ser concluída do início ao fim
    /// e para medir quedas, mundos atravessados e tempo de jogo.
    /// </summary>
    public class AutoPilot : MonoBehaviour
    {
        public int Bounces;
        public float StartTime;
        public float LastBounceY;

        PlayerController player;
        PlatformSpawner spawner;
        Platform target;

        void Start()
        {
            player = FindAnyObjectByType<PlayerController>();
            spawner = FindAnyObjectByType<PlatformSpawner>();
            player.Bounced += OnBounced;
            LastBounceY = player.transform.position.y - 1f;
            StartTime = Time.time;
        }

        void OnDestroy()
        {
            if (player != null)
            {
                player.Bounced -= OnBounced;
                player.ScriptedInput = null;
            }
        }

        void OnBounced(Platform p)
        {
            Bounces++;
            LastBounceY = p != null ? p.SurfaceY : player.transform.position.y;
            target = null;
        }

        void Update()
        {
            if (player == null || spawner == null) return;
            // Após um respawn o jogador cai bem abaixo do último pulo.
            if (!player.ControlEnabled) { LastBounceY = player.transform.position.y - 3f; target = null; }

            if (target == null || !target.IsAlive) target = ChooseTarget();
            float input = 0f;
            if (target != null)
            {
                float dx = target.transform.position.x - player.transform.position.x;
                input = Mathf.Abs(dx) < 0.12f ? 0f : Mathf.Clamp(dx * 2f, -1f, 1f);
            }
            player.ScriptedInput = input;
        }

        Platform ChooseTarget()
        {
            // Primeiro tenta subir; se não houver alvo acima, aceita uma plataforma no mesmo nível.
            return ChooseTarget(0.4f) ?? ChooseTarget(-0.8f);
        }

        Platform ChooseTarget(float minDy)
        {
            Platform best = null;
            float bestScore = float.MaxValue;
            float maxDy = player.MaxJumpHeight * 0.92f;
            foreach (var p in spawner.ActivePlatforms)
            {
                if (!p.IsAlive) continue;
                float dy = p.SurfaceY - LastBounceY;
                if (dy < minDy || dy > maxDy) continue;
                float dx = Mathf.Abs(p.transform.position.x - player.transform.position.x);
                if (dx < 0.5f && Mathf.Abs(dy) < 0.4f) continue; // a própria plataforma atual
                if (dx > player.HorizontalReach(Mathf.Max(0f, dy)) * 0.95f) continue;
                float score = dy * 2f + dx * 0.3f + (p.Type == PlatformType.Crumbling ? 1.5f : 0f) - (p.Type == PlatformType.Goal ? 100f : 0f);
                if (score < bestScore) { bestScore = score; best = p; }
            }
            return best;
        }
    }
}
