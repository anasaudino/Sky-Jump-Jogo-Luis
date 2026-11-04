using SkyJump.Player;
using UnityEngine;

namespace SkyJump.Core
{
    /// <summary>Linha de chegada no topo do Paraíso: tocar nela vence a partida.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class GoalZone : MonoBehaviour
    {
        [SerializeField] GameManager gameManager;
        [SerializeField] Transform glow;
        [SerializeField] float pulseSpeed = 3f;

        Vector3 glowScale;

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        void Awake()
        {
            if (glow != null) glowScale = glow.localScale;
        }

        void Update()
        {
            if (glow == null) return;
            float s = 1f + 0.08f * Mathf.Sin(Time.time * pulseSpeed);
            glow.localScale = new Vector3(glowScale.x * s, glowScale.y, 1f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null && gameManager != null)
                gameManager.Victory();
        }
    }
}
