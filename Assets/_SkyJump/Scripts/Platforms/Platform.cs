using System.Collections;
using SkyJump.FX;
using SkyJump.World;
using UnityEngine;

namespace SkyJump.Platforms
{
    public enum PlatformType
    {
        Static,
        Moving,
        Crumbling,
        Rescue,
        Goal
    }

    /// <summary>
    /// Plataforma com colisão unidirecional (PlatformEffector2D): o jogador atravessa por baixo
    /// e quica por cima. Pode ser estática, móvel (vai e volta na horizontal), quebradiça
    /// (some depois de um pulo) ou de resgate (aparece após perder uma vida).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class Platform : MonoBehaviour
    {
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] float colliderThickness = 0.3f;
        [SerializeField] Color crumblingTint = new Color(1f, 0.72f, 0.66f, 1f);
        [SerializeField] Color rescueTint = new Color(1f, 1f, 0.75f, 1f);

        Rigidbody2D body;
        BoxCollider2D box;
        Transform visual;
        Vector3 visualBasePos;
        PlatformSpawner owner;

        float baseX;
        float moveRange;
        float moveSpeed;
        float phase;
        int bouncesLeft;
        float bobTimer;
        bool breaking;

        public PlatformType Type { get; private set; }
        public float Width { get; private set; }
        public float SurfaceY => transform.position.y;
        public float BaseX => baseX;
        public float MoveRange => moveRange;
        public bool IsAlive => gameObject.activeSelf && !breaking;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            visual = spriteRenderer != null ? spriteRenderer.transform : null;
        }

        public void Setup(PlatformSpawner spawner, PlatformSkin skin, Vector2 position, float width,
                          PlatformType type, float speed = 0f, float range = 0f)
        {
            owner = spawner;
            Type = type;
            Width = width;
            baseX = position.x;
            moveSpeed = speed;
            moveRange = type == PlatformType.Moving ? range : 0f;
            phase = Random.Range(0f, Mathf.PI * 2f);
            bouncesLeft = type == PlatformType.Rescue ? 3 : int.MaxValue;
            breaking = false;
            bobTimer = 0f;

            transform.position = position;
            body.position = position;
            box.enabled = true;
            box.size = new Vector2(width * 0.92f, colliderThickness);
            box.offset = new Vector2(0f, -colliderThickness * 0.5f);

            ApplySkin(skin, width);

            spriteRenderer.color = type switch
            {
                PlatformType.Crumbling => crumblingTint,
                PlatformType.Rescue => rescueTint,
                _ => Color.white
            };
            gameObject.SetActive(true);
            if (type == PlatformType.Moving) UpdateMovement(0f);
        }

        /// <summary>Escala o sprite para a largura desejada e alinha a superfície visível ao colisor.</summary>
        void ApplySkin(PlatformSkin skin, float width)
        {
            if (skin == null || skin.sprite == null || visual == null) return;
            spriteRenderer.sprite = skin.sprite;
            Vector2 spriteSize = skin.sprite.bounds.size;
            Rect vr = skin.visibleRect;
            float visibleW = spriteSize.x * vr.width;
            float scale = width / Mathf.Max(0.01f, visibleW);
            visual.localScale = new Vector3(scale, scale, 1f);

            // Coordenadas locais do sprite com pivot central.
            float visibleTop = (vr.yMax - 0.5f) * spriteSize.y * scale;
            float visibleH = vr.height * spriteSize.y * scale;
            float surface = visibleTop - skin.surfaceInset * visibleH;
            float centerX = (vr.center.x - 0.5f) * spriteSize.x * scale;
            visualBasePos = new Vector3(-centerX, -surface, 0f);
            visual.localPosition = visualBasePos;
        }

        void FixedUpdate()
        {
            if (Type == PlatformType.Moving && !breaking) UpdateMovement(Time.fixedDeltaTime);
        }

        void UpdateMovement(float dt)
        {
            if (moveRange <= 0.01f) return;
            phase += dt * moveSpeed / moveRange;
            float x = baseX + Mathf.Sin(phase) * moveRange;
            body.MovePosition(new Vector2(x, body.position.y));
        }

        void Update()
        {
            if (visual == null || breaking) return;
            // Pequeno "afundar" quando o jogador quica.
            if (bobTimer > 0f)
            {
                bobTimer -= Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(1f - bobTimer / 0.18f) * Mathf.PI);
                visual.localPosition = visualBasePos + Vector3.down * (0.12f * k);
            }
            else if (Type == PlatformType.Rescue)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 8f);
                spriteRenderer.color = Color.Lerp(Color.white, rescueTint, pulse);
            }
        }

        public void OnBounced()
        {
            bobTimer = 0.18f;
            PixelParticles.Burst(new Vector2(transform.position.x, SurfaceY), PuffColor(), 6, 2.2f);

            if (Type == PlatformType.Crumbling) Break();
            else if (Type == PlatformType.Rescue && --bouncesLeft <= 0) Break();
        }

        Color PuffColor() => Type == PlatformType.Crumbling ? new Color(0.9f, 0.6f, 0.5f) : new Color(1f, 1f, 1f, 0.9f);

        public void Break()
        {
            if (breaking) return;
            StartCoroutine(BreakRoutine());
        }

        IEnumerator BreakRoutine()
        {
            breaking = true;
            box.enabled = false;
            PixelParticles.Burst(transform.position, spriteRenderer.color * 0.8f, 14, 3f);
            float t = 0f;
            Color c = spriteRenderer.color;
            Vector3 start = visual.localPosition;
            while (t < 0.45f)
            {
                t += Time.deltaTime;
                float k = t / 0.45f;
                visual.localPosition = start + new Vector3(Random.Range(-0.05f, 0.05f), -k * k * 1.5f, 0f);
                spriteRenderer.color = new Color(c.r, c.g, c.b, 1f - k);
                yield return null;
            }
            Release();
        }

        public void Release()
        {
            StopAllCoroutines();
            breaking = false;
            if (owner != null) owner.ReturnToPool(this);
            else gameObject.SetActive(false);
        }
    }
}
