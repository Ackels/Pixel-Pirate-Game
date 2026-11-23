using UnityEngine;

namespace Pixel.Ship
{
    /// <summary>
    /// Dá a sensação de navegar: o navio fica parado e o cenário (água, nuvens, ilha, docas)
    /// desliza para a esquerda, cada camada numa velocidade (parallax).
    /// </summary>
    public class SeaScroller : MonoBehaviour
    {
        [Tooltip("Velocidade atual do navio em unidades/s (0 = parado).")]
        public float speed;
        [Tooltip("Movimento da água mesmo parado (ondas).")]
        public float idleWaterDrift = 0.4f;

        [Header("Camadas")]
        public Transform[] waterLayers;     // sprites em modo Tiled, repetem a cada 1 unidade
        public Transform[] clouds;          // voltam pela direita quando saem pela esquerda
        public float cloudFactor = 0.35f;
        public float cloudWrapLeft = -32f, cloudWrapWidth = 64f;
        public Transform farScenery;        // ilha distante
        public float farFactor = 0.12f;
        public Transform nearScenery;       // docas
        public float nearFactor = 1f;

        Vector3[] waterStart;

        void Start()
        {
            waterStart = new Vector3[waterLayers.Length];
            for (int i = 0; i < waterLayers.Length; i++) waterStart[i] = waterLayers[i].position;
        }

        float waterOffset;

        void Update()
        {
            float dt = Time.deltaTime;
            waterOffset = Mathf.Repeat(waterOffset + (speed + idleWaterDrift) * dt, 1f);
            for (int i = 0; i < waterLayers.Length; i++)
            {
                if (waterLayers[i] == null) continue;
                // camada da frente anda um pouco mais rápido que a de trás
                float o = Mathf.Repeat(waterOffset * (i % 2 == 0 ? 1f : 1.3f), 1f);
                waterLayers[i].position = waterStart[i] + Vector3.left * o;
            }

            foreach (var c in clouds)
            {
                if (c == null) continue;
                c.position += Vector3.left * ((speed * cloudFactor + 0.15f) * dt);
                if (c.position.x < cloudWrapLeft) c.position += Vector3.right * cloudWrapWidth;
            }

            if (farScenery != null) farScenery.position += Vector3.left * (speed * farFactor * dt);
            if (nearScenery != null) nearScenery.position += Vector3.left * (speed * nearFactor * dt);
        }
    }
}
