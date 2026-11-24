using System.Collections.Generic;
using UnityEngine;

namespace Pixel.Ship
{
    /// <summary>Atributos do navio calculados a partir dos blocos.</summary>
    public struct ShipStats
    {
        public int blocks, hp, sails, bunks, cannons, value;
        public float speedKnots;
        public int MaxCrew => 2 + bunks;
    }

    /// <summary>
    /// O desenho do navio: uma grade (visão lateral) com um bloco por célula.
    /// Guarda as regras de construção (tudo conectado, mastro precisa de apoio, vela presa no mastro...).
    /// </summary>
    public class ShipLayout
    {
        public const int Width = 30;
        public const int Height = 14;
        /// <summary>A linha d'água passa no meio desta linha: ela e as de baixo ficam submersas.
        /// (Era 2 até 24/11/2026; saves antigos ganham uma quilha, veja MigrateLegacy.)</summary>
        public const int WaterRow = 1;

        readonly Dictionary<Vector2Int, ShipBlockDef> blocks = new Dictionary<Vector2Int, ShipBlockDef>();
        readonly Dictionary<Vector2Int, int> rots = new Dictionary<Vector2Int, int>();
        /// <summary>Orientação da peça (rampa, curvo, meio bloco). Veja ShipShapes.</summary>
        public int Rot(Vector2Int c) => rots.TryGetValue(c, out var r) ? r : 0;
        public IReadOnlyDictionary<Vector2Int, ShipBlockDef> Blocks => blocks;
        public event System.Action Changed;

        static readonly Vector2Int[] Neighbors = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        public ShipBlockDef Get(Vector2Int c) => blocks.TryGetValue(c, out var d) ? d : null;
        bool Is(Vector2Int c, BlockRole role) => blocks.TryGetValue(c, out var d) && d.role == role;
        bool IsSolid(Vector2Int c) => blocks.TryGetValue(c, out var d) && d.solid;
        /// <summary>Dá pra apoiar algo em cima (casco, convés ou piso).</summary>
        bool IsFloor(Vector2Int c) => blocks.TryGetValue(c, out var d) && d.Standable;

        public static bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;

        // ---------- Regras ----------
        public bool CanPlace(ShipBlockDef def, Vector2Int c, out string reason)
        {
            if (!InBounds(c)) { reason = "Fora da área do estaleiro."; return false; }
            if (blocks.ContainsKey(c)) { reason = "Já tem um bloco aqui."; return false; }
            if (blocks.Count > 0)
            {
                bool touches = false;
                foreach (var n in Neighbors) if (blocks.ContainsKey(c + n)) touches = true;
                if (!touches) { reason = "Precisa encostar no navio."; return false; }
            }
            if (!SupportOk(def, c, out reason)) return false;
            reason = null;
            return true;
        }

        bool SupportOk(ShipBlockDef def, Vector2Int c, out string reason)
        {
            reason = null;
            switch (def.role)
            {
                case BlockRole.Mast:
                    if (!(IsFloor(c + Vector2Int.down) || Is(c + Vector2Int.down, BlockRole.Mast)))
                    { reason = "O mastro precisa de casco, piso ou mastro embaixo."; return false; }
                    break;
                case BlockRole.Flag:
                    if (!Is(c + Vector2Int.down, BlockRole.Mast)) { reason = "A bandeira vai no topo de um mastro."; return false; }
                    break;
                case BlockRole.Decor:
                    if (!IsFloor(c + Vector2Int.down)) { reason = "Enfeites precisam de chão embaixo (casco ou piso)."; return false; }
                    break;
                case BlockRole.Sail:
                    if (c.y <= WaterRow) { reason = "Vela embaixo d'água não serve!"; return false; }
                    bool attached = false;
                    foreach (var n in Neighbors) if (Is(c + n, BlockRole.Mast) || Is(c + n, BlockRole.Sail)) attached = true;
                    if (!attached) { reason = "A vela precisa ficar presa num mastro (ou em outra vela)."; return false; }
                    break;
                case BlockRole.Cannon:
                    if (c.y <= WaterRow) { reason = "Canhão embaixo d'água não atira!"; return false; }
                    if (!IsFloor(c + Vector2Int.down)) { reason = "O canhão precisa de chão embaixo (casco ou piso)."; return false; }
                    break;
            }
            return true;
        }

        public bool CanRemove(Vector2Int c, out string reason)
        {
            if (!blocks.TryGetValue(c, out var def)) { reason = "Não tem bloco aqui."; return false; }
            blocks.Remove(c);
            try
            {
                int hulls = 0;
                foreach (var kv in blocks) if (kv.Value.solid) hulls++;
                if (hulls == 0) { reason = "O navio precisa de pelo menos um casco."; return false; }
                foreach (var kv in blocks)
                    if (!SupportOk(kv.Value, kv.Key, out _)) { reason = $"O bloco {kv.Value.displayName} depende deste."; return false; }
                if (!Connected()) { reason = "Isso partiria o navio em dois!"; return false; }
                reason = null;
                return true;
            }
            finally { blocks[c] = def; }
        }

        bool Connected()
        {
            if (blocks.Count == 0) return true;
            var seen = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            foreach (var k in blocks.Keys) { queue.Enqueue(k); seen.Add(k); break; }
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var n in Neighbors)
                {
                    var nb = cur + n;
                    if (blocks.ContainsKey(nb) && seen.Add(nb)) queue.Enqueue(nb);
                }
            }
            return seen.Count == blocks.Count;
        }

        // ---------- Alterações ----------
        public void Set(Vector2Int c, ShipBlockDef def, bool notify = true) => Set(c, def, 0, notify);

        public void Set(Vector2Int c, ShipBlockDef def, int rot, bool notify = true)
        {
            blocks[c] = def;
            int n = def != null ? ShipShapes.Orientations(def.shape) : 1;
            if (n > 1 && rot % n != 0) rots[c] = rot % n; else rots.Remove(c);
            if (notify) Changed?.Invoke();
        }

        /// <summary>
        /// Troca o navio inteiro pelo desenho de outro (navio capturado). mirror = vira de lado (os inimigos
        /// são desenhados com a proa para a esquerda). O resultado fica centralizado na grade.
        /// </summary>
        public void ReplaceWith(ShipLayout other, bool mirror)
        {
            if (other == null || other.blocks.Count == 0) return;
            int minX = int.MaxValue, maxX = int.MinValue;
            foreach (var c in other.blocks.Keys) { minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x); }
            int shift = (Width - (maxX - minX + 1)) / 2 - minX;
            blocks.Clear();
            rots.Clear();
            foreach (var kv in other.blocks)
            {
                int x = mirror ? minX + maxX - kv.Key.x : kv.Key.x;
                int r = other.Rot(kv.Key);
                if (mirror && kv.Value != null) r = ShipShapes.Mirror(kv.Value.shape, r);
                var nc = new Vector2Int(x + shift, kv.Key.y);
                if (InBounds(nc)) Set(nc, kv.Value, r, false);
            }
            Changed?.Invoke();
        }

        public ShipBlockDef Remove(Vector2Int c)
        {
            if (!blocks.TryGetValue(c, out var def)) return null;
            blocks.Remove(c);
            rots.Remove(c);
            Changed?.Invoke();
            return def;
        }

        public ShipStats ComputeStats()
        {
            var s = new ShipStats();
            foreach (var d in blocks.Values)
            {
                s.blocks++;
                s.hp += d.hp;
                s.sails += d.sailPower;
                s.bunks += d.bunks;
                s.cannons += d.cannons;
                s.value += d.cost;
            }
            s.speedKnots = s.sails == 0 ? 0f : Mathf.Clamp(2f + s.sails * 2f / (1f + s.blocks * 0.04f), 0f, 14f);
            return s;
        }

        /// <summary>
        /// O barquinho inicial: quilha submersa com as pontas em rampa, convés de 8 cascos acima d'água,
        /// um mastro de 3 e 4 velas.
        /// </summary>
        public static ShipLayout Raft(ShipBlockDef hull, ShipBlockDef mast, ShipBlockDef sail, ShipBlockDef slope = null)
        {
            var l = new ShipLayout();
            int deck = WaterRow + 1;
            for (int x = 11; x <= 18; x++) l.Set(new Vector2Int(x, deck), hull, false);
            for (int x = 13; x <= 16; x++) l.Set(new Vector2Int(x, WaterRow), hull, false);
            l.Set(new Vector2Int(12, WaterRow), slope != null ? slope : hull, 3, false);   // ◥
            l.Set(new Vector2Int(17, WaterRow), slope != null ? slope : hull, 2, false);   // ◤
            for (int y = deck + 1; y <= deck + 3; y++) l.Set(new Vector2Int(15, y), mast, false);
            for (int y = deck + 2; y <= deck + 3; y++)
            {
                l.Set(new Vector2Int(14, y), sail, false);
                l.Set(new Vector2Int(16, y), sail, false);
            }
            return l;
        }

        /// <summary>
        /// Saves de antes de 24/11/2026: a água ficava uma linha mais alta (WaterRow 2). Para o navio
        /// "subir um bloco", cada casco que estava na linha d'água antiga ganha um casco igual embaixo (quilha).
        /// </summary>
        public bool MigrateLegacy()
        {
            foreach (var c in blocks.Keys) if (c.y <= WaterRow) return false;   // já tem algo submerso
            var add = new List<(Vector2Int, ShipBlockDef)>();
            foreach (var kv in blocks)
                if (kv.Key.y == WaterRow + 1 && kv.Value.solid && !blocks.ContainsKey(kv.Key + Vector2Int.down))
                    add.Add((kv.Key + Vector2Int.down, kv.Value));
            foreach (var (c, d) in add) blocks[c] = d;
            return add.Count > 0;
        }

        /// <summary>Espelho de uma célula em volta de um eixo vertical (x do eixo em "meias células": 2*x).</summary>
        public static Vector2Int MirrorCell(Vector2Int c, int axis2) => new Vector2Int(axis2 - 1 - c.x, c.y);
    }
}
