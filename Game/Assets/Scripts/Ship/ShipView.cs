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
        readonly Dictionary<Vector2Int, (ShipBlockDef def, GameObject go)> views = new Dictionary<Vector2Int, (ShipBlockDef, GameObject)>();

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
                if (layout.Get(kv.Key) != kv.Value.def) stale.Add(kv.Key);
            foreach (var c in stale)
            {
                DestroyImmediate(views[c].go);
                views.Remove(c);
            }
            foreach (var kv in layout.Blocks)
            {
                if (views.ContainsKey(kv.Key)) continue;
                var def = kv.Value;
                var go = new GameObject($"{def.displayName} {kv.Key.x},{kv.Key.y}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = (Vector2)kv.Key + new Vector2(0.5f, 0.5f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = def.sprite;
                sr.sortingOrder = def.sortingOrder;
                if (def.role == BlockRole.Sail) sr.color = sailTint;
                if (def.role == BlockRole.Cannon) sr.flipX = facingLeft;
                if (def.solid)
                {
                    var box = go.AddComponent<BoxCollider2D>();
                    box.size = Vector2.one;
                    box.compositeOperation = Collider2D.CompositeOperation.Merge;
                }
                views[kv.Key] = (def, go);
            }
            RefreshConnected();
        }

        /// <summary>Blocos que se encaixam na vertical (velas): topo, meio, base ou peça única, conforme os vizinhos.</summary>
        void RefreshConnected()
        {
            foreach (var kv in views)
            {
                var def = kv.Value.def;
                if (def.spriteTop == null && def.spriteBottom == null && def.spriteSingle == null) continue;
                bool above = layout.Get(kv.Key + Vector2Int.up) == def;
                bool below = layout.Get(kv.Key + Vector2Int.down) == def;
                Sprite s = !above && !below ? def.spriteSingle : !above ? def.spriteTop : !below ? def.spriteBottom : def.sprite;
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
                    if (kv.Key.x == x && kv.Value.solid) top = Mathf.Max(top, kv.Key.y);
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
                if (!kv.Value.solid) continue;
                var above = layout.Get(kv.Key + Vector2Int.up);
                if (above != null && above.solid) continue;
                float score = Mathf.Abs(kv.Key.x - mid) - kv.Key.y * 0.5f;
                if (score < bestScore) { bestScore = score; best = kv.Key; }
            }
            if (best.x < 0) return CellCenter(new Vector2Int(ShipLayout.Width / 2, ShipLayout.WaterRow + 2));
            return origin + new Vector2(best.x + 0.5f, best.y + 1.02f);
        }
    }
}
