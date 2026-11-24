using System;
using Pixel.Village;
using UnityEngine;

namespace Pixel.Combat
{
    /// <summary>
    /// Bala de canhão em arco: clarão e fumaça na boca do canhão, a bala gira deixando um rastro de fumaça
    /// e, ao chegar, explode com lascas de madeira (acerto) ou faz um respingo na água (erro).
    /// </summary>
    public class Cannonball : MonoBehaviour
    {
        Vector2 from, to;
        float flight, age, arc, trail;
        bool hit;
        Action onHit;

        /// <param name="rightward">O canhão aponta para a direita (o clarão vira para o lado certo).</param>
        public static void Fire(Vector2 from, Vector2 to, bool hit, Action onHit, bool rightward = true)
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

            // Boca do canhão: clarão (o desenho original aponta para a esquerda) + fumaça
            float dir = rightward ? 1f : -1f;
            if (Fx.Flash != null && Fx.Flash.Length > 0) Fx.Play(Fx.Flash, from + new Vector2(dir * 0.25f, 0f), 20f, rightward, 36);
            if (Fx.Smoke != null && Fx.Smoke.Length > 0) Fx.Play(Fx.Smoke, from + new Vector2(dir * 0.4f, 0.1f), 12f, rightward, 34, 0.9f);
            else Fx.Spawn(Fx.Puff, from, 0.6f, 2f);
            AudioManager.PlayVaried(Sfx.Cannon, 0.8f);
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / flight);
            Vector2 p = Vector2.Lerp(from, to, t) + Vector2.up * (arc * 4f * t * (1f - t));
            transform.position = p;
            transform.Rotate(0, 0, (to.x > from.x ? -1f : 1f) * 540f * Time.deltaTime);

            // Rastro de fumaça
            trail -= Time.deltaTime;
            if (trail <= 0f && t < 0.95f)
            {
                trail = 0.045f;
                Fx.Spawn(Fx.Puff, p, 0.35f, 1.4f, false, 34, 0.35f);
            }
            if (t < 1f) return;

            if (hit)
            {
                if (Fx.Explosion != null && Fx.Explosion.Length > 0) Fx.Play(Fx.Explosion, to, 16f, false, 37);
                else Fx.Spawn(Fx.Boom, to, 0.35f, 1.8f);
                Fx.Splinters(to, 7);
                Fx.Spawn(Fx.Puff, to + Vector2.up * 0.3f, 0.9f, 2.4f);
                AudioManager.PlayVaried(Sfx.Boom, 0.8f);
                AudioManager.PlayVaried(Sfx.Wood, 0.7f, 0.2f);
                onHit?.Invoke();
            }
            else
            {
                if (Fx.SplashAnim != null && Fx.SplashAnim.Length > 0) Fx.Play(Fx.SplashAnim, to + Vector2.up * 0.35f, 12f, false, 36, 1.6f);
                else Fx.Spawn(Fx.Splash, to + Vector2.up * 0.4f, 0.6f, 1.3f);
                AudioManager.PlayVaried(Sfx.Splash, 0.7f);
            }
            Destroy(gameObject);
        }
    }
}
