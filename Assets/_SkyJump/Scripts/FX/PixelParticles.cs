using System.Collections.Generic;
using UnityEngine;

namespace SkyJump.FX
{
    /// <summary>
    /// Partículas quadradas simples (pixels) com pool. Criado automaticamente na primeira chamada.
    /// </summary>
    public class PixelParticles : MonoBehaviour
    {
        struct Particle
        {
            public SpriteRenderer renderer;
            public Vector2 velocity;
            public float life;
            public float maxLife;
            public Color color;
        }

        static PixelParticles instance;
        static Sprite pixelSprite;

        readonly List<Particle> live = new List<Particle>();
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();

        [SerializeField] float pixelSize = 0.12f;
        [SerializeField] float gravity = 9f;
        [SerializeField] int sortingOrder = 20;

        static PixelParticles Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<PixelParticles>();
                    if (instance == null) instance = new GameObject("PixelParticles").AddComponent<PixelParticles>();
                }
                return instance;
            }
        }

        static Sprite PixelSprite
        {
            get
            {
                if (pixelSprite == null)
                {
                    var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    pixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                }
                return pixelSprite;
            }
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Burst(Vector2 position, Color color, int count, float speed)
        {
            if (!Application.isPlaying) return;
            Instance.Emit(position, color, count, speed);
        }

        void Emit(Vector2 position, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                var sr = pool.Count > 0 ? pool.Pop() : CreateRenderer();
                sr.gameObject.SetActive(true);
                sr.transform.position = position + Random.insideUnitCircle * 0.15f;
                float size = pixelSize * Random.Range(0.8f, 1.5f);
                sr.transform.localScale = new Vector3(size, size, 1f);
                Vector2 dir = new Vector2(Random.Range(-1f, 1f), Random.Range(0.2f, 1f)).normalized;
                float life = Random.Range(0.35f, 0.7f);
                live.Add(new Particle { renderer = sr, velocity = dir * speed * Random.Range(0.5f, 1f), life = life, maxLife = life, color = color });
                sr.color = color;
            }
        }

        SpriteRenderer CreateRenderer()
        {
            var go = new GameObject("px");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelSprite;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                p.life -= dt;
                if (p.life <= 0f)
                {
                    p.renderer.gameObject.SetActive(false);
                    pool.Push(p.renderer);
                    live.RemoveAt(i);
                    continue;
                }
                p.velocity.y -= gravity * dt;
                p.renderer.transform.position += (Vector3)(p.velocity * dt);
                var c = p.color;
                c.a *= p.life / p.maxLife;
                p.renderer.color = c;
                live[i] = p;
            }
        }
    }
}
