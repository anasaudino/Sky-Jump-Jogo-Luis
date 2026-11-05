using SkyJump.CameraSystem;
using SkyJump.Player;
using SkyJump.World;
using UnityEngine;

namespace SkyJump.Hazards
{
    /// <summary>
    /// Um perigo da subida: um voador (pássaro/morcego) que atravessa a tela ondulando, ou um objeto
    /// que cai do céu (pedra, gelo, estrela). Antes de aparecer mostra um aviso "!" piscando, para que
    /// o jogador sempre tenha tempo de reagir. Usa collider trigger; quem decide o efeito é o HazardSpawner.
    /// </summary>
    public class Hazard : MonoBehaviour
    {
        enum Phase { Warning, Active, Defeated }

        const int SortingOrder = 15;
        const float FlapInterval = 0.14f;

        HazardSpawner owner;
        CameraController cam;
        SpriteRenderer body;
        SpriteRenderer sign;
        CircleCollider2D hitbox;

        Phase phase;
        Sprite[] frames;
        float warnLeft;
        float age;
        Vector2 velocity;
        float baseY;
        float waveAmp, waveFreq;
        float spin;
        float maxFall;
        float exitX;
        bool spent;

        public bool IsFlyer { get; private set; }
        /// <summary>Já atingiu o jogador (não causa um segundo empurrão).</summary>
        public bool Spent => spent;
        public bool IsDangerous => phase == Phase.Active && !spent;
        public Color ParticleColor { get; private set; }

        public static Hazard Create(HazardSpawner owner, Transform parent)
        {
            var go = new GameObject("Hazard");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<Hazard>();
            h.owner = owner;

            h.body = go.AddComponent<SpriteRenderer>();
            h.body.sortingOrder = SortingOrder;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
            h.hitbox = go.AddComponent<CircleCollider2D>();
            h.hitbox.isTrigger = true;

            var signGo = new GameObject("Warning");
            signGo.transform.SetParent(go.transform, false);
            h.sign = signGo.AddComponent<SpriteRenderer>();
            h.sign.sprite = HazardArt.WarningSign();
            h.sign.sortingOrder = SortingOrder + 15;
            return h;
        }

        /// <summary>Voador que entra pela lateral <paramref name="side"/> (-1 esquerda, 1 direita) na altura y.</summary>
        public void LaunchFlyer(CameraController camera, WorldData world, float side, float y, float speed, float columnHalf, float warning)
        {
            Prepare(camera, warning);
            IsFlyer = true;
            frames = HazardArt.Flyer(world.flyerStyle, world.flyerColor);
            ParticleColor = world.flyerColor;
            bool bat = world.flyerStyle == FlyerStyle.Bat;
            waveAmp = bat ? 0.9f : 0.4f;
            waveFreq = bat ? 4.5f : 2.6f;
            hitbox.radius = bat ? 0.4f : 0.45f;
            spin = 0f;

            float startX = side * (Mathf.Min(camera.HalfWidth, columnHalf + 3f) + 1.2f);
            exitX = -startX;
            baseY = y;
            velocity = new Vector2(-side * speed, 0f);
            transform.position = new Vector3(startX, y, 0f);
            body.sprite = frames[0];
            body.flipX = velocity.x < 0f; // o desenho olha para a direita
            sign.transform.position = new Vector3(side * (columnHalf - 0.5f), y, 0f);
        }

        /// <summary>Objeto que cai do topo da tela na coluna x.</summary>
        public void LaunchFalling(CameraController camera, WorldData world, float x, float fallSpeed, float warning)
        {
            Prepare(camera, warning);
            IsFlyer = false;
            frames = new[] { HazardArt.Falling(world.fallingStyle) };
            body.sprite = frames[0];
            body.flipX = false;
            ParticleColor = world.fallingStyle == FallingStyle.Ice ? new Color(0.75f, 0.92f, 1f)
                : world.fallingStyle == FallingStyle.Star ? new Color(1f, 0.88f, 0.35f) : new Color(0.55f, 0.46f, 0.4f);
            hitbox.radius = world.fallingStyle == FallingStyle.Ice ? 0.32f : 0.42f;
            spin = world.fallingStyle == FallingStyle.Ice ? 0f : Random.Range(-260f, 260f);
            maxFall = fallSpeed;
            velocity = new Vector2(0f, -2.5f);
            transform.position = new Vector3(x, camera.TopY + 1f, 0f);
        }

        void Prepare(CameraController camera, float warning)
        {
            cam = camera;
            phase = Phase.Warning;
            warnLeft = warning;
            age = 0f;
            spent = false;
            transform.rotation = Quaternion.identity;
            body.color = Color.white;
            body.enabled = false;
            hitbox.enabled = false;
            sign.enabled = true;
            gameObject.SetActive(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            switch (phase)
            {
                case Phase.Warning: UpdateWarning(dt); break;
                case Phase.Active: UpdateActive(dt); break;
                case Phase.Defeated: UpdateDefeated(dt); break;
            }
        }

        void UpdateWarning(float dt)
        {
            warnLeft -= dt;
            sign.enabled = Mathf.Repeat(warnLeft, 0.24f) > 0.09f;
            if (!IsFlyer)
            {
                // O aviso acompanha o topo da tela; o objeto nasce logo acima dele.
                float x = transform.position.x;
                transform.position = new Vector3(x, cam.TopY + 1f, 0f);
                sign.transform.position = new Vector3(x, cam.TopY - 0.9f, 0f);
            }
            if (warnLeft > 0f) return;

            phase = Phase.Active;
            sign.enabled = false;
            body.enabled = true;
            hitbox.enabled = true;
        }

        void UpdateActive(float dt)
        {
            age += dt;
            var p = (Vector2)transform.position;
            if (IsFlyer)
            {
                p.x += velocity.x * dt;
                p.y = baseY + Mathf.Sin(age * waveFreq) * waveAmp;
                body.sprite = frames[Mathf.FloorToInt(age / FlapInterval) % frames.Length];
                bool passed = velocity.x > 0f ? p.x > exitX : p.x < exitX;
                if (passed) { owner.Release(this); return; }
            }
            else
            {
                velocity.y = Mathf.MoveTowards(velocity.y, -maxFall, 14f * dt);
                p += velocity * dt;
                if (spin != 0f) transform.Rotate(0f, 0f, spin * dt);
                if (p.y < cam.BottomY - 2f) { owner.Release(this); return; }
            }
            transform.position = p;
        }

        void UpdateDefeated(float dt)
        {
            velocity.y -= 22f * dt;
            transform.position += (Vector3)(velocity * dt);
            transform.Rotate(0f, 0f, 540f * dt);
            if (transform.position.y < cam.BottomY - 2f) owner.Release(this);
        }

        /// <summary>Pisado pelo jogador: cai girando para fora da tela.</summary>
        public void Defeat()
        {
            phase = Phase.Defeated;
            hitbox.enabled = false;
            body.sprite = frames[0];
            velocity = new Vector2(velocity.x * 0.3f, 3f);
        }

        /// <summary>Já empurrou o jogador: segue o caminho sem atingir de novo, levemente transparente.</summary>
        public void MarkSpent()
        {
            spent = true;
            hitbox.enabled = false;
            body.color = new Color(1f, 1f, 1f, 0.55f);
        }

        void OnTriggerEnter2D(Collider2D other) => Touch(other);
        void OnTriggerStay2D(Collider2D other) => Touch(other);

        void Touch(Collider2D other)
        {
            if (!IsDangerous) return;
            var rb = other.attachedRigidbody;
            var player = rb != null ? rb.GetComponent<PlayerController>() : null;
            if (player != null) owner.OnPlayerContact(this, player);
        }
    }
}
