using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Pixel.Combat
{
    /// <summary>
    /// Câmera do mar: desliza na horizontal para enquadrar os dois navios durante um combate e, se eles não
    /// couberem, afasta o zoom (targetWidth = largura do mundo que precisa aparecer). Enquanto está afastada, o
    /// "pixel perfeito" fica desligado; ao voltar ao tamanho normal, liga de novo.
    /// </summary>
    public class SeaCamera : MonoBehaviour
    {
        public float targetX;
        [Tooltip("Largura (em blocos) que precisa caber na tela. 0 = tamanho normal.")]
        public float targetWidth;
        public float smoothing = 3f;
        float shake;
        Vector3 shakeOffset;
        Camera cam;
        PixelPerfectCamera ppc;
        bool zoomed;
        float baseSize, baseY;

        /// <summary>Treme a câmera (ex.: bala de canhão acertando o seu navio).</summary>
        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        void Start()
        {
            cam = GetComponent<Camera>();
            ppc = GetComponent<PixelPerfectCamera>();
            baseY = transform.position.y;
            baseSize = cam != null ? cam.orthographicSize : 8.4375f;
        }

        void LateUpdate()
        {
            float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            var p = transform.position - shakeOffset;
            p.x = Mathf.Lerp(p.x, targetX, k);
            if (Mathf.Abs(p.x - targetX) < 0.01f) p.x = targetX;

            bool editing = Pixel.Ship.SeaController.Instance != null && Pixel.Ship.SeaController.Instance.Mode == Pixel.Ship.SeaMode.Editing;
            if (cam != null && !editing)
            {
                if (!zoomed) { baseSize = cam.orthographicSize; baseY = p.y; }
                float want = targetWidth > 0f ? Mathf.Max(baseSize, targetWidth / cam.aspect / 2f) : baseSize;
                if (!zoomed && want > baseSize + 0.05f)
                {
                    zoomed = true;
                    if (ppc != null) ppc.enabled = false;
                    cam.orthographicSize = baseSize;
                }
                if (zoomed)
                {
                    cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, want, k);
                    // Afastando, mostra mais céu que fundo do mar (a linha d'água desce um pouco na tela)
                    p.y = baseY + (cam.orthographicSize - baseSize) * 0.45f;
                    if (want <= baseSize + 0.05f && Mathf.Abs(cam.orthographicSize - baseSize) < 0.03f)
                    {
                        zoomed = false;
                        cam.orthographicSize = baseSize;
                        p.y = baseY;
                        if (ppc != null) ppc.enabled = true;
                    }
                }
            }

            shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 1.5f);
            shakeOffset = shake > 0f ? (Vector3)(Random.insideUnitCircle * shake) : Vector3.zero;
            transform.position = p + shakeOffset;
        }
    }
}
