using UnityEngine;

namespace Pixel.Combat
{
    /// <summary>
    /// Efeitos visuais rápidos que somem sozinhos: os antigos (um quadro que cresce e some) e os animados
    /// (explosão, clarão do canhão, respingo, fumaça) mais as lascas de madeira voando.
    /// </summary>
    public static class Fx
    {
        public static Sprite Puff, Boom, Splash, Slash, Cannonball;
        /// <summary>Quadros das animações (configurados pelo EncounterDirector).</summary>
        public static Sprite[] Explosion, Flash, SplashAnim, Smoke, Debris, Cracks, CannonFire;

        static Sprite white;
        public static Sprite White
        {
            get
            {
                if (white != null) return white;
                var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
                return white;
            }
        }

        public static void Spawn(Sprite sprite, Vector2 pos, float life = 0.5f, float grow = 1.6f, bool flipX = false, int order = 30, float scale = 1f)
        {
            if (sprite == null) return;
            var go = new GameObject("Fx");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.flipX = flipX;
            sr.sortingOrder = order;
            var f = go.AddComponent<FadeOut>();
            f.life = life;
            f.grow = grow;
            f.baseScale = scale;
        }

        /// <summary>Toca uma animação uma vez e some.</summary>
        public static SpriteRenderer Play(Sprite[] frames, Vector2 pos, float fps = 14f, bool flipX = false, int order = 31, float scale = 1f)
        {
            if (frames == null || frames.Length == 0) return null;
            var go = new GameObject("FxAnim");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.flipX = flipX;
            sr.sortingOrder = order;
            var a = go.AddComponent<SpriteAnim>();
            a.frames = frames;
            a.fps = fps;
            a.destroyAtEnd = true;
            return sr;
        }

        /// <summary>Lascas de madeira voando com gravidade (impacto de bala, navio afundando).</summary>
        public static void Splinters(Vector2 pos, int count, int order = 33)
        {
            if (Debris == null || Debris.Length == 0) return;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Lasca");
                go.transform.position = pos + Random.insideUnitCircle * 0.2f;
                go.transform.localScale = Vector3.one * Random.Range(0.5f, 0.9f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Debris[Random.Range(0, Debris.Length)];
                sr.sortingOrder = order;
                var p = go.AddComponent<Piece>();
                p.vel = new Vector2(Random.Range(-4f, 4f), Random.Range(3f, 8f));
                p.spin = Random.Range(-720f, 720f);
                p.life = Random.Range(0.7f, 1.2f);
            }
        }

        class FadeOut : MonoBehaviour
        {
            public float life = 0.5f, grow = 1.6f, baseScale = 1f;
            float age;
            SpriteRenderer sr;
            void Awake() => sr = GetComponent<SpriteRenderer>();
            void Update()
            {
                age += Time.deltaTime;
                float t = age / life;
                if (t >= 1f) { Destroy(gameObject); return; }
                transform.localScale = Vector3.one * baseScale * Mathf.Lerp(1f, grow, t);
                var c = sr.color; c.a = 1f - t * t; sr.color = c;
            }
        }

        class Piece : MonoBehaviour
        {
            public Vector2 vel;
            public float spin, life;
            float age;
            SpriteRenderer sr;
            void Awake() => sr = GetComponent<SpriteRenderer>();
            void Update()
            {
                age += Time.deltaTime;
                if (age >= life) { Destroy(gameObject); return; }
                vel.y -= 20f * Time.deltaTime;
                transform.position += (Vector3)(vel * Time.deltaTime);
                transform.Rotate(0, 0, spin * Time.deltaTime);
                if (life - age < 0.3f) { var c = sr.color; c.a = (life - age) / 0.3f; sr.color = c; }
            }
        }
    }

    /// <summary>Anima um SpriteRenderer por uma lista de quadros (uma vez ou em loop).</summary>
    public class SpriteAnim : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 12f;
        public bool loop, destroyAtEnd;
        /// <summary>Ao terminar (sem loop), volta para este sprite (ex.: o canhão parado).</summary>
        public Sprite restore;
        float t;
        SpriteRenderer sr;

        void Awake() => sr = GetComponent<SpriteRenderer>();

        public void Restart(Sprite[] f, float speed, Sprite back = null)
        {
            frames = f; fps = speed; restore = back; t = 0f; enabled = true;
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (frames != null && frames.Length > 0) sr.sprite = frames[0];
        }

        void Update()
        {
            if (frames == null || frames.Length == 0 || sr == null) return;
            t += Time.deltaTime * fps;
            int i = (int)t;
            if (i >= frames.Length)
            {
                if (loop) { t %= frames.Length; i = (int)t; }
                else if (destroyAtEnd) { Destroy(gameObject); return; }
                else { if (restore != null) sr.sprite = restore; enabled = false; return; }
            }
            sr.sprite = frames[i];
        }
    }
}
