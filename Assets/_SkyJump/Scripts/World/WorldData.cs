using System;
using UnityEngine;

namespace SkyJump.World
{
    /// <summary>Visual de uma plataforma: sprite e a área realmente visível (sem transparência).</summary>
    [Serializable]
    public class PlatformSkin
    {
        public Sprite sprite;
        [Tooltip("Retângulo visível do sprite, normalizado (0-1), calculado automaticamente.")]
        public Rect visibleRect = new Rect(0, 0, 1, 1);
        [Tooltip("Quanto da altura visível fica acima da superfície de colisão (0 = topo exato).")]
        [Range(0f, 0.6f)] public float surfaceInset = 0.15f;
    }

    /// <summary>
    /// Configuração de um mundo (Subterrâneo, Terra, Céu, Paraíso).
    /// Valores "Start" valem no início do mundo e "End" no final; são interpolados conforme a altura.
    /// </summary>
    [CreateAssetMenu(menuName = "Sky Jump/World Data", fileName = "World")]
    public class WorldData : ScriptableObject
    {
        [Header("Identidade")]
        public string displayName = "Mundo";
        public Color themeColor = Color.white;

        [Header("Cenário")]
        public Sprite backgroundVertical;
        public Sprite backgroundHorizontal;

        [Header("Altura")]
        [Tooltip("Altura (em unidades) necessária para atravessar este mundo.")]
        public float length = 60f;
        [Tooltip("Bônus de pontos ao completar este mundo.")]
        public int completionBonus = 250;

        [Header("Plataformas")]
        public PlatformSkin[] platformSkins;
        public float platformWidth = 2.6f;
        public Vector2 gapStart = new Vector2(1.3f, 1.8f);
        public Vector2 gapEnd = new Vector2(1.5f, 2.1f);

        [Header("Plataformas móveis")]
        [Range(0, 1)] public float movingChanceStart;
        [Range(0, 1)] public float movingChanceEnd;
        public Vector2 moveSpeed = new Vector2(1f, 1.6f);
        public float maxMoveRange = 2f;

        [Header("Plataformas que quebram (avançadas)")]
        [Range(0, 1)] public float crumbleChanceStart;
        [Range(0, 1)] public float crumbleChanceEnd;

        public Vector2 GapAt(float t) => Vector2.Lerp(gapStart, gapEnd, t);
        public float MovingChanceAt(float t) => Mathf.Lerp(movingChanceStart, movingChanceEnd, t);
        public float CrumbleChanceAt(float t) => Mathf.Lerp(crumbleChanceStart, crumbleChanceEnd, t);

        public PlatformSkin RandomSkin()
        {
            if (platformSkins == null || platformSkins.Length == 0) return null;
            return platformSkins[UnityEngine.Random.Range(0, platformSkins.Length)];
        }

        public Sprite BackgroundFor(bool portrait)
        {
            var s = portrait ? backgroundVertical : backgroundHorizontal;
            return s != null ? s : (backgroundVertical != null ? backgroundVertical : backgroundHorizontal);
        }
    }
}
