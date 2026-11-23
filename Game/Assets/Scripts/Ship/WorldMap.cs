using System.Collections.Generic;
using UnityEngine;

namespace Pixel.Ship
{
    public enum IslandType { Home, Port, Wild, PirateCamp }

    /// <summary>Uma ilha do mapa-múndi.</summary>
    public class IslandNode
    {
        public int id;
        public string name;
        public IslandType type;
        public Vector2 pos;          // em milhas, no mapa (0..100 x 0..60)
        public int seed;
        public bool revealed;        // aparece no mapa (senão, fica na névoa)
        public bool visited;
        public bool looted;          // baús já abertos
        public bool cleared;         // inimigos já derrotados
        public bool hasTreasureHint; // marcado por uma garrafa com mapa

        /// <summary>0 = seguro (perto de casa) até ~5 = perigoso (longe).</summary>
        public int Danger(Vector2 home) => Mathf.Clamp(Mathf.RoundToInt(Vector2.Distance(pos, home) / 18f), 0, 5);

        public static string TypeName(IslandType t) => t switch
        {
            IslandType.Home => "Enseada (sua vila)",
            IslandType.Port => "Porto amigo",
            IslandType.Wild => "Ilha selvagem",
            IslandType.PirateCamp => "Acampamento pirata",
            _ => "?",
        };
    }

    /// <summary>
    /// O mapa-múndi: ilhas geradas a partir de uma semente. Começa com as ilhas perto de casa
    /// reveladas; visitar uma ilha revela as vizinhas.
    /// </summary>
    public class WorldMap
    {
        public const float Width = 100f, Height = 60f;
        public const float RevealRadius = 32f;

        public readonly List<IslandNode> Islands = new List<IslandNode>();
        public IslandNode Home => Islands[0];

        static readonly string[] Names =
        {
            "Ilha do Coco", "Recife Sombrio", "Porto Esmeralda", "Baía dos Ossos", "Ilhota da Gaivota",
            "Rochedo do Corvo", "Porto Velho", "Ilha da Caveira", "Praia das Conchas", "Atol do Sol",
            "Ilha do Papagaio", "Porto Aurora", "Morro do Tesouro", "Ilha Perdida", "Banco de Areia",
        };

        public static WorldMap Generate(int seed)
        {
            var rng = new System.Random(seed);
            var map = new WorldMap();
            map.Islands.Add(new IslandNode { id = 0, name = "Enseada", type = IslandType.Home, pos = new Vector2(10f, 30f), revealed = true, visited = true });

            var names = new List<string>(Names);
            int tries = 0;
            while (map.Islands.Count < 12 && tries++ < 2000)
            {
                var p = new Vector2(18f + (float)rng.NextDouble() * (Width - 24f), 6f + (float)rng.NextDouble() * (Height - 12f));
                bool tooClose = false;
                foreach (var i in map.Islands) if (Vector2.Distance(i.pos, p) < 12f) tooClose = true;
                if (tooClose) continue;

                float far = Vector2.Distance(p, map.Home.pos) / Width;   // 0 perto .. ~1 longe
                double roll = rng.NextDouble();
                IslandType type = roll < 0.25 ? IslandType.Port : roll < 0.25 + 0.35 + far * 0.15 ? IslandType.Wild : IslandType.PirateCamp;
                if (map.Islands.Count == 1) type = IslandType.Wild;       // a mais próxima de início é tranquila
                if (map.Islands.Count == 2) type = IslandType.Port;

                int n = rng.Next(names.Count);
                map.Islands.Add(new IslandNode { id = map.Islands.Count, name = names[n], type = type, pos = p, seed = rng.Next() });
                names.RemoveAt(n);
            }
            // As duas primeiras geradas ficam perto de casa para o começo ser amigável
            if (map.Islands.Count > 2)
            {
                map.Islands[1].pos = map.Home.pos + new Vector2(16f, -8f);
                map.Islands[2].pos = map.Home.pos + new Vector2(20f, 10f);
            }
            map.RevealAround(map.Home);
            return map;
        }

        public void RevealAround(IslandNode center)
        {
            foreach (var i in Islands)
                if (Vector2.Distance(i.pos, center.pos) <= RevealRadius) i.revealed = true;
        }

        /// <summary>Revela uma ilha escondida qualquer (garrafa com mapa). Retorna null se não houver.</summary>
        public IslandNode RevealRandom(System.Random rng)
        {
            var hidden = Islands.FindAll(i => !i.revealed);
            if (hidden.Count == 0) return null;
            var pick = hidden[rng.Next(hidden.Count)];
            pick.revealed = true;
            pick.hasTreasureHint = true;
            return pick;
        }

        public static float Distance(IslandNode a, IslandNode b) => Vector2.Distance(a.pos, b.pos);
    }
}
