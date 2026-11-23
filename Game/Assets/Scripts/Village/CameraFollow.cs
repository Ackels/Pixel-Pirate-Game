using UnityEngine;

namespace Pixel.Village
{
    /// <summary>
    /// Câmera que segue o alvo suavemente e não sai dos limites do mapa.
    /// A Pixel Perfect Camera (no mesmo objeto) cuida de alinhar ao pixel.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        [Tooltip("Quanto maior, mais rápido a câmera alcança o alvo.")]
        public float smoothing = 8f;
        [Tooltip("Limites do mapa em unidades do mundo (canto inferior esquerdo e tamanho).")]
        public Rect bounds = new Rect(0, 0, 64, 40);

        Camera cam;

        void Awake() => cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 goal = target.position;
            Vector3 pos = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

            if (cam != null && cam.orthographic)
            {
                float halfH = cam.orthographicSize;
                float halfW = halfH * cam.aspect;
                pos.x = bounds.width > halfW * 2 ? Mathf.Clamp(pos.x, bounds.xMin + halfW, bounds.xMax - halfW) : bounds.center.x;
                pos.y = bounds.height > halfH * 2 ? Mathf.Clamp(pos.y, bounds.yMin + halfH, bounds.yMax - halfH) : bounds.center.y;
            }
            pos.z = transform.position.z;
            transform.position = pos;
        }
    }
}
