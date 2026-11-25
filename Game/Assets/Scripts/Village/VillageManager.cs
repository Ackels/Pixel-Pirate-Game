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
        [Tooltip("Sombra macia posta embaixo das construções.")]
        public Sprite shadowSprite;

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

        // ---------- Natureza (árvores, pedras...): ocupa a grade até ser limpa com Demolir ----------
        readonly Dictionary<Vector2Int, NatureObject> nature = new Dictionary<Vector2Int, NatureObject>();
        readonly List<NatureObject> natureAll = new List<NatureObject>();
        readonly HashSet<int> clearedNature = new HashSet<int>();
        /// <summary>Ids das peças de natureza já limpas (vão no save).</summary>
        public IEnumerable<int> ClearedNature => clearedNature;

        public void RegisterNature(NatureObject n)
        {
            if (n == null) return;
            if (n.blocks && clearedNature.Contains(n.id)) { Destroy(n.gameObject); return; }
            natureAll.Add(n);
            foreach (var c in Cells(n)) if (!nature.ContainsKey(c) || n.blocks) nature[c] = n;
            // Já tem construção em cima (jogo carregado)? Some.
            foreach (var c in Cells(n)) if (occupied.ContainsKey(c)) { RemoveNature(n, false); return; }
        }

        static IEnumerable<Vector2Int> Cells(NatureObject n)
        {
            for (int x = 0; x < n.size.x; x++)
                for (int y = 0; y < n.size.y; y++)
                    yield return new Vector2Int(n.cell.x + x, n.cell.y + y);
        }

        /// <summary>Natureza que bloqueia nesta célula (ou desenhada embaixo do mouse).</summary>
        public NatureObject NatureAt(Vector2 world)
        {
            var cell = new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
            if (nature.TryGetValue(cell, out var n) && n != null && n.blocks) return n;
            NatureObject best = null;
            float bestY = float.MaxValue;
            foreach (var o in natureAll)
            {
                if (o == null || !o.blocks) continue;
                var sr = o.GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                var b = sr.bounds;
                if (b.Contains(new Vector3(world.x, world.y, b.center.z)) && o.transform.position.y < bestY) { best = o; bestY = o.transform.position.y; }
            }
            return best;
        }

        /// <summary>Limpa o terreno: a peça some com um "puf" e fica marcada no save.</summary>
        public void ClearNature(NatureObject n)
        {
            if (n == null) return;
            AudioManager.Play(n.kind == NatureObject.Kind.Rock ? Sfx.Build : Sfx.Swing);
            RemoveNature(n, true);
            Changed?.Invoke();
        }

        void RemoveNature(NatureObject n, bool animate)
        {
            if (n.blocks) clearedNature.Add(n.id);
            foreach (var c in Cells(n)) if (nature.TryGetValue(c, out var o) && o == n) nature.Remove(c);
            natureAll.Remove(n);
            if (animate) n.gameObject.AddComponent<VanishPuff>();
            else Destroy(n.gameObject);
        }

        /// <summary>Ao carregar: some com o que o jogador já tinha limpado.</summary>
        public void RestoreClearedNature(IEnumerable<int> ids)
        {
            clearedNature.Clear();
            if (ids != null) foreach (var id in ids) clearedNature.Add(id);
            foreach (var n in natureAll.ToArray())
                if (n != null && n.blocks && clearedNature.Contains(n.id)) RemoveNature(n, false);
        }

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
                if (nature.TryGetValue(c, out var nat) && nat != null && nat.blocks)
                { reason = $"Tem {(nat.kind == NatureObject.Kind.Rock ? "uma rocha" : nat.kind == NatureObject.Kind.Stump ? "um toco" : nat.kind == NatureObject.Kind.Bush ? "um arbusto" : nat.kind == NatureObject.Kind.Palm ? "um coqueiro" : "uma árvore")} aqui: use Demolir (bomba) para limpar o terreno."; return false; }
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
                if (col.GetComponentInParent<NatureObject>() != null) continue;   // natureza já foi vista pela grade
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
            // Sombra macia embaixo (dá "peso" à construção no chão)
            if (shadowSprite != null)
            {
                var sh = new GameObject("Sombra").AddComponent<SpriteRenderer>();
                sh.transform.SetParent(go.transform, false);
                sh.transform.localPosition = new Vector3(0, 0.15f, 0);
                sh.transform.localScale = new Vector3(def.footprint.x * 16f / 40f, 1.4f, 1f);
                sh.sprite = shadowSprite;
                sh.sortingOrder = -10;
            }
            // Casas e taverna acendem a luz da janela à noite
            if (def.kind == BuildingKind.House || def.kind == BuildingKind.Tavern)
                Pixel.Combat.Glow.Add(go.transform, new Vector2(0, def.footprint.y * 0.6f), Pixel.Combat.Glow.Candle, 2.6f + def.footprint.x * 0.4f, 0.9f);
            // Flores e capim embaixo somem; natureza grande (só em jogo carregado) também
            for (int x = 0; x < def.footprint.x; x++)
                for (int y = 0; y < def.footprint.y; y++)
                    if (nature.TryGetValue(new Vector2Int(origin.x + x, origin.y + y), out var n) && n != null) RemoveNature(n, false);
            foreach (var n in natureAll.ToArray())
                if (n != null && !n.blocks && n.cell.x >= origin.x && n.cell.x < origin.x + def.footprint.x && n.cell.y >= origin.y && n.cell.y < origin.y + def.footprint.y)
                    RemoveNature(n, false);

            for (int x = 0; x < def.footprint.x; x++)
                for (int y = 0; y < def.footprint.y; y++)
                    occupied[new Vector2Int(origin.x + x, origin.y + y)] = b;
            buildings.Add(b);
            Changed?.Invoke();
            BuildingsChanged?.Invoke();
            return b;
        }

        // ---------- Salvar / carregar ----------
        public IEnumerable<KeyValuePair<ItemDef, int>> Stock => stock;

        /// <summary>Apaga tudo (prédios, estoque) para carregar um jogo salvo.</summary>
        public void ClearForLoad(int gold)
        {
            foreach (var b in buildings) if (b != null) Destroy(b.gameObject);
            buildings.Clear();
            occupied.Clear();
            stock.Clear();
            Gold = gold;
            Changed?.Invoke();
            BuildingsChanged?.Invoke();
        }

        public void SetStock(ItemDef item, int count)
        {
            if (item == null) return;
            stock[item] = count;
            Changed?.Invoke();
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
