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
        public const int Width = 26;
        public const int Height = 14;
        /// <summary>Linhas abaixo desta ficam submersas (o casco pode ir pra baixo d'água).</summary>
        public const int WaterRow = 2;

        readonly Dictionary<Vector2Int, ShipBlockDef> blocks = new Dictionary<Vector2Int, ShipBlockDef>();
        public IReadOnlyDictionary<Vector2Int, ShipBlockDef> Blocks => blocks;
        public event System.Action Changed;

        static readonly Vector2Int[] Neighbors = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        public ShipBlockDef Get(Vector2Int c) => blocks.TryGetValue(c, out var d) ? d : null;
        bool Is(Vector2Int c, BlockRole role) => blocks.TryGetValue(c, out var d) && d.role == role;
        bool IsSolid(Vector2Int c) => blocks.TryGetValue(c, out var d) && d.solid;

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
                    if (!(IsSolid(c + Vector2Int.down) || Is(c + Vector2Int.down, BlockRole.Mast)))
                    { reason = "O mastro precisa de casco ou mastro embaixo."; return false; }
                    break;
                case BlockRole.Sail:
                    if (c.y <= WaterRow) { reason = "Vela embaixo d'água não serve!"; return false; }
                    bool attached = false;
                    foreach (var n in Neighbors) if (Is(c + n, BlockRole.Mast) || Is(c + n, BlockRole.Sail)) attached = true;
                    if (!attached) { reason = "A vela precisa ficar presa num mastro (ou em outra vela)."; return false; }
                    break;
                case BlockRole.Cannon:
                    if (c.y <= WaterRow) { reason = "Canhão embaixo d'água não atira!"; return false; }
                    if (!IsSolid(c + Vector2Int.down)) { reason = "O canhão precisa de casco ou convés embaixo."; return false; }
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
        public void Set(Vector2Int c, ShipBlockDef def, bool notify = true)
        {
            blocks[c] = def;
            if (notify) Changed?.Invoke();
        }

        public ShipBlockDef Remove(Vector2Int c)
        {
            if (!blocks.TryGetValue(c, out var def)) return null;
            blocks.Remove(c);
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

        /// <summary>A jangada inicial: 6 cascos, um mastro de 3 e 4 velas.</summary>
        public static ShipLayout Raft(ShipBlockDef hull, ShipBlockDef mast, ShipBlockDef sail)
        {
            var l = new ShipLayout();
            for (int x = 10; x <= 15; x++) l.Set(new Vector2Int(x, WaterRow), hull, false);
            for (int y = 3; y <= 5; y++) l.Set(new Vector2Int(12, y), mast, false);
            l.Set(new Vector2Int(11, 4), sail, false);
            l.Set(new Vector2Int(11, 5), sail, false);
            l.Set(new Vector2Int(13, 4), sail, false);
            l.Set(new Vector2Int(13, 5), sail, false);
            return l;
        }
    }
}
