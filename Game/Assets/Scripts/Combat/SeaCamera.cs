using UnityEngine;

namespace Pixel.Combat
{
    /// <summary>Câmera do mar: desliza na horizontal para enquadrar os dois navios durante um combate.</summary>
    public class SeaCamera : MonoBehaviour
    {
        public float targetX;
        public float smoothing = 3f;

        void LateUpdate()
        {
            var p = transform.position;
            p.x = Mathf.Lerp(p.x, targetX, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            if (Mathf.Abs(p.x - targetX) < 0.01f) p.x = targetX;
            transform.position = p;
        }
    }
}
