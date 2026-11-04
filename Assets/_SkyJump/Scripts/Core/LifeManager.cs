using System;
using UnityEngine;

namespace SkyJump.Core
{
    /// <summary>
    /// Vidas da partida inteira. São compartilhadas entre os mundos e
    /// só voltam ao máximo quando uma nova partida começa.
    /// </summary>
    public class LifeManager : MonoBehaviour
    {
        [SerializeField] int maxLives = 3;

        public int MaxLives => maxLives;
        public int Lives { get; private set; }
        public bool HasLives => Lives > 0;

        public event Action<int> LivesChanged;

        public void ResetLives()
        {
            Lives = maxLives;
            LivesChanged?.Invoke(Lives);
        }

        /// <summary>Remove uma vida e retorna quantas restam.</summary>
        public int LoseLife()
        {
            Lives = Mathf.Max(0, Lives - 1);
            LivesChanged?.Invoke(Lives);
            return Lives;
        }
    }
}
