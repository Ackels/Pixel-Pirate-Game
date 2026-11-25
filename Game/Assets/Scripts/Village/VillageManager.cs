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
        [Header("Caminhos (o jogador pode construir)")]
        [Tooltip("Peças do caminho por máscara de vizinhos (1=N 2=NE 4=L 8=SE 16=S 32=SO 64=O 128=NO).")]
        public int[] pathMasks;
        public TileBase[] pathTiles;
        public int pathCost = 1;
        [Tooltip("Bonecos andam mais rápido em cima de caminhos e docas.")]
        public float pathSpeed = 1.4f;
        [Header("Começo do jogo")]
        public int startingWood = 15, startingStone = 8;

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
            // Árvore vira madeira; pedra vira pedra
            var (mat, amount) = n.kind switch
            {
                NatureObject.Kind.Rock => (Item("Pedra"), 2 + n.size.x * n.size.y),
                NatureObject.Kind.Tree => (Item("Madeira"), 1 + n.size.x * n.size.y),
                NatureObject.Kind.Palm => (Item("Madeira"), 2),
                NatureObject.Kind.Stump => (Item("Madeira"), 2),
                NatureObject.Kind.Bush => (Item("Madeira"), 1),
                _ => ((ItemDef)null, 0),
            };
            if (mat != null && amount > 0)
            {
                AddStock(mat, amount);
                FloatingText.Spawn(n.transform.position + Vector3.up * 1.6f, $"+{amount} {mat.displayName}", mat.textColor);
            }
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

        void Start()
        {
            AddStock(Item("Madeira"), startingWood);
            AddStock(Item("Pedra"), startingStone);
        }

        /// <summary>Item do estoque pelo nome (ex.: "Madeira").</summary>
        public ItemDef Item(string name)
        {
            if (items == null) return null;
            foreach (var i in items) if (i != null && (i.name == name || i.displayName == name)) return i;
            return null;
        }

        // ---------- Custos (ouro + materiais) ----------
        /// <summary>Dá para pagar? Se não, diz o que falta.</summary>
        public bool CanAfford(BuildingDef def, out string missing)
        {
            var parts = new List<string>();
            if (Gold < def.cost) parts.Add($"{def.cost - Gold} ouro");
            if (def.materials != null)
                foreach (var m in def.materials)
                    if (m.item != null && GetStock(m.item) < m.amount) parts.Add($"{m.amount - GetStock(m.item)} {m.item.displayName}");
            missing = parts.Count > 0 ? "Falta " + string.Join(", ", parts) + "." : null;
            return parts.Count == 0;
        }

        public bool TryPay(BuildingDef def)
        {
            if (!CanAfford(def, out _)) return false;
            Gold -= def.cost;
            if (def.materials != null)
                foreach (var m in def.materials) if (m.item != null) stock[m.item] = GetStock(m.item) - m.amount;
            Changed?.Invoke();
            return true;
        }

        /// <summary>"40 ouro, 6 Madeira, 4 Pedra".</summary>
        public static string CostText(BuildingDef def, float fraction = 1f)
        {
            var parts = new List<string> { $"{Mathf.RoundToInt(def.cost * fraction)} ouro" };
            if (def.materials != null)
                foreach (var m in def.materials)
                    if (m.item != null && Mathf.RoundToInt(m.amount * fraction) > 0) parts.Add($"{Mathf.RoundToInt(m.amount * fraction)} {m.item.displayName}");
            return string.Join(", ", parts);
        }

        // ---------- Beleza (decorações) ----------
        /// <summary>Soma da beleza das decorações da vila.</summary>
        public int Beauty { get { int b = 0; foreach (var x in buildings) if (x != null) b += x.def.beauty; return b; } }
        /// <summary>Moral extra por dia para cada tripulante (1 a cada 2 de beleza, até +10).</summary>
        public int BeautyMorale => Mathf.Min(10, Beauty / 2);

        // ---------- Caminhos ----------
        readonly HashSet<Vector2Int> playerPaths = new HashSet<Vector2Int>();
        /// <summary>Caminhos feitos pelo jogador (vão no save).</summary>
        public IEnumerable<Vector2Int> PlayerPaths => playerPaths;

        bool IsPathTile(TileBase t)
        {
            if (t == null || pathTiles == null) return false;
            foreach (var p in pathTiles) if (p == t) return true;
            return false;
        }

        bool IsPathCell(Vector2Int c) => detailMap != null && IsPathTile(detailMap.GetTile((Vector3Int)c));

        /// <summary>Tem caminho (ou doca) embaixo deste ponto? Bonecos andam mais rápido.</summary>
        public bool OnPath(Vector2 world) =>
            detailMap != null && detailMap.HasTile(new Vector3Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y), 0));

        public float SpeedAt(Vector2 world) => OnPath(world) ? pathSpeed : 1f;

        public bool CanPlacePath(Vector2Int c, out string reason)
        {
            reason = null;
            if (c.x < 1 || c.y < 1 || c.x >= mapWidth - 1 || c.y >= mapHeight - 1) { reason = "Fora da vila."; return false; }
            if (waterMap != null && waterMap.HasTile((Vector3Int)c)) { reason = "Não dá para fazer caminho na água."; return false; }
            if (detailMap != null && detailMap.HasTile((Vector3Int)c)) { reason = "Já tem caminho aqui."; return false; }
            if (occupied.ContainsKey(c)) { reason = "Tem uma construção aqui."; return false; }
            if (nature.TryGetValue(c, out var nat) && nat != null && nat.blocks) { reason = $"Tem {nat.DisplayName} aqui: limpe com Demolir."; return false; }
            if (Gold < pathCost) { reason = $"Falta ouro ({pathCost} por bloco)."; return false; }
            return true;
        }

        public bool PlacePath(Vector2Int c, bool pay = true)
        {
            if (detailMap == null || pathTiles == null || pathTiles.Length == 0) return false;
            if (pay && !TrySpendGold(pathCost)) return false;
            playerPaths.Add(c);
            detailMap.SetTile((Vector3Int)c, pathTiles[0]);
            // Flores e capim embaixo somem
            foreach (var n in natureAll.ToArray())
                if (n != null && !n.blocks && n.cell == c) RemoveNature(n, false);
            RefreshPathsAround(c);
            return true;
        }

        /// <summary>Tira um caminho feito pelo jogador (os da ilha original ficam).</summary>
        public bool RemovePath(Vector2Int c)
        {
            if (!playerPaths.Remove(c)) return false;
            detailMap.SetTile((Vector3Int)c, null);
            RefreshPathsAround(c);
            Gold += pathCost / 2;
            Changed?.Invoke();
            return true;
        }

        public bool IsPlayerPath(Vector2Int c) => playerPaths.Contains(c);

        /// <summary>Refaz a borda das peças de caminho nesta célula e nas 8 vizinhas.</summary>
        void RefreshPathsAround(Vector2Int c)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var n = new Vector2Int(c.x + dx, c.y + dy);
                    if (IsPathCell(n)) detailMap.SetTile((Vector3Int)n, PathTileFor(PathMask(n)));
                }
        }

        int PathMask(Vector2Int c)
        {
            bool P(int x, int y) => IsPathCell(new Vector2Int(x, y));
            int x0 = c.x, y0 = c.y, m = 0;
            bool N = P(x0, y0 + 1), E = P(x0 + 1, y0), S = P(x0, y0 - 1), W = P(x0 - 1, y0);
            if (N) m |= 1;
            if (N && E && P(x0 + 1, y0 + 1)) m |= 2;
            if (E) m |= 4;
            if (S && E && P(x0 + 1, y0 - 1)) m |= 8;
            if (S) m |= 16;
            if (S && W && P(x0 - 1, y0 - 1)) m |= 32;
            if (W) m |= 64;
            if (N && W && P(x0 - 1, y0 + 1)) m |= 128;
            return m;
        }

        TileBase PathTileFor(int mask)
        {
            int best = 0, bestScore = int.MaxValue;
            for (int i = 0; i < pathMasks.Length; i++)
            {
                int diff = pathMasks[i] ^ mask;
                int score = Bits(diff & 0b01010101) * 10 + Bits(diff & 0b10101010);
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return pathTiles[best];
        }

        static int Bits(int v) { int c = 0; while (v != 0) { c += v & 1; v >>= 1; } return c; }

        /// <summary>Ao carregar: refaz os caminhos do jogador.</summary>
        public void RestorePaths(IEnumerable<Vector2Int> cells)
        {
            if (cells == null) return;
            foreach (var c in cells) if (detailMap != null && !detailMap.HasTile((Vector3Int)c)) PlacePath(c, false);
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
                col.size = def.flat ? new Vector2(def.footprint.x - 0.2f, def.footprint.y - 0.2f)
                                    : new Vector2(def.footprint.x - 0.2f, Mathf.Max(0.6f, def.footprint.y - 0.8f));
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
            if (def.lightAtNight)
                Pixel.Combat.Glow.Add(go.transform, new Vector2(0, 1.3f), Pixel.Combat.Glow.Candle, 4f, 1.1f);
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
            foreach (var c in new List<Vector2Int>(playerPaths)) { playerPaths.Remove(c); detailMap?.SetTile((Vector3Int)c, null); RefreshPathsAround(c); }
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
            if (b.def.materials != null)
                foreach (var m in b.def.materials)
                    if (m.item != null) stock[m.item] = GetStock(m.item) + Mathf.RoundToInt(m.amount * demolishRefund);
            foreach (var w in b.Workers) w.workplace = null;
            b.Workers.Clear();
            Destroy(b.gameObject);
            Changed?.Invoke();
            BuildingsChanged?.Invoke();
        }
    }
}
