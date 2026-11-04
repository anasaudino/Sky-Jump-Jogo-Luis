using System.Collections.Generic;
using SkyJump.CameraSystem;
using SkyJump.Player;
using SkyJump.World;
using UnityEngine;

namespace SkyJump.Platforms
{
    /// <summary>
    /// Gera plataformas à frente da câmera e recicla as que ficaram para trás (object pooling).
    /// O posicionamento respeita a física do personagem: a distância vertical nunca passa da
    /// altura máxima do pulo e a horizontal fica dentro do alcance real naquele salto.
    /// </summary>
    public class PlatformSpawner : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] Platform platformPrefab;
        [SerializeField] LevelManager level;
        [SerializeField] PlayerController player;
        [SerializeField] CameraController cameraController;
        [SerializeField] GameObject goalObject;

        [Header("Geração")]
        [Tooltip("Quantas unidades acima do topo da câmera manter geradas.")]
        [SerializeField] float spawnAhead = 8f;
        [Tooltip("Plataformas abaixo da câmera por mais que isso são recicladas.")]
        [SerializeField] float cullMargin = 3f;
        [Tooltip("Fração da altura máxima do pulo usada como espaçamento máximo.")]
        [Range(0.5f, 0.95f)] [SerializeField] float maxGapFraction = 0.82f;
        [Tooltip("Fração do alcance horizontal teórico considerada segura.")]
        [Range(0.3f, 0.9f)] [SerializeField] float reachSafety = 0.6f;
        [SerializeField] float rescueWidth = 3.2f;
        [SerializeField] float goalWidthFraction = 0.8f;

        readonly Queue<Platform> pool = new Queue<Platform>();
        readonly List<Platform> active = new List<Platform>();
        readonly List<Platform> scratch = new List<Platform>();

        float lastY;
        float lastX;
        float lastRange;
        bool lastWasCrumbling;
        bool goalSpawned;
        bool running;

        public IReadOnlyList<Platform> ActivePlatforms => active;

        public void Begin()
        {
            for (int i = active.Count - 1; i >= 0; i--) active[i].Release();
            goalSpawned = false;
            if (goalObject != null) goalObject.SetActive(false);

            float baseY = level.BaseHeight;
            float floorWidth = player.PlayHalfWidth * 2f * 0.95f;
            Spawn(level.GetWorld(0).RandomSkin(), new Vector2(0f, baseY), floorWidth, PlatformType.Static);
            lastY = baseY;
            lastX = 0f;
            lastRange = 0f;
            lastWasCrumbling = false;
            running = true;
            Fill();
        }

        public void Stop() => running = false;

        void Update()
        {
            if (!running) return;
            Fill();
            Cull();
        }

        void Fill()
        {
            float limit = cameraController.TopY + spawnAhead;
            int safety = 0;
            while (!goalSpawned && lastY < limit && safety++ < 64) SpawnNext();
        }

        void Cull()
        {
            float minY = cameraController.BottomY - cullMargin;
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i].SurfaceY < minY) active[i].Release();
        }

        void SpawnNext()
        {
            float goal = level.GoalHeight;
            float maxGap = player.MaxJumpHeight * maxGapFraction;

            WorldData w = level.GetWorld(level.GetWorldIndexAt(lastY + 0.01f));
            Vector2 gapRange = w.GapAt(level.ProgressInWorld(lastY));
            float gap = Mathf.Min(Random.Range(gapRange.x, gapRange.y), maxGap);
            float y = lastY + gap;

            // Chegada: a última plataforma precisa estar ao alcance da plataforma de chegada.
            if (goal - lastY <= maxGap && goal - y < gapRange.x)
            {
                SpawnGoal(goal);
                return;
            }
            if (y > goal - gapRange.x * 0.5f) y = Mathf.Min(goal - gapRange.x * 0.5f, lastY + maxGap);
            gap = y - lastY;

            int wi = level.GetWorldIndexAt(y);
            w = level.GetWorld(wi);
            float t = level.ProgressInWorld(y);
            float width = w.platformWidth;
            float half = player.PlayHalfWidth - width * 0.5f;

            // Alcance horizontal real descontando o movimento da plataforma anterior.
            float reach = Mathf.Max(0.8f, player.HorizontalReach(gap) * reachSafety - lastRange);

            PlatformType type = PlatformType.Static;
            float speed = 0f, range = 0f;
            if (!lastWasCrumbling && Random.value < w.CrumbleChanceAt(t)) type = PlatformType.Crumbling;
            else if (Random.value < w.MovingChanceAt(t))
            {
                type = PlatformType.Moving;
                range = Random.Range(0.8f, Mathf.Max(0.8f, w.maxMoveRange));
                range = Mathf.Min(range, half * 0.9f);
                speed = Random.Range(w.moveSpeed.x, w.moveSpeed.y);
            }

            float limitX = half - range;
            float x = ChooseX(lastX, reach, limitX);

            Spawn(w.RandomSkin(), new Vector2(x, y), width, type, speed, range);

            // Plataforma extra (opcional) nos mundos iniciais deixa o caminho menos linear.
            float extraChance = 0.35f * (1f - wi / 3f);
            if (type == PlatformType.Static && Random.value < extraChance)
            {
                // Fica ao alcance de um pulo "no mesmo nível" até a plataforma principal: nunca vira beco sem saída.
                float maxOffset = Mathf.Min(width + 2.5f, player.HorizontalReach(0f) * reachSafety);
                float side = x > 0 ? -1f : 1f;
                float ex = Mathf.Clamp(x + side * Random.Range(width + 0.8f, Mathf.Max(width + 0.9f, maxOffset)), -half, half);
                if (Mathf.Abs(ex - x) > width + 0.5f && Mathf.Abs(ex - x) <= maxOffset)
                    Spawn(w.RandomSkin(), new Vector2(ex, y + Random.Range(-0.3f, 0.3f)), width, PlatformType.Static);
            }

            lastY = y;
            lastX = x;
            lastRange = range;
            lastWasCrumbling = type == PlatformType.Crumbling;
        }

        static float ChooseX(float fromX, float reach, float limitX)
        {
            limitX = Mathf.Max(0f, limitX);
            float minX = Mathf.Max(-limitX, fromX - reach);
            float maxX = Mathf.Min(limitX, fromX + reach);
            if (minX > maxX) return Mathf.Clamp(fromX, -limitX, limitX);
            // Evita plataformas exatamente empilhadas: prefere deslocamentos de ao menos 0.6.
            for (int i = 0; i < 6; i++)
            {
                float x = Random.Range(minX, maxX);
                if (Mathf.Abs(x - fromX) > 0.6f) return x;
            }
            return Random.Range(minX, maxX);
        }

        void SpawnGoal(float goalY)
        {
            float width = player.PlayHalfWidth * 2f * goalWidthFraction;
            var last = level.GetWorld(level.WorldCount - 1);
            Spawn(last.platformSkins[0], new Vector2(0f, goalY), width, PlatformType.Goal);
            if (goalObject != null)
            {
                goalObject.transform.position = new Vector3(0f, goalY, 0f);
                goalObject.SetActive(true);
            }
            lastY = goalY;
            goalSpawned = true;
        }

        Platform Spawn(PlatformSkin skin, Vector2 pos, float width, PlatformType type, float speed = 0f, float range = 0f)
        {
            Platform p = pool.Count > 0 ? pool.Dequeue() : Instantiate(platformPrefab, transform);
            p.Setup(this, skin, pos, width, type, speed, range);
            active.Add(p);
            return p;
        }

        public void ReturnToPool(Platform p)
        {
            if (!active.Remove(p)) return;
            p.gameObject.SetActive(false);
            pool.Enqueue(p);
        }

        /// <summary>
        /// Cria uma plataforma de resgate visível na parte de baixo da tela, sempre
        /// ao alcance da plataforma seguinte. Retorna o ponto onde o jogador deve reaparecer.
        /// </summary>
        public Vector2 CreateRescuePlatform()
        {
            float bottom = cameraController.BottomY;
            Platform target = null;
            foreach (var p in active)
            {
                if (!p.IsAlive || p.Type == PlatformType.Crumbling || p.Type == PlatformType.Rescue) continue;
                if (p.SurfaceY <= bottom + 3f) continue;
                if (target == null || p.SurfaceY < target.SurfaceY) target = p;
            }

            float y = target != null ? Mathf.Max(bottom + 1.5f, target.SurfaceY - 2.2f) : bottom + 1.5f;
            float half = player.PlayHalfWidth - rescueWidth * 0.5f;
            float x = Mathf.Clamp(target != null ? target.BaseX : 0f, -half, half);

            // Remove plataformas que ficariam sobrepostas ao resgate.
            scratch.Clear();
            foreach (var p in active)
                if (p != target && Mathf.Abs(p.SurfaceY - y) < 1.1f &&
                    Mathf.Abs(p.transform.position.x - x) < (p.Width + rescueWidth) * 0.5f + p.MoveRange)
                    scratch.Add(p);
            foreach (var p in scratch) p.Release();

            var world = level.GetWorld(level.GetWorldIndexAt(y));
            Spawn(world.RandomSkin(), new Vector2(x, y), rescueWidth, PlatformType.Rescue);
            return new Vector2(x, y + 1.4f);
        }
    }
}
