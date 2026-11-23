using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Pixel.Village
{
    /// <summary>
    /// O "cérebro" da vila: ouro, estoque de itens, construções e onde é permitido construir.
    /// </summary>
    public class VillageManager : MonoBehaviour
    {
        public static VillageManager Instance { get; private set; }

        [Header("Economia")]
        public int startingGold = 180;
        [Tooltip("Itens que aparecem no HUD, na ordem.")]
        public ItemDef[] items;
        [Tooltip("Construções disponíveis na barra de construção.")]
        public BuildingDef[] buildable;
        [Range(0f, 1f)] public float demolishRefund = 0.5f;

        [Header("Mapa")]
        public int mapWidth = 64;
        public int mapHeight = 40;
        public Tilemap waterMap;
        public Tilemap detailMap;   // caminhos e docas: não dá pra construir em cima

        public int Gold { get; private set; }
        public IReadOnlyList<Building> Buildings => buildings;
        public event Action Changed;
        /// <summary>Disparado quando uma construção é colocada ou demolida.</summary>
        public event Action BuildingsChanged;

        /// <summary>Quanto deste item o Mercado deve guardar para a tripulação.</summary>
        public int Reserve(ItemDef item)
        {
            int crewCount = CrewManager.Instance != null ? CrewManager.Instance.Crew.Count : 0;
            return item.reservePerCrew * crewCount;
        }

        readonly Dictionary<ItemDef, int> stock = new Dictionary<ItemDef, int>();
        readonly Dictionary<Vector2Int, Building> occupied = new Dictionary<Vector2Int, Building>();
        readonly List<Building> buildings = new List<Building>();
        readonly Collider2D[] overlapResults = new Collider2D[16];

        void Awake()
        {
            Instance = this;
            Gold = startingGold;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- Ouro e estoque ----------
        public int GetStock(ItemDef item) => item != null && stock.TryGetValue(item, out int n) ? n : 0;

        public void AddStock(ItemDef item, int amount)
        {
            if (item == null || amount == 0) return;
            stock[item] = GetStock(item) + amount;
            Changed?.Invoke();
        }

        public bool TryTakeStock(ItemDef item, int amount)
        {
            if (GetStock(item) < amount) return false;
            stock[item] -= amount;
            Changed?.Invoke();
            return true;
        }

        public void AddGold(int amount)
        {
            Gold += amount;
            Changed?.Invoke();
        }

        public bool TrySpendGold(int amount)
        {
            if (Gold < amount) return false;
            Gold -= amount;
            Changed?.Invoke();
            return true;
        }

        public int HousingCapacity
        {
            get
            {
                int total = 0;
                foreach (var b in buildings) if (b.def.kind == BuildingKind.House) total += b.def.housing;
                return total;
            }
        }

        public Building FindNearest(BuildingKind kind, Vector2 from)
        {
            Building best = null;
            float bestDist = float.MaxValue;
            foreach (var b in buildings)
            {
                if (b.def.kind != kind) continue;
                float d = ((Vector2)b.transform.position - from).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = b; }
            }
            return best;
        }

        // ---------- Construção ----------
        /// <summary>Posição no mundo (meio da base) de uma construção com esse canto inferior esquerdo.</summary>
        public static Vector3 WorldPosition(BuildingDef def, Vector2Int origin) =>
            new Vector3(origin.x + def.footprint.x / 2f, origin.y, 0);

        public bool CanPlace(BuildingDef def, Vector2Int origin, out string reason)
        {
            for (int x = 0; x < def.footprint.x; x++)
            for (int y = 0; y < def.footprint.y; y++)
            {
                var c = new Vector2Int(origin.x + x, origin.y + y);
                if (c.x < 1 || c.y < 1 || c.x >= mapWidth - 1 || c.y >= mapHeight - 1) { reason = "Fora da vila."; return false; }
                if (waterMap != null && waterMap.HasTile((Vector3Int)c)) { reason = "Não dá pra construir na água."; return false; }
                if (detailMap != null && detailMap.HasTile((Vector3Int)c)) { reason = "Não construa em cima do caminho."; return false; }
                if (occupied.ContainsKey(c)) { reason = "Já tem uma construção aqui."; return false; }
            }

            if (def.requiresWater && waterMap != null)
            {
                bool touchesWater = false;
                for (int x = -1; x <= def.footprint.x && !touchesWater; x++)
                for (int y = -1; y <= def.footprint.y && !touchesWater; y++)
                {
                    bool inside = x >= 0 && y >= 0 && x < def.footprint.x && y < def.footprint.y;
                    if (!inside && waterMap.HasTile(new Vector3Int(origin.x + x, origin.y + y, 0))) touchesWater = true;
                }
                if (!touchesWater) { reason = "Precisa ficar encostada na água."; return false; }
            }

            // Objetos soltos no terreno (coqueiros, barris...). O capitão e visitantes não contam.
            var center = new Vector2(origin.x + def.footprint.x / 2f, origin.y + def.footprint.y / 2f);
            var filter = new ContactFilter2D { useTriggers = false };
            int hits = Physics2D.OverlapBox(center, (Vector2)def.footprint * 0.9f, 0f, filter, overlapResults);
            for (int i = 0; i < hits; i++)
            {
                var col = overlapResults[i];
                if (col.attachedRigidbody != null && col.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) continue;
                if (col.GetComponentInParent<Tilemap>() != null) continue;
                reason = col.name == "Coqueiro" ? "Tem um coqueiro no caminho." : "Tem algo no caminho.";
                return false;
            }

            reason = null;
            return true;
        }

        public Building Place(BuildingDef def, Vector2Int origin)
        {
            var go = new GameObject(def.displayName);
            go.transform.position = WorldPosition(def, origin);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = def.sprite;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            if (def.blocksMovement)
            {
                var col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(def.footprint.x - 0.2f, Mathf.Max(0.6f, def.footprint.y - 0.8f));
                col.offset = new Vector2(0, col.size.y / 2f);
            }
            var b = go.AddComponent<Building>();
            b.def = def;
            b.origin = origin;

            for (int x = 0; x < def.footprint.x; x++)
                for (int y = 0; y < def.footprint.y; y++)
                    occupied[new Vector2Int(origin.x + x, origin.y + y)] = b;
            buildings.Add(b);
            Changed?.Invoke();
            BuildingsChanged?.Invoke();
            return b;
        }

        public Building BuildingAt(Vector2 world)
        {
            var cell = new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
            if (occupied.TryGetValue(cell, out var b)) return b;
            // Também aceita clicar no telhado (parte de cima do desenho).
            foreach (var bb in buildings)
            {
                var r = bb.GetComponent<SpriteRenderer>().bounds;
                if (r.Contains(new Vector3(world.x, world.y, r.center.z))) return bb;
            }
            return null;
        }

        public void Demolish(Building b)
        {
            if (b == null) return;
            var keys = new List<Vector2Int>();
            foreach (var kv in occupied) if (kv.Value == b) keys.Add(kv.Key);
            foreach (var k in keys) occupied.Remove(k);
            buildings.Remove(b);
            Gold += Mathf.RoundToInt(b.def.cost * demolishRefund);
            foreach (var w in b.Workers) w.workplace = null;
            b.Workers.Clear();
            Destroy(b.gameObject);
            Changed?.Invoke();
            BuildingsChanged?.Invoke();
        }
    }
}
