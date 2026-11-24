using System.Collections.Generic;
using Pixel.Village;
using UnityEngine;

namespace Pixel.Ship
{
    public enum Weather { Clear, Rain, Storm }

    /// <summary>
    /// Céu do mar que muda com o relógio do dia (amanhecer, dia, tarde, pôr do sol, noite): degradê, sol e lua
    /// cruzando o céu, estrelas à noite. E o clima: chuva e tempestade (mais escuro, chuva forte, raios e trovões).
    /// Acompanha a câmera. A cor de baixo do degradê é a do fundo da câmera (a mesma da faixa do horizonte).
    /// </summary>
    public class SeaSky : MonoBehaviour
    {
        public static SeaSky Instance { get; private set; }

        public Camera cam;
        public float horizonY;               // topo da faixa do horizonte
        public Sprite sun, moon, star, drop;
        [Tooltip("Nuvens do cenário: ficam cinza-chumbo na chuva e na tempestade.")]
        public SpriteRenderer[] clouds;

        public Weather Current { get; private set; }
        /// <summary>0 = céu limpo, 1 = tempestade fechada (escurece a luz e acinzenta o céu).</summary>
        public float Gloom { get; private set; }

        SpriteRenderer gradient, sunR, moonR, flash;
        Texture2D gradTex;
        readonly List<SpriteRenderer> stars = new List<SpriteRenderer>();
        readonly List<Vector2> starPos = new List<Vector2>();
        readonly List<SpriteRenderer> rain = new List<SpriteRenderer>();
        float weatherLeft, rainAmount, lightningTimer, flashAlpha, thunderAt = -1f, gradTimer;
        Material unlit;

        static readonly Gradient Zenith = MakeZenith();

        static Gradient MakeZenith()
        {
            var g = new Gradient();
            Color C(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
            g.SetKeys(new[]
            {
                new GradientColorKey(C("2e3568"), 0f),     // madrugada
                new GradientColorKey(C("7e86c8"), 0.06f),  // amanhecer
                new GradientColorKey(C("62b0e0"), 0.16f),  // manhã
                new GradientColorKey(C("4fa6e2"), 0.45f),  // meio-dia
                new GradientColorKey(C("86a9cf"), 0.63f),  // tarde
                new GradientColorKey(C("6f5a9e"), 0.72f),  // pôr do sol
                new GradientColorKey(C("2a2558"), 0.8f),   // anoitecer
                new GradientColorKey(C("0f1030"), 0.9f),   // noite
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        void Awake()
        {
            Instance = this;
            var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh != null) unlit = new Material(sh);

            gradTex = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            gradient = Make("CeuDegrade", Sprite.Create(gradTex, new Rect(0, 0, 1, 32), new Vector2(0.5f, 0f), 1f), -100);
            sunR = Make("Sol", sun, -97);
            moonR = Make("Lua", moon, -97);
            var rng = new System.Random(11);
            for (int i = 0; i < 70; i++)
            {
                stars.Add(Make("Estrela", star, -98));
                starPos.Add(new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()));
            }
            for (int i = 0; i < 160; i++)
            {
                var r = Make("Chuva", drop, 26);
                r.gameObject.SetActive(false);
                rain.Add(r);
            }
            flash = Make("Relampago", Combat.Fx.White, 60);
            flash.color = new Color(1, 1, 1, 0);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        SpriteRenderer Make(string name, Sprite s, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = s;
            sr.sortingOrder = order;
            if (unlit != null) sr.sharedMaterial = unlit;
            return sr;
        }

        /// <summary>Começa um clima por alguns segundos (chuva ou tempestade).</summary>
        public void StartWeather(Weather w, float seconds)
        {
            Current = w;
            weatherLeft = seconds;
            if (w == Weather.Storm) lightningTimer = Random.Range(2f, 5f);
            AudioManager.Instance?.SetWeatherSound(w);
        }

        public void ClearWeather() { if (Current != Weather.Clear) StartWeather(Weather.Clear, 0f); }

        void LateUpdate()
        {
            if (cam == null) return;
            float dt = Time.deltaTime;
            var cm = CrewManager.Instance;
            float t = cm != null ? cm.DayProgress : 0.4f;

            // Clima: vai e vem suavemente
            if (Current != Weather.Clear)
            {
                weatherLeft -= dt;
                if (weatherLeft <= 0f) StartWeather(Weather.Clear, 0f);
            }
            float targetGloom = Current == Weather.Storm ? 1f : Current == Weather.Rain ? 0.5f : 0f;
            float targetRain = Current == Weather.Storm ? 1f : Current == Weather.Rain ? 0.55f : 0f;
            Gloom = Mathf.MoveTowards(Gloom, targetGloom, dt / 4f);
            rainAmount = Mathf.MoveTowards(rainAmount, targetRain, dt / 3f);

            float h = cam.orthographicSize, w = h * cam.aspect;
            Vector2 c = cam.transform.position;
            float top = c.y + h + 1f, left = c.x - w - 1f, width = 2f * w + 2f;
            float skyH = Mathf.Max(1f, top - horizonY);

            // Degradê: em cima a cor da hora do dia, embaixo a do horizonte (fundo da câmera)
            gradTimer -= dt;
            if (gradTimer <= 0f)
            {
                gradTimer = 0.1f;
                var gray = Color.Lerp(new Color(0.55f, 0.58f, 0.64f), new Color(0.12f, 0.13f, 0.18f), Night(t));
                var zen = Color.Lerp(Zenith.Evaluate(t), gray, Gloom * 0.85f);
                var bottom = cam.backgroundColor;
                for (int i = 0; i < 32; i++) gradTex.SetPixel(0, i, Color.Lerp(bottom, zen, Mathf.Pow(i / 31f, 0.8f)));
                gradTex.Apply();
            }
            if (clouds != null)
            {
                var cloudTint = Color.Lerp(Color.white, new Color(0.5f, 0.52f, 0.58f), Gloom);
                foreach (var cl in clouds) if (cl != null) cl.color = cloudTint;
            }
            gradient.transform.position = new Vector3(c.x, horizonY - 0.5f, 0);
            gradient.transform.localScale = new Vector3(width, (skyH + 0.5f) / 32f, 1);

            // Sol (de dia) e lua (de noite) cruzando o céu e sumindo atrás do horizonte
            float clear = 1f - Gloom * 0.9f;
            Arc(sunR, (t - 0.02f) / 0.76f,left + 3f, width - 6f, skyH);
            float sunset = Mathf.InverseLerp(0.6f, 0.76f, t);
            sunR.color = new Color(1f, Mathf.Lerp(1f, 0.62f, sunset), Mathf.Lerp(1f, 0.4f, sunset), clear);
            float mu = t >= 0.78f ? (t - 0.78f) / 0.27f : t < 0.05f ? (t + 0.22f) / 0.27f : -1f;
            Arc(moonR, mu, left + 3f, width - 6f, skyH);
            moonR.color = new Color(1, 1, 1, clear);

            // Estrelas piscando à noite
            float night = Night(t) * clear;
            for (int i = 0; i < stars.Count; i++)
            {
                var p = starPos[i];
                stars[i].transform.position = new Vector3(left + p.x * width, horizonY + 1.5f + p.y * (skyH - 2f), 0);
                float tw = 0.65f + 0.35f * Mathf.Sin(Time.time * (1.5f + i % 5) + i);
                stars[i].color = new Color(1, 1, 1, night * tw);
                stars[i].enabled = night > 0.01f;
            }

            // Chuva: riscos caindo na diagonal; somem na água e voltam lá em cima
            int want = Mathf.RoundToInt(rainAmount * rain.Count);
            Vector2 vel = Current == Weather.Storm ? new Vector2(-7f, -20f) : new Vector2(-4f, -15f);
            for (int i = 0; i < rain.Count; i++)
            {
                var r = rain[i];
                bool on = i < want;
                if (r.gameObject.activeSelf != on)
                {
                    r.gameObject.SetActive(on);
                    if (on) r.transform.position = new Vector3(left + Random.value * (width + 6f), horizonY + Random.value * (top - horizonY + 3f) - 2f, 0);
                }
                if (!on) continue;
                var p = (Vector2)r.transform.position + vel * dt;
                if (p.y < horizonY - 3f || p.x < left - 1f) p = new Vector2(left + Random.value * (width + 6f), top + Random.value * 2f);
                r.transform.position = p;
            }

            // Tempestade: relâmpago (clarão na tela) e o trovão logo depois
            if (Current == Weather.Storm)
            {
                lightningTimer -= dt;
                if (lightningTimer <= 0f)
                {
                    lightningTimer = Random.Range(4f, 10f);
                    flashAlpha = Random.Range(0.45f, 0.7f);
                    thunderAt = Time.time + Random.Range(0.2f, 0.8f);
                }
            }
            if (thunderAt > 0f && Time.time >= thunderAt) { thunderAt = -1f; AudioManager.PlayVaried(Sfx.Thunder, 1f, 0.08f, 0.5f); }
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 2.5f);
            flash.transform.position = new Vector3(c.x, c.y, 0);
            flash.transform.localScale = new Vector3(width + 2f, 2f * h + 2f, 1);
            flash.color = new Color(1, 1, 1, flashAlpha);
        }

        /// <summary>Quanto é noite (0 de dia, 1 de noite fechada).</summary>
        public static float Night(float t) =>
            t >= 0.72f ? Mathf.Clamp01((t - 0.72f) / 0.1f) : t < 0.06f ? 1f - t / 0.06f : 0f;

        /// <summary>Posiciona um astro num arco: u 0..1 = do horizonte à esquerda até o da direita (fora disso, escondido).</summary>
        void Arc(SpriteRenderer r, float u, float x0, float span, float skyH)
        {
            bool show = u >= -0.02f && u <= 1.02f;
            r.enabled = show;
            if (!show) return;
            float y = horizonY - 0.8f + Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * (skyH * 0.72f);
            r.transform.position = new Vector3(x0 + Mathf.Clamp01(u) * span, y, 0);
        }
    }
}
