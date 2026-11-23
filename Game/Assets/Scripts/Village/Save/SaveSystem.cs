using System;
using System.Collections.Generic;
using System.IO;
using Pixel.Ship;
using UnityEngine;

namespace Pixel.Village
{
    /// <summary>
    /// Salva e carrega o jogo num arquivo JSON (pasta de dados do jogo).
    /// Só salva com o capitão na vila; no mar, o jogo salva quando você volta.
    /// </summary>
    public static class SaveSystem
    {
        const string FileName = "save_1.json";
        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
        public static bool Exists => File.Exists(FilePath);

        public static bool CanSaveNow =>
            VillageManager.Instance != null && CrewManager.Instance != null &&
            GameFlow.Instance != null && !GameFlow.Instance.AtSea;

        // ---------- Salvar ----------
        public static bool Save()
        {
            if (!CanSaveNow) return false;
            try
            {
                var data = Collect();
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[Pixel] Falha ao salvar: " + e.Message);
                return false;
            }
        }

        static SaveData Collect()
        {
            var vm = VillageManager.Instance;
            var cm = CrewManager.Instance;
            var flow = GameFlow.Instance;
            var d = new SaveData { savedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm"), gold = vm.Gold };

            foreach (var kv in vm.Stock) d.stock.Add(new ItemCount { item = kv.Key.name, count = kv.Value });
            foreach (var b in vm.Buildings) d.buildings.Add(new BuildingSave { def = b.def.name, x = b.origin.x, y = b.origin.y });
            if (flow.captain != null) { d.captainX = flow.captain.transform.position.x; d.captainY = flow.captain.transform.position.y; }

            d.day = cm.Day;
            d.dayTimer = cm.DayTimer;
            foreach (var c in cm.Crew) d.crew.Add(CrewSave.From(c));
            foreach (var r in cm.Recruits) d.recruits.Add(CrewSave.From(r));
            foreach (var (msg, color) in cm.History) d.log.Add(new LogLine { msg = msg, color = color });

            foreach (var kv in flow.Ship.Blocks) d.ship.Add(new BlockSave { def = kv.Value.name, x = kv.Key.x, y = kv.Key.y });
            d.shipDamage = flow.ShipDamage;
            d.worldSeed = flow.WorldSeedUsed;
            foreach (var i in flow.World.Islands)
                d.islands.Add(new IslandSave { id = i.id, revealed = i.revealed, visited = i.visited, looted = i.looted, cleared = i.cleared, treasure = i.hasTreasureHint });
            var em = ExpeditionManager.Instance;
            if (em != null)
            {
                d.dungeonsCleared.AddRange(em.ClearedNames);
                d.presets.AddRange(em.presets);
            }
            if (ChronicleManager.Instance != null) d.chronicle = ChronicleManager.Instance.Current;
            d.stats = GameStats.Export();
            return d;
        }

        // ---------- Carregar ----------
        public static SaveData Peek()
        {
            try { return Exists ? JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath)) : null; }
            catch { return null; }
        }

        public static bool Load()
        {
            var d = Peek();
            if (d == null || !CanSaveNow) return false;
            var vm = VillageManager.Instance;
            var cm = CrewManager.Instance;
            var flow = GameFlow.Instance;

            // Navio e mapa
            var ship = new ShipLayout();
            foreach (var b in d.ship)
            {
                var def = Array.Find(flow.shipBlocks, x => x != null && x.name == b.def);
                if (def != null) ship.Set(new Vector2Int(b.x, b.y), def, false);
            }
            if (ship.Blocks.Count == 0) ship = ShipLayout.Raft(flow.raftHull, flow.raftMast, flow.raftSail);
            flow.RestoreFromSave(ship, d.shipDamage, d.worldSeed);
            foreach (var s in d.islands)
            {
                var n = flow.World.Islands.Find(i => i.id == s.id);
                if (n == null) continue;
                n.revealed = s.revealed; n.visited = s.visited; n.looted = s.looted; n.cleared = s.cleared; n.hasTreasureHint = s.treasure;
            }

            // Vila
            vm.ClearForLoad(d.gold);
            foreach (var s in d.stock)
            {
                var item = Array.Find(vm.items, x => x != null && x.name == s.item);
                if (item != null) vm.SetStock(item, s.count);
            }
            foreach (var b in d.buildings)
            {
                var def = Array.Find(vm.buildable, x => x != null && x.name == b.def);
                if (def != null) vm.Place(def, new Vector2Int(b.x, b.y));
            }

            // Tripulação
            var members = new List<CrewMember>();
            foreach (var c in d.crew) members.Add(c.ToMember());
            var recruits = new List<CrewMember>();
            foreach (var r in d.recruits) recruits.Add(r.ToMember());
            var log = new List<(string, Color)>();
            foreach (var l in d.log) log.Add((l.msg, l.color));
            cm.RestoreFromSave(members, recruits, d.day, d.dayTimer, log);
            ExpeditionManager.Instance?.RestoreFromSave(d.dungeonsCleared, d.presets?.ToArray());
            GameStats.Import(d.stats);
            ChronicleManager.Instance?.RestoreFromSave(d.chronicle);

            // Capitão
            if (flow.captain != null && (d.captainX != 0 || d.captainY != 0))
            {
                var p = new Vector2(d.captainX, d.captainY);
                flow.captain.transform.position = p;
                var body = flow.captain.GetComponent<Rigidbody2D>();
                if (body != null) body.position = p;
            }
            cm.Report($"Jogo carregado (salvo em {d.savedAt}).", new Color(0.6f, 0.9f, 1f));
            return true;
        }

        public static void Delete()
        {
            if (Exists) File.Delete(FilePath);
        }
    }
}
