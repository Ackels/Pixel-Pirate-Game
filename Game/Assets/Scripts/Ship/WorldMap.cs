using System.Collections.Generic;
using UnityEngine;

namespace Pixel.Ship
{
    public enum IslandType { Home, Port, Wild, PirateCamp, Fortress }

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
            IslandType.Fortress => "Fortaleza do Capitão Lendário",
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
            map.Spread();
            map.AddFortress(seed);
            map.RevealAround(map.Home);
            return map;
        }

        /// <summary>Nome da fortaleza do primeiro Capitão Lendário (o Corvo Rubro).</summary>
        public const string FortressName = "Fortaleza do Corvo";
        public IslandNode Fortress => Islands.Find(i => i.type == IslandType.Fortress);

        /// <summary>
        /// A fortaleza do Capitão Lendário: bem no leste, no lugar mais longe das outras ilhas. Entra depois de
        /// tudo (com sorteio próprio), então as ilhas que já existiam continuam iguais nos saves antigos.
        /// </summary>
        void AddFortress(int seed)
        {
            var rng = new System.Random(seed ^ 0x5F0A7);
            Vector2 best = new Vector2(Width - 6f, Height / 2f);
            float bestGap = -1f;
            for (float x = Width - 18f; x <= Width - 9f; x += 1.5f)       // longe da borda: o nome cabe no papel
                for (float y = 13f; y <= Height - 12f; y += 1.5f)
                {
                    var p = new Vector2(x, y);
                    float gap = float.MaxValue;
                    foreach (var i in Islands)
                        gap = Mathf.Min(gap, new Vector2(i.pos.x - p.x, (i.pos.y - p.y) * 2f).magnitude);
                    gap += (float)rng.NextDouble() * 0.5f;   // desempate sorteado
                    if (gap > bestGap) { bestGap = gap; best = p; }
                }
            Islands.Add(new IslandNode { id = Islands.Count, name = FortressName, type = IslandType.Fortress, pos = best, seed = rng.Next() });
        }

        /// <summary>
        /// Afasta ilhas que ficaram coladas (as duas de perto de casa são arrastadas depois de geradas e podiam cair
        /// em cima de outra). Conta a distância na vertical em dobro, porque o nome embaixo do ícone é largo.
        /// Só mexe na posição (o id, o nome e o tipo continuam iguais, então saves antigos seguem valendo).
        /// </summary>
        void Spread()
        {
            const float MinGap = 18f;
            for (int iter = 0; iter < 400; iter++)
            {
                bool moved = false;
                for (int a = 0; a < Islands.Count; a++)
                    for (int b = a + 1; b < Islands.Count; b++)
                    {
                        var pa = Islands[a].pos; var pb = Islands[b].pos;
                        var d = new Vector2(pb.x - pa.x, (pb.y - pa.y) * 2f);
                        float len = d.magnitude;
                        if (len >= MinGap) continue;
                        if (len < 0.01f) d = new Vector2(1f, 0.5f);
                        var push = d.normalized * (MinGap - len) * 0.5f;
                        push.y *= 0.5f;                          // volta para a escala real
                        // Casa e as duas primeiras ficam paradas; a outra é que se afasta
                        bool fixedA = a <= 2, fixedB = b <= 2;
                        if (fixedA && fixedB) continue;
                        if (!fixedB) Islands[b].pos = Clamp(pb + (fixedA ? push * 2f : push));
                        if (!fixedA) Islands[a].pos = Clamp(pa - (fixedB ? push * 2f : push));
                        moved = true;
                    }
                if (!moved) break;
            }
        }

        static Vector2 Clamp(Vector2 p) => new Vector2(Mathf.Clamp(p.x, 18f, Width - 4f), Mathf.Clamp(p.y, 5f, Height - 5f));

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
