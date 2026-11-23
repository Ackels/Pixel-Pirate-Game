using UnityEngine;

namespace Pixel.Village
{
    /// <summary>De tempos em tempos, desembarca um visitante nas docas, se houver um Mercado.</summary>
    public class VisitorSpawner : MonoBehaviour
    {
        public float interval = 10f;
        public int maxVisitors = 6;
        public Vector2 spawnPoint = new Vector2(31.5f, 1f);
        public Sprite[] down, up, left, right;

        static readonly Color[] Tints =
        {
            Color.white, new Color(0.85f, 0.9f, 1f), new Color(1f, 0.9f, 0.8f), new Color(0.9f, 1f, 0.9f),
        };

        float timer;

        void Start() => timer = interval * 0.5f; // o primeiro chega mais rápido

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0) return;
            timer = interval;

            var vm = VillageManager.Instance;
            if (vm == null || transform.childCount >= maxVisitors) return;
            var market = vm.FindNearest(BuildingKind.Market, spawnPoint);
            if (market == null) return;

            var go = new GameObject("Visitante");
            go.transform.SetParent(transform, false);
            go.transform.position = spawnPoint;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = up != null && up.Length > 0 ? up[0] : null;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            sr.color = Tints[Random.Range(0, Tints.Length)];
            var v = go.AddComponent<Visitor>();
            v.down = down; v.up = up; v.left = left; v.right = right;
            v.speed = Random.Range(2.2f, 3f);
            v.Init(market, spawnPoint);
        }
    }
}
