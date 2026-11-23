using UnityEngine;

namespace Pixel.Combat
{
    /// <summary>Efeitos visuais rápidos (fumaça, explosão, respingo, golpe) que somem sozinhos.</summary>
    public static class Fx
    {
        public static Sprite Puff, Boom, Splash, Slash, Cannonball;

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

        public static void Spawn(Sprite sprite, Vector2 pos, float life = 0.5f, float grow = 1.6f, bool flipX = false, int order = 30)
        {
            if (sprite == null) return;
            var go = new GameObject("Fx");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.flipX = flipX;
            sr.sortingOrder = order;
            var f = go.AddComponent<FadeOut>();
            f.life = life;
            f.grow = grow;
        }

        class FadeOut : MonoBehaviour
        {
            public float life = 0.5f, grow = 1.6f;
            float age;
            SpriteRenderer sr;
            void Awake() => sr = GetComponent<SpriteRenderer>();
            void Update()
            {
                age += Time.deltaTime;
                float t = age / life;
                if (t >= 1f) { Destroy(gameObject); return; }
                transform.localScale = Vector3.one * Mathf.Lerp(1f, grow, t);
                var c = sr.color; c.a = 1f - t * t; sr.color = c;
            }
        }
    }
}
