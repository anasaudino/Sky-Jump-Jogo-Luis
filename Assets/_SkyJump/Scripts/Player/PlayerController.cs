using System;
using System.Collections;
using SkyJump.Platforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SkyJump.Player
{
    /// <summary>
    /// Bola controlada pelo jogador. Move-se na horizontal e pula automaticamente
    /// ao tocar o topo de uma plataforma (estilo Sky Jump). Usa Rigidbody2D dinâmico.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Pulo e gravidade")]
        [SerializeField] float jumpVelocity = 13f;
        [SerializeField] float gravity = 25f;
        [SerializeField] float maxFallSpeed = 20f;

        [Header("Movimento horizontal")]
        [SerializeField] float maxSpeed = 7f;
        [SerializeField] float acceleration = 45f;
        [SerializeField] float deceleration = 30f;
        [Tooltip("Metade da largura da área jogável (coluna).")]
        [SerializeField] float playHalfWidth = 5f;

        [Header("Visual")]
        [SerializeField] Transform visual;
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] float squashAmount = 0.22f;
        [SerializeField] float squashRecover = 10f;

        [Header("Perigos")]
        [Tooltip("Velocidade do empurrão ao ser atingido (x = lateral, y = para baixo).")]
        [SerializeField] Vector2 knockback = new Vector2(6.5f, 6f);
        [Tooltip("Tempo sem controle logo após ser atingido.")]
        [SerializeField] float stunTime = 0.3f;
        [Tooltip("Tempo piscando, imune a novos perigos.")]
        [SerializeField] float invulnerableTime = 1.5f;

        [Header("Toque")]
        [Tooltip("Fração superior da tela ignorada pelo toque (área do HUD/pausa).")]
        [SerializeField] float touchIgnoreTop = 0.14f;

        Rigidbody2D body;
        CircleCollider2D circle;
        float input;
        bool controlEnabled = true;
        float squash;
        float lastBounceTime = -1f;
        float stunUntil = -1f;
        float invulnerableUntil = -1f;
        Vector3 visualBaseScale = Vector3.one;

        public float JumpVelocity => jumpVelocity;
        public float Gravity => gravity;
        public float MaxSpeed => maxSpeed;
        public float PlayHalfWidth => playHalfWidth;
        public float Radius => circle != null ? circle.radius * transform.lossyScale.x : 0.45f;
        public Vector2 Velocity => body.linearVelocity;
        public bool ControlEnabled => controlEnabled;
        public bool IsInvulnerable => !controlEnabled || Time.time < invulnerableUntil;

        /// <summary>Disparado sempre que o jogador quica numa plataforma.</summary>
        public event Action<Platform> Bounced;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            circle = GetComponent<CircleCollider2D>();
            body.gravityScale = gravity / Mathf.Abs(Physics2D.gravity.y);
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (visual != null) visualBaseScale = visual.localScale;
        }

        void Update()
        {
            input = controlEnabled && Time.time >= stunUntil ? ReadInput() : 0f;
            UpdateVisual();
            // Pisca enquanto está imune depois de ser atingido (o respawn controla o próprio piscar).
            if (controlEnabled && spriteRenderer != null)
                spriteRenderer.enabled = Time.time >= invulnerableUntil || Mathf.Repeat(Time.time, 0.14f) < 0.09f;
        }

        /// <summary>Entrada simulada (usada pelo piloto automático de testes). Null = usa os dispositivos.</summary>
        public float? ScriptedInput { get; set; }

        float ReadInput()
        {
            if (ScriptedInput.HasValue) return Mathf.Clamp(ScriptedInput.Value, -1f, 1f);
            float value = 0f;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) value -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) value += 1f;
            }

            var pad = Gamepad.current;
            if (pad != null && Mathf.Abs(value) < 0.01f)
            {
                float stick = pad.leftStick.ReadValue().x;
                if (Mathf.Abs(stick) > 0.2f) value = stick;
                if (pad.dpad.left.isPressed) value = -1f;
                if (pad.dpad.right.isPressed) value = 1f;
            }

            // Toque: segurar o lado esquerdo/direito da tela.
            var ts = Touchscreen.current;
            if (ts != null && Mathf.Abs(value) < 0.01f)
            {
                float touchValue = 0f;
                foreach (var touch in ts.touches)
                {
                    if (!touch.press.isPressed) continue;
                    Vector2 p = touch.position.ReadValue();
                    if (p.y > Screen.height * (1f - touchIgnoreTop)) continue;
                    touchValue = p.x < Screen.width * 0.5f ? -1f : 1f; // o toque mais recente prevalece
                }
                value = touchValue;
            }

            return Mathf.Clamp(value, -1f, 1f);
        }

        void FixedUpdate()
        {
            if (!body.simulated) return;
            Vector2 v = body.linearVelocity;

            float target = input * maxSpeed;
            float rate = Mathf.Abs(target) > 0.01f ? acceleration : deceleration;
            v.x = Mathf.MoveTowards(v.x, target, rate * Time.fixedDeltaTime);
            v.y = Mathf.Max(v.y, -maxFallSpeed);

            // Mantém a bola dentro da coluna jogável.
            Vector2 pos = body.position;
            float limit = playHalfWidth - Radius;
            if (pos.x < -limit) { pos.x = -limit; if (v.x < 0) v.x = 0; body.position = pos; }
            else if (pos.x > limit) { pos.x = limit; if (v.x > 0) v.x = 0; body.position = pos; }

            body.linearVelocity = v;
        }

        void OnCollisionEnter2D(Collision2D collision) => TryBounce(collision);
        void OnCollisionStay2D(Collision2D collision) => TryBounce(collision);

        void TryBounce(Collision2D collision)
        {
            if (!controlEnabled) return;
            // Evita quicar duas vezes no mesmo contato.
            if (body.linearVelocity.y > 1f || Time.time - lastBounceTime < 0.05f) return;

            for (int i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y > 0.6f)
                {
                    var platform = collision.collider.GetComponentInParent<Platform>();
                    Bounce(platform);
                    return;
                }
            }
        }

        void Bounce(Platform platform)
        {
            lastBounceTime = Time.time;
            var v = body.linearVelocity;
            v.y = jumpVelocity;
            body.linearVelocity = v;
            squash = 1f;
            if (platform != null) platform.OnBounced();
            Bounced?.Invoke(platform);
        }

        /// <summary>
        /// Atingido por um perigo: perde o impulso (e uma vida, via GameManager), é empurrado para baixo e para o lado oposto
        /// e fica imune por um instante. Retorna false se já estava imune.
        /// </summary>
        public bool TryHit(Vector2 from)
        {
            if (IsInvulnerable || !body.simulated) return false;
            float dir = transform.position.x >= from.x ? 1f : -1f;
            body.linearVelocity = new Vector2(dir * knockback.x, Mathf.Min(body.linearVelocity.y, -knockback.y));
            stunUntil = Time.time + stunTime;
            invulnerableUntil = Time.time + invulnerableTime;
            squash = 1f;
            return true;
        }

        /// <summary>Quique extra ao pisar num inimigo.</summary>
        public void StompBounce() => Bounce(null);

        void UpdateVisual()
        {
            if (visual == null) return;
            squash = Mathf.MoveTowards(squash, 0f, squashRecover * Time.deltaTime);
            float s = squash * squashAmount;
            visual.localScale = new Vector3(visualBaseScale.x * (1f + s), visualBaseScale.y * (1f - s), visualBaseScale.z);
        }

        /// <summary>Habilita/desabilita controle e física (usado em fim de jogo e pausa).</summary>
        public void SetActiveControl(bool active)
        {
            controlEnabled = active;
            body.simulated = active;
            if (!active) body.linearVelocity = Vector2.zero;
            invulnerableUntil = -1f;
            if (spriteRenderer != null) spriteRenderer.enabled = true;
        }

        /// <summary>Reaparece no ponto seguro, piscando antes de voltar a cair.</summary>
        public void Respawn(Vector2 position, Action onFinished = null)
        {
            StopAllCoroutines();
            StartCoroutine(RespawnRoutine(position, onFinished));
        }

        IEnumerator RespawnRoutine(Vector2 position, Action onFinished)
        {
            controlEnabled = false;
            body.simulated = false;
            body.linearVelocity = Vector2.zero;
            transform.position = position;
            body.position = position;

            const float blinkTime = 0.9f;
            float t = 0f;
            while (t < blinkTime)
            {
                t += Time.deltaTime;
                if (spriteRenderer != null) spriteRenderer.enabled = Mathf.Repeat(t, 0.16f) < 0.1f;
                yield return null;
            }
            if (spriteRenderer != null) spriteRenderer.enabled = true;

            controlEnabled = true;
            body.simulated = true;
            body.linearVelocity = new Vector2(0f, -2f);
            stunUntil = -1f;
            invulnerableUntil = Time.time + 1f;
            onFinished?.Invoke();
        }

        /// <summary>Altura máxima de um pulo (v²/2g).</summary>
        public float MaxJumpHeight => jumpVelocity * jumpVelocity / (2f * gravity);

        /// <summary>
        /// Distância horizontal alcançável para subir <paramref name="dy"/> e aterrissar
        /// por cima (tempo até cruzar a altura na descida × velocidade máxima).
        /// </summary>
        public float HorizontalReach(float dy)
        {
            float disc = jumpVelocity * jumpVelocity - 2f * gravity * dy;
            if (disc < 0f) return 0f;
            float t = (jumpVelocity + Mathf.Sqrt(disc)) / gravity;
            return t * maxSpeed;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(new Vector3(-playHalfWidth, transform.position.y - 5), new Vector3(-playHalfWidth, transform.position.y + 5));
            Gizmos.DrawLine(new Vector3(playHalfWidth, transform.position.y - 5), new Vector3(playHalfWidth, transform.position.y + 5));
        }
#endif
    }
}
