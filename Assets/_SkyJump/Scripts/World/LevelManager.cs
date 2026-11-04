using System;
using UnityEngine;

namespace SkyJump.World
{
    /// <summary>
    /// Controla a sequência de mundos (Subterrâneo → Terra → Céu → Paraíso) com base na altura.
    /// Cada mundo ocupa uma faixa de altura; o topo do último mundo é a linha de chegada.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] WorldData[] worlds;
        [Tooltip("Altura em que a partida começa (chão inicial).")]
        [SerializeField] float baseHeight = 0f;

        float[] worldStarts;
        public int CurrentWorldIndex { get; private set; }
        public int WorldCount => worlds.Length;
        public WorldData CurrentWorld => worlds[CurrentWorldIndex];
        public float GoalHeight { get; private set; }
        public float BaseHeight => baseHeight;

        /// <summary>(novo índice, índice anterior)</summary>
        public event Action<int, int> WorldChanged;

        void Awake()
        {
            if (worlds == null || worlds.Length == 0)
            {
                Debug.LogError("[LevelManager] Nenhum mundo configurado.", this);
                enabled = false;
                return;
            }
            worldStarts = new float[worlds.Length];
            float h = baseHeight;
            for (int i = 0; i < worlds.Length; i++)
            {
                worldStarts[i] = h;
                h += worlds[i].length;
            }
            GoalHeight = h;
            CurrentWorldIndex = 0;
        }

        public WorldData GetWorld(int index) => worlds[Mathf.Clamp(index, 0, worlds.Length - 1)];

        public int GetWorldIndexAt(float height)
        {
            for (int i = worlds.Length - 1; i >= 0; i--)
                if (height >= worldStarts[i]) return i;
            return 0;
        }

        public float WorldStart(int index) => worldStarts[Mathf.Clamp(index, 0, worlds.Length - 1)];

        /// <summary>Progresso (0-1) dentro do mundo naquela altura.</summary>
        public float ProgressInWorld(float height)
        {
            int i = GetWorldIndexAt(height);
            return Mathf.Clamp01((height - worldStarts[i]) / worlds[i].length);
        }

        /// <summary>Progresso total (0-1) da partida.</summary>
        public float TotalProgress(float height) =>
            Mathf.Clamp01((height - baseHeight) / (GoalHeight - baseHeight));

        /// <summary>Informa a maior altura alcançada; dispara a troca de mundo quando necessário.</summary>
        public void ReportHeight(float maxHeight)
        {
            int index = GetWorldIndexAt(maxHeight);
            if (index > CurrentWorldIndex)
            {
                int previous = CurrentWorldIndex;
                CurrentWorldIndex = index;
                WorldChanged?.Invoke(index, previous);
            }
        }
    }
}
