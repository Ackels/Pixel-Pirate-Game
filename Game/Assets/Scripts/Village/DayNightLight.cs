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
                    new GradientColorKey(new Color(0.62f, 0.62f, 0.9f), 0f),    // madrugada
                    new GradientColorKey(new Color(1f, 0.8f, 0.66f), 0.07f),    // amanhecer
                    new GradientColorKey(new Color(1f, 0.98f, 0.93f), 0.2f),    // manhã
                    new GradientColorKey(new Color(1f, 0.97f, 0.9f), 0.5f),     // meio-dia
                    new GradientColorKey(new Color(1f, 0.88f, 0.7f), 0.63f),    // tarde dourada
                    new GradientColorKey(new Color(1f, 0.62f, 0.42f), 0.72f),   // pôr do sol
                    new GradientColorKey(new Color(0.72f, 0.52f, 0.78f), 0.78f),// anoitecer
                    new GradientColorKey(new Color(0.45f, 0.52f, 0.92f), 0.86f),// noite
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            intensity = new AnimationCurve(
                new Keyframe(0f, 0.55f), new Keyframe(0.07f, 0.8f), new Keyframe(0.2f, 1f), new Keyframe(0.6f, 1f),
                new Keyframe(0.72f, 0.88f), new Keyframe(0.78f, 0.7f), new Keyframe(0.86f, 0.5f), new Keyframe(1f, 0.52f));
        }

        void Update()
        {
            var cm = CrewManager.Instance;
            if (cm == null) return;
            float t = cm.DayProgress;
            var col = colors.Evaluate(t);
            float inten = intensity.Evaluate(t);
            // Chuva e tempestade no mar: luz mais fraca e acinzentada
            var sky = Pixel.Ship.SeaSky.Instance;
            if (sky != null && sky.gameObject.scene == gameObject.scene && sky.Gloom > 0f)
            {
                float g = sky.Gloom;
                float lum = (col.r + col.g + col.b) / 3f;
                col = Color.Lerp(col, new Color(lum * 0.9f, lum * 0.95f, lum * 1.05f), g * 0.7f);
                inten *= 1f - 0.3f * g;
            }
            light2D.color = col;
            light2D.intensity = inten;
            if (tintCamera != null)
            {
                // Mesma conta que a luz faz nos sprites: o fundo casa com a faixa do horizonte
                var c = skyColor * col * inten;
                c.a = 1f;
                tintCamera.backgroundColor = c;
            }
        }
    }
}
