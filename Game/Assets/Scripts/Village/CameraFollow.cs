using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Pixel.Village
{
    /// <summary>
    /// Câmera da vila: segue o alvo suavemente, não sai dos limites do mapa e escolhe o zoom.
    /// O zoom é sempre inteiro (1 pixel da arte = 2, 3, 4... pixels da tela), então a arte fica nítida
    /// em qualquer tamanho de janela. Por padrão mostra uns 16 blocos de altura; a rodinha do mouse aproxima ou afasta.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        [Tooltip("Quanto maior, mais rápido a câmera alcança o alvo.")]
        public float smoothing = 8f;
        [Tooltip("Limites do mapa em unidades do mundo (canto inferior esquerdo e tamanho).")]
        public Rect bounds = new Rect(0, 0, 64, 40);
        [Tooltip("Pixels da arte por bloco.")]
        public int pixelsPerUnit = 16;
        [Tooltip("Quantos blocos de altura aparecem no zoom padrão (o zoom inteiro mais perto disso).")]
        public float tilesTall = 16f;
        [Tooltip("Mínimo e máximo de blocos de altura que a rodinha deixa ver.")]
        public Vector2 tilesTallRange = new Vector2(7f, 36f);

        Camera cam;
        int zoomStep;            // + = mais perto, - = mais longe (passos da rodinha)
        int lastScreenH = -1, scale = 1;
        bool snapped;

        /// <summary>Quantos pixels da tela cada pixel da arte ocupa agora.</summary>
        public int Scale => scale;

        void Awake() => cam = GetComponent<Camera>();

        void OnEnable() => snapped = false;

        void LateUpdate()
        {
            if (cam == null) return;
            ReadWheel();
            ApplyZoom();
            if (target == null) return;
            Vector3 goal = target.position;
            Vector3 pos = snapped ? Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-smoothing * Time.deltaTime)) : goal;
            snapped = true;

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            pos.x = bounds.width > halfW * 2 ? Mathf.Clamp(pos.x, bounds.xMin + halfW, bounds.xMax - halfW) : bounds.center.x;
            pos.y = bounds.height > halfH * 2 ? Mathf.Clamp(pos.y, bounds.yMin + halfH, bounds.yMax - halfH) : bounds.center.y;
            pos.z = transform.position.z;
            transform.position = pos;
        }

        void ReadWheel()
        {
            var mouse = Mouse.current;
            if (mouse == null || GameMenu.IsOpen) return;
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) < 0.01f) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;   // rolando uma lista
            int before = zoomStep;
            zoomStep += wheel > 0 ? 1 : -1;
            if (ScaleFor(zoomStep) == ScaleFor(before)) zoomStep = before;   // já no limite
            lastScreenH = -1;
        }

        int DefaultScale(int screenH) => Mathf.Max(1, Mathf.RoundToInt(screenH / (tilesTall * pixelsPerUnit)));

        int ScaleFor(int step)
        {
            int h = Mathf.Max(1, cam.pixelHeight);
            int min = Mathf.Max(1, Mathf.CeilToInt(h / (tilesTallRange.y * pixelsPerUnit)));
            int max = Mathf.Max(min, Mathf.FloorToInt(h / (tilesTallRange.x * pixelsPerUnit)));
            return Mathf.Clamp(DefaultScale(h) + step, min, max);
        }

        void ApplyZoom()
        {
            int h = cam.pixelHeight;
            if (h == lastScreenH) return;
            lastScreenH = h;
            scale = ScaleFor(zoomStep);
            cam.orthographicSize = h / (2f * pixelsPerUnit * scale);
        }
    }
}
