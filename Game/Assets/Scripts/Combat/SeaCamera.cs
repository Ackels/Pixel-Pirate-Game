using UnityEngine;

namespace Pixel.Combat
{
    /// <summary>Câmera do mar: desliza na horizontal para enquadrar os dois navios durante um combate.</summary>
    public class SeaCamera : MonoBehaviour
    {
        public float targetX;
        public float smoothing = 3f;
        float shake;
        Vector3 shakeOffset;

        /// <summary>Treme a câmera (ex.: bala de canhão acertando o seu navio).</summary>
        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        void LateUpdate()
        {
            var p = transform.position - shakeOffset;
            p.x = Mathf.Lerp(p.x, targetX, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            if (Mathf.Abs(p.x - targetX) < 0.01f) p.x = targetX;
            shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 1.5f);
            shakeOffset = shake > 0f ? (Vector3)(Random.insideUnitCircle * shake) : Vector3.zero;
            transform.position = p + shakeOffset;
        }
    }
}
