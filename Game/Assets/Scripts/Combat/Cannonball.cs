using System;
using UnityEngine;

namespace Pixel.Combat
{
    /// <summary>Bala de canhão em arco. Ao chegar: explosão (acerto) ou respingo na água (erro).</summary>
    public class Cannonball : MonoBehaviour
    {
        Vector2 from, to;
        float flight, age, arc;
        bool hit;
        Action onHit;

        public static void Fire(Vector2 from, Vector2 to, bool hit, Action onHit)
        {
            var go = new GameObject("BalaDeCanhao");
            go.transform.position = from;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Fx.Cannonball;
            sr.sortingOrder = 35;
            var b = go.AddComponent<Cannonball>();
            b.from = from;
            b.to = to;
            b.hit = hit;
            b.onHit = onHit;
            float dist = Vector2.Distance(from, to);
            b.flight = 0.45f + dist / 16f;
            b.arc = 1.5f + dist * 0.12f;
            Fx.Spawn(Fx.Puff, from, 0.6f, 2f);
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / flight);
            Vector2 p = Vector2.Lerp(from, to, t) + Vector2.up * (arc * 4f * t * (1f - t));
            transform.position = p;
            if (t < 1f) return;

            if (hit)
            {
                Fx.Spawn(Fx.Boom, to, 0.35f, 1.8f);
                Fx.Spawn(Fx.Puff, to + Vector2.up * 0.3f, 0.8f, 2.2f);
                onHit?.Invoke();
            }
            else Fx.Spawn(Fx.Splash, to + Vector2.up * 0.4f, 0.6f, 1.3f);
            Destroy(gameObject);
        }
    }
}
