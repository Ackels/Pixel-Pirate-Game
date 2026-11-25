using System.Collections.Generic;

namespace Pixel.Village
{
    /// <summary>Contadores do jogo (vendas, baús, vitórias...). Usados pelas Crônicas e salvos no save.</summary>
    public static class GameStats
    {
        public const string Sales = "vendas";
        public const string Voyages = "viagens";
        public const string IslandsVisited = "ilhas";
        public const string SeaVictories = "vitorias_mar";
        public const string Legends = "lendarios";
        public const string Chests = "baus";
        public const string Hires = "contratacoes";

        static readonly Dictionary<string, int> counters = new Dictionary<string, int>();

        public static void Add(string key, int amount = 1) => counters[key] = Get(key) + amount;
        public static int Get(string key) => counters.TryGetValue(key, out int v) ? v : 0;
        public static void Reset() => counters.Clear();

        public static List<ItemCount> Export()
        {
            var list = new List<ItemCount>();
            foreach (var kv in counters) list.Add(new ItemCount { item = kv.Key, count = kv.Value });
            return list;
        }

        public static void Import(List<ItemCount> list)
        {
            counters.Clear();
            if (list != null) foreach (var c in list) counters[c.item] = c.count;
        }
    }
}
