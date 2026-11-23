using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Pixel.Village
{
    /// <summary>
    /// Ciclo de dia e noite: muda cor e intensidade da luz global conforme o relógio do dia
    /// (amanhecer dourado → meio-dia claro → pôr do sol laranja → noite azulada).
    /// Serve para a vila e para o mar (os dois leem o mesmo relógio).
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class DayNightLight : MonoBehaviour
    {
        [Tooltip("Opcional: câmera cujo fundo (céu) acompanha a luz.")]
        public Camera tintCamera;
        public Color skyColor = new Color(0.53f, 0.78f, 0.91f);
        Light2D light2D;
        Gradient colors;
        AnimationCurve intensity;

        void Awake()
        {
            light2D = GetComponent<Light2D>();
            colors = new Gradient();
            colors.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.82f, 0.68f), 0f),     // amanhecer
                    new GradientColorKey(new Color(1f, 0.98f, 0.93f), 0.2f),    // manhã
                    new GradientColorKey(new Color(1f, 0.98f, 0.93f), 0.6f),    // tarde
                    new GradientColorKey(new Color(1f, 0.68f, 0.45f), 0.74f),   // pôr do sol
                    new GradientColorKey(new Color(0.52f, 0.58f, 0.95f), 0.86f),// noite
                    new GradientColorKey(new Color(0.7f, 0.68f, 0.85f), 1f),    // madrugada
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            intensity = new AnimationCurve(
                new Keyframe(0f, 0.85f), new Keyframe(0.2f, 1f), new Keyframe(0.6f, 1f),
                new Keyframe(0.74f, 0.9f), new Keyframe(0.86f, 0.6f), new Keyframe(1f, 0.75f));
        }

        void Update()
        {
            var cm = CrewManager.Instance;
            if (cm == null) return;
            float t = cm.DayProgress;
            light2D.color = colors.Evaluate(t);
            light2D.intensity = intensity.Evaluate(t);
            if (tintCamera != null)
            {
                var c = skyColor * light2D.color * Mathf.Lerp(0.55f, 1f, (light2D.intensity - 0.6f) / 0.4f);
                c.a = 1f;
                tintCamera.backgroundColor = c;
            }
        }
    }
}
