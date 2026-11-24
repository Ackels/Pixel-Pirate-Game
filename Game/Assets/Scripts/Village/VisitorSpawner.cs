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
        [Tooltip("Visuais possíveis (se vazio, usa os quadros acima com cores variadas).")]
        public CharacterLook[] looks;

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
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            var v = go.AddComponent<Visitor>();
            var look = looks != null && looks.Length > 0 ? looks[Random.Range(0, looks.Length)] : null;
            if (look != null) { v.down = look.down; v.up = look.up; v.left = look.left; v.right = look.right; }
            else
            {
                v.down = down; v.up = up; v.left = left; v.right = right;
                sr.color = Tints[Random.Range(0, Tints.Length)];
            }
            sr.sprite = v.up != null && v.up.Length > 0 ? v.up[0] : null;
            v.speed = Random.Range(2.2f, 3f);
            v.Init(market, spawnPoint);
        }
    }
}
