using Pixel.Village;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Pixel.Combat
{
    /// <summary>Luzes quentes (fogueira, velas, lanternas): brilham bem mais à noite e tremulam como fogo.</summary>
    public static class Glow
    {
        public static Light2D Add(Transform parent, Vector2 local, Color color, float radius, float intensity, bool flicker = true)
        {
            var go = new GameObject("Luz");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.15f;
            l.falloffIntensity = 0.65f;
            var f = go.AddComponent<LightFlicker>();
            f.baseIntensity = intensity;
            f.flicker = flicker;
            return l;
        }

        public static readonly Color Fire = new Color(1f, 0.62f, 0.3f);
        public static readonly Color Candle = new Color(1f, 0.78f, 0.45f);
    }

    /// <summary>De dia a luz é fraquinha; de noite brilha forte. Fogo tremula.</summary>
    public class LightFlicker : MonoBehaviour
    {
        public float baseIntensity = 1f;
        public bool flicker = true;
        Light2D l;
        float seed;

        void Start() { l = GetComponent<Light2D>(); seed = Random.value * 100f; }

        void Update()
        {
            if (l == null) return;
            var cm = CrewManager.Instance;
            float night = cm != null ? Pixel.Ship.SeaSky.Night(cm.DayProgress) : 0f;
            float k = flicker ? 0.82f + 0.18f * Mathf.PerlinNoise(seed, Time.time * 3.5f) : 1f;
            l.intensity = baseIntensity * k * (0.25f + 0.75f * night);
        }
    }
}
