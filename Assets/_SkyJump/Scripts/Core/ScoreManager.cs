using System;
using UnityEngine;

namespace SkyJump.Core
{
    /// <summary>
    /// Pontuação = (altura máxima alcançada × pontos por unidade) + bônus.
    /// Usar a altura MÁXIMA evita pontuar de novo quando o jogador cai e sobe pelo mesmo trecho.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        const string BestKey = "SkyJump.BestScore";

        [SerializeField] int pointsPerUnit = 10;

        float baseHeight;
        float maxHeight;
        int bonus;

        public int Score => Mathf.FloorToInt(Mathf.Max(0f, maxHeight - baseHeight) * pointsPerUnit) + bonus;
        public float MaxHeight => maxHeight;
        public static int BestScore => PlayerPrefs.GetInt(BestKey, 0);

        public event Action<int> ScoreChanged;

        public void ResetScore(float startHeight)
        {
            baseHeight = startHeight;
            maxHeight = startHeight;
            bonus = 0;
            ScoreChanged?.Invoke(Score);
        }

        public void ReportHeight(float height)
        {
            if (height <= maxHeight) return;
            int before = Score;
            maxHeight = height;
            if (Score != before) ScoreChanged?.Invoke(Score);
        }

        public void AddBonus(int amount)
        {
            if (amount <= 0) return;
            bonus += amount;
            ScoreChanged?.Invoke(Score);
        }

        /// <summary>Salva o recorde; retorna true se for um novo recorde.</summary>
        public bool SaveBest()
        {
            if (Score <= BestScore) return false;
            PlayerPrefs.SetInt(BestKey, Score);
            PlayerPrefs.Save();
            return true;
        }
    }
}
