using System.Collections.Generic;
using UnityEngine;

namespace Pixel.Ship
{
    /// <summary>
    /// Desenha um ShipLayout no mundo (um sprite por bloco) e cria a colisão dos blocos sólidos.
    /// O canto inferior esquerdo da célula (0,0) é a posição deste objeto, então mover o objeto move o navio.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CompositeCollider2D))]
    public class ShipView : MonoBehaviour
    {
        [Tooltip("Navio que se move (inimigo): Kinematic. O do jogador fica parado: Static.")]
        public bool movable;
        [Tooltip("Canhões apontam para a esquerda (navio inimigo).")]
        public bool facingLeft;
        public Color sailTint = Color.white;

        ShipLayout layout;
        Rigidbody2D body;
        readonly Dictionary<Vector2Int, (ShipBlockDef def, int rot, GameObject go)> views = new Dictionary<Vector2Int, (ShipBlockDef, int, GameObject)>();

        public ShipLayout Layout => layout;
        public Vector2 origin => transform.position;
        public float WaterlineY => origin.y + ShipLayout.WaterRow + 0.5f;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = movable ? RigidbodyType2D.Kinematic : RigidbodyType2D.Static;
            GetComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Polygons;
        }

        public void MoveTo(Vector2 pos)
        {
            if (body != null && movable) body.MovePosition(pos);
            else transform.position = pos;
        }

        public Vector2 CellCenter(Vector2Int c) => origin + (Vector2)c + new Vector2(0.5f, 0.5f);

        public Vector2Int WorldToCell(Vector2 w) =>
            new Vector2Int(Mathf.FloorToInt(w.x - origin.x), Mathf.FloorToInt(w.y - origin.y));

        public void Bind(ShipLayout l)
        {
            if (layout != null) layout.Changed -= Sync;
            layout = l;
            layout.Changed += Sync;
            Sync();
        }

        void OnDestroy()
        {
            if (layout != null) layout.Changed -= Sync;
        }

        void Sync()
        {
            var stale = new List<Vector2Int>();
            foreach (var kv in views)
                if (layout.Get(kv.Key) != kv.Value.def || layout.Rot(kv.Key) != kv.Value.rot) stale.Add(kv.Key);
            foreach (var c in stale)
            {
                DestroyImmediate(views[c].go);
                views.Remove(c);
            }
            foreach (var kv in layout.Blocks)
            {
                if (views.ContainsKey(kv.Key)) continue;
                var def = kv.Value;
                int rot = layout.Rot(kv.Key);
                views[kv.Key] = (def, rot, CreateBlock(def, kv.Key, rot));
            }
            RefreshConnected();
        }

        GameObject CreateBlock(ShipBlockDef def, Vector2Int c, int rot)
        {
            var go = new GameObject($"{def.displayName} {c.x},{c.y}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = (Vector2)c + new Vector2(0.5f, 0.5f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = def.sprite;
            sr.sortingOrder = def.sortingOrder;
            var (fx, fy) = ShipShapes.Flips(def.shape, rot);
            sr.flipX = fx; sr.flipY = fy;
            if (def.role == BlockRole.Sail) sr.color = sailTint;
            if (def.role == BlockRole.Cannon) sr.flipX = facingLeft;

            if (def.solid)
            {
                Collider2D col;
                if (def.shape == BlockShape.Full)
                {
                    var box = go.AddComponent<BoxCollider2D>();
                    box.size = Vector2.one;
                    col = box;
                }
                else
                {
                    var poly = go.AddComponent<PolygonCollider2D>();
                    poly.SetPath(0, ShipShapes.Polygon(def.shape, rot));
                    col = poly;
                }
                col.compositeOperation = Collider2D.CompositeOperation.Merge;
            }
            else if (def.role == BlockRole.Platform)
            {
                // Piso vazado: dá pra pisar em cima e pular de baixo para cima (S desce)
                var top = new GameObject("PisoVazado");
                top.transform.SetParent(go.transform, false);
                var box = top.AddComponent<BoxCollider2D>();
                box.size = new Vector2(1f, 0.12f);
                box.offset = new Vector2(0f, 0.44f);
                box.usedByEffector = true;
                var eff = top.AddComponent<PlatformEffector2D>();
                eff.useOneWay = true;
                eff.surfaceArc = 160f;
                top.AddComponent<ShipPlatform>();
            }
            else if (def.role == BlockRole.Ladder)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(0.6f, 1f);
                go.AddComponent<ShipLadder>();
            }
            return go;
        }

        /// <summary>
        /// Uma célula só tem um bloco: um barril, escada ou canhão no meio de uma parede deixaria um "buraco" com o céu.
        /// Se houver parede lisa ao lado, desenha a mesma parede atrás.
        /// </summary>
        readonly HashSet<Vector2Int> backed = new HashSet<Vector2Int>();
        Sprite plainWall;
        int plainWallOrder;

        static bool IsWallRole(ShipBlockDef d) => d != null && (d.role == BlockRole.Wall || d.role == BlockRole.Cabin);
        static bool NeedsBack(ShipBlockDef d) => d != null &&
            (d.role == BlockRole.Decor || d.role == BlockRole.Ladder || d.role == BlockRole.Cannon || d.role == BlockRole.Platform);

        /// <summary>
        /// Quais células sem parede ganham a parede lisa desenhada atrás: piso, escada, enfeite ou canhão ao lado
        /// de uma parede, ou em cima de uma parede (ou de outra célula que já ganhou). De baixo para cima.
        /// </summary>
        void ComputeBackWalls()
        {
            backed.Clear();
            plainWall = null;
            foreach (var kv in layout.Blocks)
                if (kv.Value.role == BlockRole.Wall && (plainWall == null || kv.Value.Family == "parede"))
                { plainWall = kv.Value.sprite; plainWallOrder = kv.Value.sortingOrder; if (kv.Value.Family == "parede") break; }
            if (plainWall == null) return;
            var cells = new List<Vector2Int>();
            foreach (var kv in layout.Blocks) if (NeedsBack(kv.Value)) cells.Add(kv.Key);
            cells.Sort((p, q) => p.y.CompareTo(q.y));
            foreach (var c in cells)
            {
                var below = c + Vector2Int.down;
                // Em cima de um piso começa outro andar (convés aberto): a parede de baixo não sobe por ele
                var belowDef = layout.Get(below);
                bool fromBelow = IsWallRole(belowDef) || (backed.Contains(below) && belowDef.role != BlockRole.Platform);
                if (IsWallRole(layout.Get(c + Vector2Int.left)) || IsWallRole(layout.Get(c + Vector2Int.right)) || fromBelow)
                    backed.Add(c);
            }
        }

        void UpdateBackWall(GameObject go, ShipBlockDef def, Vector2Int c)
        {
            var t = go.transform.Find("ParedeAtras");
            if (!backed.Contains(c)) { if (t != null) Destroy(t.gameObject); return; }
            var back = t != null ? t.GetComponent<SpriteRenderer>() : new GameObject("ParedeAtras").AddComponent<SpriteRenderer>();
            back.transform.SetParent(go.transform, false);
            back.transform.localPosition = Vector3.zero;
            back.flipX = back.flipY = false;
            back.sprite = plainWall;
            back.sortingOrder = plainWallOrder;
        }

        bool IsMast(Vector2Int c) { var d = layout.Get(c); return d != null && d.role == BlockRole.Mast; }

        /// <summary>Vela ao lado de um mastro: desenha a aba na célula do mastro (pano até o mastro e a verga atravessando).</summary>
        void UpdateSailTabs(GameObject go, ShipBlockDef def, Vector2Int c)
        {
            foreach (var (dir, name, flip) in new[] { (Vector2Int.left, "AbaEsquerda", false), (Vector2Int.right, "AbaDireita", true) })
            {
                var t = go.transform.Find(name);
                bool want = def.role == BlockRole.Sail && def.tabSprites != null && def.tabSprites.Length >= 4 && IsMast(c + dir);
                if (!want) { if (t != null) Destroy(t.gameObject); continue; }
                int m = (layout.Get(c + Vector2Int.up) == def ? 1 : 0) | (layout.Get(c + Vector2Int.down) == def ? 2 : 0);
                var sr = t != null ? t.GetComponent<SpriteRenderer>() : new GameObject(name).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(go.transform, false);
                sr.transform.localPosition = (Vector2)dir;
                sr.sprite = def.tabSprites[m];
                sr.flipX = flip;
                sr.sortingOrder = def.sortingOrder;
                sr.color = sailTint;
            }
        }

        /// <summary>Escada atravessando um piso vazado: o piso continua na célula da escada (dá pra andar por cima e descer pela escada).</summary>
        void UpdateLadderFloor(GameObject go, ShipBlockDef def, Vector2Int c)
        {
            ShipBlockDef floor = null;
            if (def.role == BlockRole.Ladder)
                foreach (var n in new[] { Vector2Int.left, Vector2Int.right })
                {
                    var d = layout.Get(c + n);
                    if (d != null && d.role == BlockRole.Platform) { floor = d; break; }
                }
            var t = go.transform.Find("PisoDaEscada");
            if (floor == null) { if (t != null) Destroy(t.gameObject); return; }
            if (t != null) return;
            var top = new GameObject("PisoDaEscada");
            top.transform.SetParent(go.transform, false);
            var sr = top.AddComponent<SpriteRenderer>();
            sr.sprite = floor.sprite;
            sr.sortingOrder = floor.sortingOrder;
            var box = top.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 0.12f);
            box.offset = new Vector2(0f, 0.44f);
            box.usedByEffector = true;
            var eff = top.AddComponent<PlatformEffector2D>();
            eff.useOneWay = true;
            eff.surfaceArc = 160f;
            top.AddComponent<ShipPlatform>();
        }

        /// <summary>Peças que mudam com os vizinhos: velas (16 peças), casco com friso quando o topo fica exposto, etc.</summary>
        void RefreshConnected()
        {
            ComputeBackWalls();
            foreach (var kv in views)
            {
                var def = kv.Value.def;
                var c = kv.Key;
                UpdateBackWall(kv.Value.go, def, c);
                UpdateSailTabs(kv.Value.go, def, c);
                UpdateLadderFloor(kv.Value.go, def, c);
                Sprite s = def.sprite;
                switch (def.connect)
                {
                    case ConnectMode.Vertical:
                    {
                        bool above = layout.Get(c + Vector2Int.up) == def, below = layout.Get(c + Vector2Int.down) == def;
                        s = !above && !below ? def.spriteSingle : !above ? def.spriteTop : !below ? def.spriteBottom : def.sprite;
                        break;
                    }
                    case ConnectMode.Sail16:
                    {
                        // Mastro ao lado conta como "emendado": a beirada da vela some e a aba de ligação completa até o mastro
                        int m = (layout.Get(c + Vector2Int.up) == def ? 1 : 0) | (layout.Get(c + Vector2Int.down) == def ? 2 : 0)
                              | (layout.Get(c + Vector2Int.left) == def || IsMast(c + Vector2Int.left) ? 4 : 0)
                              | (layout.Get(c + Vector2Int.right) == def || IsMast(c + Vector2Int.right) ? 8 : 0);
                        if (def.connectSprites != null && m < def.connectSprites.Length) s = def.connectSprites[m];
                        break;
                    }
                    case ConnectMode.ExposedTop:
                    {
                        var above = layout.Get(c + Vector2Int.up);
                        bool covered = above != null && above.solid;
                        s = covered ? def.sprite : def.spriteTop;
                        break;
                    }
                    default:
                        continue;
                }
                var sr = kv.Value.go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = s != null ? s : def.sprite;
            }
        }

        // ---------- Consultas úteis para o combate ----------
        public bool TryGetBounds(out int minX, out int maxX)
        {
            minX = int.MaxValue; maxX = int.MinValue;
            foreach (var c in layout.Blocks.Keys) { minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x); }
            return layout.Blocks.Count > 0;
        }

        public float LeftEdge => TryGetBounds(out int a, out _) ? origin.x + a : origin.x;
        public float RightEdge => TryGetBounds(out _, out int b) ? origin.x + b + 1 : origin.x;
        public float CenterX => (LeftEdge + RightEdge) / 2f;
        public bool ContainsX(float x) => x >= LeftEdge - 0.3f && x <= RightEdge + 0.3f;

        /// <summary>Topo do piso sólido na coluna mais à esquerda/direita (para a prancha de abordagem).</summary>
        public Vector2 EdgeDeck(bool right)
        {
            TryGetBounds(out int minX, out int maxX);
            int step = right ? -1 : 1;
            for (int x = right ? maxX : minX; right ? x >= minX : x <= maxX; x += step)
            {
                int top = int.MinValue;
                foreach (var kv in layout.Blocks)
                    if (kv.Key.x == x && kv.Value.Standable) top = Mathf.Max(top, kv.Key.y);
                if (top != int.MinValue)
                    return origin + new Vector2(right ? x + 0.9f : x + 0.1f, top + 1f);
            }
            return origin + new Vector2(right ? maxX + 1 : minX, ShipLayout.WaterRow + 1);
        }

        public List<Vector2> CannonMuzzles()
        {
            var list = new List<Vector2>();
            foreach (var kv in layout.Blocks)
                if (kv.Value.role == BlockRole.Cannon)
                    list.Add(CellCenter(kv.Key) + new Vector2(facingLeft ? -0.5f : 0.5f, 0.05f));
            return list;
        }

        public Vector2 RandomBlockPoint()
        {
            int i = Random.Range(0, layout.Blocks.Count), n = 0;
            foreach (var c in layout.Blocks.Keys) { if (n++ == i) return CellCenter(c); }
            return origin;
        }

        /// <summary>Ponto em cima do bloco sólido mais alto, perto do meio do navio (para alguém nascer).</summary>
        public Vector2 SpawnPoint(float offsetX = 0f)
        {
            Vector2Int best = new Vector2Int(-1, -1);
            float bestScore = float.MaxValue;
            float mid = TryGetBounds(out int a, out int b) ? (a + b) / 2f + offsetX : ShipLayout.Width / 2f;
            foreach (var kv in layout.Blocks)
            {
                // nasce em cima de um casco cheio ou de um piso, de preferência no convés mais alto
                bool floor = (kv.Value.solid && kv.Value.shape == BlockShape.Full) || kv.Value.role == BlockRole.Platform;
                if (!floor) continue;
                var above = layout.Get(kv.Key + Vector2Int.up);
                if (above != null && (above.solid || above.role == BlockRole.Platform)) continue;
                float score = Mathf.Abs(kv.Key.x - mid) - kv.Key.y * 1.5f;
                if (score < bestScore) { bestScore = score; best = kv.Key; }
            }
            if (best.x < 0) return CellCenter(new Vector2Int(ShipLayout.Width / 2, ShipLayout.WaterRow + 2));
            return origin + new Vector2(best.x + 0.5f, best.y + 1.02f);
        }
    }
}
