using System.Collections.Generic;
using Pixel.Combat;
using Pixel.Village;
using UnityEngine;

namespace Pixel.Ship
{
    /// <summary>
    /// Monta a ilha do destino em visão lateral, ao lado do navio: praia, morros, coqueiros,
    /// baús, inimigos (caranguejos ou piratas) ou o comerciante do porto. A ilha "chega" deslizando.
    /// </summary>
    public class IslandBuilder : MonoBehaviour
    {
        public SeaController sea;
        public EncounterDirector director;

        /// <summary>Peças de terreno escolhidas pela máscara de vizinhos (1=N 2=NE 4=L 8=SE 16=S 32=SO 64=O 128=NO).</summary>
        [System.Serializable]
        public class TerrainSet
        {
            public int[] masks;
            public Sprite[] sprites;

            public Sprite Get(int mask)
            {
                int best = -1, bestScore = int.MaxValue;
                for (int i = 0; i < masks.Length; i++)
                {
                    int diff = masks[i] ^ mask;
                    int score = Bits(diff & 0b01010101) * 10 + Bits(diff & 0b10101010);
                    if (score < bestScore) { bestScore = score; best = i; }
                }
                return best >= 0 ? sprites[best] : null;
            }

            static int Bits(int v) { int c = 0; while (v != 0) { c += v & 1; v >>= 1; } return c; }
        }

        [System.Serializable]
        public class Creature
        {
            public string name;
            public Sprite[] frames;
        }

        [Header("Sprites")]
        public TerrainSet grass, sand;
        public Sprite chestClosed, chestOpen, tent, campfire, palm, market, plank, grassDecor;
        public Sprite[] backPalms;
        [Tooltip("Coqueiros bem ao fundo (desfocados e azulados), atrás dos do fundo.")]
        public Sprite[] farPalms;
        /// <summary>Peças das construções de lado (Art/Mar/Casa, geradas por tools/arte/casa_export.py).</summary>
        [System.Serializable]
        public class HouseArt
        {
            public Sprite back, pillar, pillarTop, pillarBase, beam, beamLeft, beamRight, roof, roofLeft, roofRight;
            public Sprite table, chair, window, barrel, barrelSide, barrelFront, bottleBlue, bottleGreen, bottleRed, skull, shelf;
            public Sprite fish, meat, steak, shrimp, sword, axe, lance;
            public Sprite[] candle;
        }
        public HouseArt house;

        [Tooltip("Bichos das ilhas selvagens (um é sorteado para cada inimigo).")]
        public Creature[] creatures;

        public const int Columns = 30;
        // Construções: Acampamento = Taverna + Arsenal (com fogueira no meio); Porto = Mercado + Taverna
        const int TavernCol = 7, TavernW = 8, ArsenalCol = 18, ArsenalW = 7, FireCol = 16;
        const int MarketCol = 7, MarketW = 7, PortTavernCol = 16, PortTavernW = 8;
        public GameObject Root { get; private set; }
        /// <summary>Ilha que está ficando para trás (sem colisão, se afastando).</summary>
        GameObject departing;
        float departDir = -1f;
        /// <summary>Tem ilha na tela (chegando, atracada ou indo embora).</summary>
        public bool Present => Root != null || departing != null;
        public bool Arrived { get; private set; }
        public IslandNode Node { get; private set; }

        readonly List<Combatant> enemies = new List<Combatant>();
        readonly List<(Vector2 local, System.Action spawn)> pendingSpawns = new List<(Vector2, System.Action)>();
        GameObject gangplank;
        float targetX, beachTop, waterline;
        int[] tops;
        bool clearedAnnounced;
        int chestsTotal, chestsOpened;
        System.Random rng;

        public float RegionMin => Root != null ? Root.transform.position.x + 3f : 0f;
        public float RegionMax => Root != null ? Root.transform.position.x + Columns : 0f;

        // ---------- Construção ----------
        public void Build(IslandNode node)
        {
            Leave();
            Node = node;
            chestsTotal = chestsOpened = 0;
            buriedChest = null;
            buriedUse = null;
            digMark = null;
            Arrived = false;
            clearedAnnounced = node.cleared;
            rng = new System.Random(node.seed);
            waterline = sea.shipView.WaterlineY;
            beachTop = waterline + 1.5f;   // praia um bloco acima d'água (na altura do convés)
            targetX = sea.shipView.RightEdge + 1.6f;

            Root = new GameObject($"Ilha {node.name}", typeof(Rigidbody2D), typeof(CompositeCollider2D));
            Root.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            Root.GetComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Polygons;
            Root.transform.position = new Vector3(targetX + 30f, 0, 0);

            // Alturas: praia plana e depois morros suaves
            tops = new int[Columns];
            int h = 0, run = 0;
            for (int i = 0; i < Columns; i++)
            {
                if (i >= 5 && --run <= 0)
                {
                    h = Mathf.Clamp(h + rng.Next(-1, 2), 0, 3);
                    run = rng.Next(3, 6);
                }
                tops[i] = i < 5 ? 0 : h;
            }
            // Cada ilha sorteia (pela semente) o que tem de especial; o chão fica plano onde vai construção
            Variant = PickVariant(node.type);
            occupied.Clear();
            foreach (var (a, b) in PlannedAreas()) { FlattenRange(a, b); occupied.Add((a, b)); }
            for (int i = 0; i < Columns; i++) BuildColumn(i);

            var free = new List<int>();
            for (int i = 6; i < Columns - 1; i++) free.Add(i);
            int danger = node.Danger(GameFlowHome());
            // Colunas das construções ficam sem coqueiro da frente nem baú solto
            bool hasArt = house != null && house.back != null;
            foreach (var (a, b) in occupied) free.RemoveAll(c => c >= a - 1 && c <= b);

            // Coqueiros de enfeite (na frente e no fundo) e tufos de grama
            for (int k = 0; k < (occupied.Count > 0 ? 2 : 4); k++) Decor(palm, Take(free), -36, 1f);
            // Selva de fundo em duas fileiras bem cheias (como no Pixel Piracy): a de trás menor, azulada e desfocada
            if (farPalms != null && farPalms.Length > 0)
                for (float x = 5.2f; x < Columns + 0.5f; x += 0.9f + (float)rng.NextDouble() * 0.5f)
                {
                    int col = Mathf.Clamp((int)x, 5, Columns - 1);
                    var sr = Sprite(farPalms[rng.Next(farPalms.Length)], new Vector2(x, Top(col).y + 0.7f + (float)rng.NextDouble() * 0.5f), -52);
                    sr.transform.localScale = Vector3.one * (0.75f + (float)rng.NextDouble() * 0.3f);
                    sr.flipX = rng.Next(2) == 0;
                }
            if (backPalms != null && backPalms.Length > 0)
                for (float x = 5.8f; x < Columns; x += 1.5f + (float)rng.NextDouble() * 1.2f)
                {
                    int col = Mathf.Clamp((int)x, 5, Columns - 1);
                    var sr = Sprite(backPalms[rng.Next(backPalms.Length)], new Vector2(x, Top(col).y + (float)rng.NextDouble() * 0.3f), -50);
                    sr.transform.localScale = Vector3.one * (0.9f + (float)rng.NextDouble() * 0.25f);
                    sr.flipX = rng.Next(2) == 0;
                }
            if (grassDecor != null)
                for (int k = 0; k < 6; k++) Decor(grassDecor, 6 + rng.Next(Columns - 8), -44, 1f);

            if (!hasArt) Variant = 0;
            switch (node.type)
            {
                case IslandType.Wild: WildIsland(danger, free); break;
                case IslandType.PirateCamp: CampIsland(danger, free); break;
                case IslandType.Port: PortIsland(); break;
            }
            // Às vezes a ilha esconde um baú esquecido a mais
            if (rng.NextDouble() < 0.2) Chest(Take(free), rng.Next(40, 91), Items(("Rum", 1, 3)));

            if (node.hasTreasureHint && !node.looted) Chest(Columns - 3, rng.Next(120, 201), Items(("Rum", 3, 6)), true);
        }

        /// <summary>Deixa as colunas de a até b (sem incluir b) na altura da coluna a.</summary>
        void FlattenRange(int a, int b)
        {
            int h = tops[a];
            for (int c = a; c < b && c < Columns; c++) if (c >= 5) tops[c] = h;
        }

        // ---------- Cada ilha é de um jeito (sorteado pela semente) ----------
        /// <summary>Versão sorteada da ilha (o que ela tem de especial). Veja PickVariant.</summary>
        public int Variant { get; private set; }
        readonly List<(int a, int b)> occupied = new List<(int, int)>();

        const int WreckCol = 13, WreckW = 9, RuinCol = 10, RuinW = 12, HutCol = 12, HutW = 5;

        /// <summary>
        /// Selvagem: 0 calma, 1 naufrágio (esqueletos guardam), 2 ruínas antigas, 3 ninho de caranguejos, 4 acampamento abandonado, 5 eremita.
        /// Acampamento: 0 vila (Taverna + Arsenal), 1 só Taverna, 2 acampamento de tendas, 3 forte do Contramestre.
        /// Porto: 0 Mercado + Taverna, 1 só Mercado, 2 feira (barraca) + Taverna.
        /// </summary>
        int PickVariant(IslandType type)
        {
            int[] w = type switch
            {
                IslandType.Wild => new[] { 22, 18, 16, 16, 12, 16 },
                IslandType.PirateCamp => new[] { 35, 25, 25, 15 },
                _ => new[] { 50, 30, 20 },
            };
            int total = 0;
            foreach (int x in w) total += x;
            int roll = rng.Next(total);
            for (int i = 0; i < w.Length; i++) { if (roll < w[i]) return i; roll -= w[i]; }
            return 0;
        }

        /// <summary>Faixas de colunas que precisam de chão plano (construções, naufrágio, ruínas).</summary>
        IEnumerable<(int, int)> PlannedAreas()
        {
            switch (Node.type)
            {
                case IslandType.Wild:
                    if (Variant == 1) yield return (WreckCol, WreckCol + WreckW);
                    if (Variant == 2) yield return (RuinCol, RuinCol + RuinW);
                    if (Variant == 4) yield return (15, 22);
                    if (Variant == 5) yield return (HutCol, HutCol + HutW);
                    break;
                case IslandType.PirateCamp:
                    if (Variant == 0 || Variant == 3) yield return (TavernCol, ArsenalCol + ArsenalW);
                    if (Variant == 1) yield return (TavernCol, TavernCol + TavernW + 6);
                    if (Variant == 2) yield return (13, 24);
                    break;
                case IslandType.Port:
                    if (Variant == 0 || Variant == 2) yield return (MarketCol, PortTavernCol + PortTavernW);
                    if (Variant == 1) yield return (MarketCol, MarketCol + MarketW + 4);
                    break;
            }
        }

        ShipBlockDef Block(string name)
        {
            if (sea.Palette == null) return null;
            foreach (var d in sea.Palette) if (d != null && d.name == name) return d;
            return null;
        }

        void WildIsland(int danger, List<int> free)
        {
            int crabs = rng.Next(2, 5) + danger / 2;
            switch (Variant)
            {
                case 0:   // calma: quase nada, só a praia e os coqueiros
                    Chest(Take(free), rng.Next(10, 31), Items(("Peixe", 2, 5), ("Cana", 2, 5)));
                    crabs = rng.Next(0, 2);
                    break;
                case 1:   // naufrágio: um navio partido na praia, esqueletos guardam o baú do capitão
                    Wreck(danger);
                    crabs = rng.Next(0, 2);
                    break;
                case 2:   // ruínas: pilares quebrados de uma construção antiga e um baú com mais ouro
                    Ruins();
                    break;
                case 3:   // ninho de caranguejos: muitos bichos e um baú gordo
                    crabs += 3;
                    Chest(Take(free), rng.Next(60, 101), Items(("Peixe", 4, 8)));
                    break;
                case 4:   // acampamento abandonado: tenda, fogueira apagada e baús esquecidos
                    if (tent != null) Decor(tent, 19, -25, 1f);
                    var fire = Sprite(campfire, Top(16), -24);
                    fire.color = new Color(0.45f, 0.42f, 0.42f);   // apagada
                    Chest(17, rng.Next(20, 41), Items(("Rum", 1, 3)));
                    Chest(21, rng.Next(20, 41), Items(("Peixe", 2, 5)));
                    crabs = rng.Next(1, 3);
                    break;
                case 5:   // eremita: uma cabana com um velho que conhece os mares
                    Hermit();
                    crabs = rng.Next(0, 2);
                    break;
            }
            if (Variant != 0 && Variant != 3) Chest(Take(free), rng.Next(10, 31), Items(("Peixe", 2, 5), ("Cana", 2, 5)));
            if (Node.cleared) return;
            for (int k = 0; k < crabs; k++)
            {
                int col = Take(free);
                pendingSpawns.Add((Top(col), () => Crab(col, danger)));
            }
        }

        /// <summary>Navio naufragado: casco inclinado e quebrado, mastro tombado, vela rasgada; esqueletos guardam o baú.</summary>
        void Wreck(int danger)
        {
            float floor = beachTop + tops[WreckCol];
            var hull = Block("CascoEscuro") ?? Block("Casco");
            var slope = Block("CascoEscuroRampa") ?? Block("CascoRampa");
            var mast = Block("Mastro");
            var sail = Block("VelaPreta") ?? Block("Vela");
            var root = new GameObject("Naufragio").transform;
            root.SetParent(Root.transform, false);
            root.localPosition = new Vector2(WreckCol + WreckW / 2f, floor - 0.35f);
            root.localRotation = Quaternion.Euler(0, 0, -9f);
            void Piece(Sprite sp, float x, float y, int order, float rot = 0f, bool flip = false)
            {
                if (sp == null) return;
                var sr = new GameObject("Destroco").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(root, false);
                sr.transform.localPosition = new Vector2(x, y);
                sr.transform.localRotation = Quaternion.Euler(0, 0, rot);
                sr.sprite = sp;
                sr.flipX = flip;
                sr.sortingOrder = order;
                sr.color = new Color(0.82f, 0.78f, 0.74f);   // madeira velha
            }
            for (int x = -3; x <= 3; x++)
            {
                if (x == 1) continue;                                       // um rombo no casco
                Piece(hull?.sprite, x, 0.5f, -34);
                if (x > -3 && x < 3 && x != 0) Piece(hull?.sprite, x, 1.5f, -34);
            }
            Piece(slope?.sprite, -4, 0.5f, -34, 0f, true);
            Piece(slope?.sprite, 4, 0.5f, -34);
            for (int y = 0; y < 3; y++) Piece(mast?.sprite, 0.6f + y * 0.55f, 2.5f + y * 0.85f, -35, -33f);   // mastro tombado
            Piece(sail != null ? sail.Preview : null, 2.3f, 3.6f, -35, -40f);
            Chest(WreckCol + WreckW / 2, rng.Next(60, 121), Items(("Rum", 2, 5)));
            if (Node.cleared) return;
            int n = 2 + danger / 2;
            for (int k = 0; k < n; k++)
            {
                float x = WreckCol + 1.5f + k * 2.2f;
                pendingSpawns.Add((new Vector2(x, floor), () => Skeleton(x, floor, danger)));
            }
        }

        void Skeleton(float x, float floor, int danger)
        {
            Vector2 world = (Vector2)Root.transform.position + new Vector2(x, floor + 0.1f);
            var lib = director.Looks;
            if (lib == null || lib.skeleton == null) return;
            var c = director.SpawnFighter(world, Team.Enemy, "Esqueleto", null, lib.skeleton);
            c.maxHp = c.hp = Mathf.RoundToInt(40 * (1f + danger * 0.15f));
            c.damage = new Vector2Int(6 + danger, 10 + danger);
            c.attackCooldown = 0.9f;
            c.FacingRight = false;
            Guard(c);
        }

        /// <summary>Ruínas: pilares de tijolo quebrados em alturas diferentes, viga caída, caveira e um baú com mais ouro.</summary>
        void Ruins()
        {
            float floor = beachTop + tops[RuinCol];
            int[] heights = { 3, 1, 2, 0, 3, 2, 1 };
            for (int k = 0; k < 6; k++)
            {
                float x = RuinCol + 1 + k * 2;
                int h = heights[(k + Node.seed) % heights.Length];
                for (int y = 0; y < h; y++)
                {
                    var sr = Sprite(y == 0 ? house.pillarBase : house.pillar, new Vector2(x + 0.5f, floor + y + 0.5f), -34);
                    sr.color = new Color(0.78f, 0.8f, 0.76f);   // pedra velha, com musgo
                }
            }
            var beam = Sprite(house.beam, new Vector2(RuinCol + 6.5f, floor + 0.35f), -33);
            beam.transform.localRotation = Quaternion.Euler(0, 0, 8f);
            beam.color = new Color(0.78f, 0.8f, 0.76f);
            FloorProp(house.skull, RuinCol + 3.6f, floor, -32, 0.8f);
            Chest(RuinCol + 8, rng.Next(70, 131), Items(("Rum", 1, 3)));
        }

        /// <summary>Eremita: cabana pequena com um velho que, uma vez, conta onde há um tesouro.</summary>
        void Hermit()
        {
            float hf = House(HutCol, HutW, "CABANA");
            float hx = HutCol + HutW / 2f;
            FloorProp(house.barrelFront, hx + 1.3f, hf, -28, 1f);
            Candle(hx - 1.2f, hf + 1.9f);
            var lib = director.Looks;
            if (lib == null || lib.visitors == null || lib.visitors.Length == 0) return;
            var go = new GameObject("Eremita");
            go.transform.SetParent(Root.transform, false);
            go.transform.localPosition = new Vector2(hx - 0.2f, hf);
            var fig = go.AddComponent<LookFigure>();
            fig.Init(lib.visitors[Node.seed % lib.visitors.Length], 7);
            fig.Pose(false, true, false);
            var act = go.AddComponent<IdleActivity>();
            act.kind = IdleActivity.Kind.Chat;
            act.friendly = true;
            if (Node.looted) return;
            var it = go.AddComponent<Interactable>();
            it.prompt = "E: falar com o eremita";
            it.range = 1.6f;
            it.onUse = () => SeaDialog.Ask("O eremita", "\"Faz anos que ninguém aparece por aqui... Querem ouvir a história do tesouro que eu enterrei quando era pirata?\"",
                new SeaDialog.Option("Ouvir a história", () =>
                {
                    var island = sea.World.RevealRandom(new System.Random(Node.seed + 7));
                    if (island != null) { island.hasTreasureHint = true; sea.Say($"O eremita marcou {island.name} no seu mapa: tem um X de tesouro lá!"); }
                    else { sea.AddLoot(60, new Dictionary<string, int>()); sea.Say("O eremita te deu 60 moedas: \"Já não preciso disso.\""); }
                    Node.looted = true;
                    Destroy(it);
                }),
                new SeaDialog.Option("Agora não", () => { }));
        }

        void CampIsland(int danger, List<int> free)
        {
            switch (Variant)
            {
                case 0: PirateTown(danger, free, true, false); break;
                case 3: PirateTown(danger, free, true, true); break;
                case 1: PirateTown(danger, free, false, false); break;
                default:   // acampamento de tendas em volta da fogueira
                    if (tent != null) { Decor(tent, 15, -25, 1f); Decor(tent, 22, -25, 1f); }
                    Fire(18);
                    for (int k = 0; k < rng.Next(2, 4); k++) Chest(Take(free), rng.Next(30, 71), Items(("Rum", 2, 5)));
                    if (Node.cleared) break;
                    float fy = Top(18).y;
                    var jobs = new List<(float, IdleActivity.Kind, bool, Sprite)>
                    {
                        (17.6f, IdleActivity.Kind.Chat, true, null), (19.4f, IdleActivity.Kind.Eat, false, house.meat),
                        (15.5f, IdleActivity.Kind.Sleep, true, null), (20.4f, IdleActivity.Kind.Drink, false, house.bottleGreen),
                        (22.5f, IdleActivity.Kind.Sleep, true, null),
                    };
                    int n = 2 + rng.Next(0, 2) + danger / 2;
                    for (int k = 0; k < n && k < jobs.Count; k++)
                    {
                        var (x, kind, right, prop) = jobs[k];
                        pendingSpawns.Add((new Vector2(x, fy), () => PirateAt(x, fy, danger, kind, right, prop)));
                    }
                    break;
            }
        }

        void PortIsland()
        {
            switch (Variant)
            {
                case 1:   // só o Mercado, com gente conversando na frente
                    MarketHouse();
                    var lib = director.Looks;
                    if (lib != null && lib.visitors != null && lib.visitors.Length > 0)
                    {
                        float f = beachTop + tops[MarketCol];
                        Villager(lib.visitors[(Node.seed + 4) % lib.visitors.Length], MarketCol + MarketW + 1.2f, f, IdleActivity.Kind.Chat, false, null);
                        Villager(lib.visitors[(Node.seed + 5) % lib.visitors.Length], MarketCol + MarketW + 2.6f, f, IdleActivity.Kind.Eat, false, house.fish);
                    }
                    break;
                case 2:   // feira: barraca do comerciante ao ar livre + Taverna
                    Decor(market, MarketCol + 3, -25, 1f);
                    Merchant(MarketCol + 3);
                    TavernHouse(PortTavernCol, PortTavernW);
                    break;
                default:
                    MarketHouse();
                    TavernHouse(PortTavernCol, PortTavernW);
                    break;
            }
        }

        // ---------- Vilas: construções de lado, como no Pixel Piracy ----------
        /// <summary>Acampamento pirata: Taverna (comendo e bebendo), fogueira e Arsenal (dormindo, guardando o baú).</summary>
        void PirateTown(int danger, List<int> free, bool arsenal, bool boss)
        {
            float tf = House(TavernCol, TavernW, "TAVERNA");
            float tx = TavernCol + TavernW / 2f;
            Furnish(tx, tf, true);
            if (!arsenal)
            {
                // Só a taverna: fogueira e tenda do lado
                if (tent != null) Decor(tent, TavernCol + TavernW + 4, -25, 1f);
                Fire(TavernCol + TavernW + 1);
                for (int k = 0; k < rng.Next(1, 3); k++) Chest(Take(free), rng.Next(30, 71), Items(("Rum", 2, 5)));
                if (Node.cleared) return;
                float fy0 = Top(TavernCol + TavernW + 1).y;
                var few = new List<(float x, float floor, IdleActivity.Kind kind, bool right, Sprite prop)>
                {
                    (tx - 1.15f, tf, IdleActivity.Kind.Eat, true, house.fish),
                    (tx + 1.15f, tf, IdleActivity.Kind.Drink, false, house.bottleRed),
                    (TavernCol + TavernW + 0.6f, fy0, IdleActivity.Kind.Chat, true, null),
                    (TavernCol + TavernW + 4.5f, fy0, IdleActivity.Kind.Sleep, true, null),
                    (tx + 2.5f, tf, IdleActivity.Kind.Chat, false, null),
                };
                int m = 2 + rng.Next(0, 2) + danger / 2;
                for (int k = 0; k < m && k < few.Count; k++)
                {
                    var j = few[k];
                    pendingSpawns.Add((new Vector2(j.x, j.floor), () => PirateAt(j.x, j.floor, danger, j.kind, j.right, j.prop)));
                }
                return;
            }
            float af = House(ArsenalCol, ArsenalW, "ARSENAL");
            float ax = ArsenalCol + ArsenalW / 2f;
            // Arsenal: armas na parede, prateleira com garrafas, barris e o baú
            WallProp(house.sword, ax - 1.6f, af + 2.1f, -30, 1.2f);
            WallProp(house.axe, ax - 0.9f, af + 2.1f, -30, 1.2f);
            WallProp(house.lance, ax - 0.2f, af + 2.1f, -30, 1.2f);
            WallProp(house.shelf, ax + 1.4f, af + 2.0f, -30, 0.8f);
            WallProp(house.bottleGreen, ax + 1.1f, af + 2.0f + 0.32f, -29, 0.6f);
            WallProp(house.bottleRed, ax + 1.6f, af + 2.0f + 0.4f, -29, 0.6f);
            FloorProp(house.barrelFront, ax + 2.0f, af, -28, 1f);
            FloorProp(house.barrelFront, ax + 1.35f, af, -28, 1f);
            FloorProp(house.barrelSide, ax + 1.7f, af + 0.62f, -28, 1f);
            Chest(ArsenalCol + 1, boss ? rng.Next(150, 251) : rng.Next(50, 101), Items(("Rum", 3, 7)));
            Candle(ArsenalCol + 1.2f, af + 1.9f);
            if (boss && !Node.cleared)
                pendingSpawns.Add((new Vector2(ax + 0.6f, af), () => Boss(ax + 0.6f, af, danger)));
            Fire(FireCol);
            for (int k = 0; k < rng.Next(1, 3); k++) Chest(Take(free), rng.Next(30, 71), Items(("Rum", 2, 5)));
            if (Node.cleared) return;

            // Quem está fazendo o quê (mais perigo = mais piratas)
            int n = 3 + rng.Next(0, 2) + danger / 2;
            var jobs = new List<(float x, float floor, IdleActivity.Kind kind, bool right, Sprite prop)>
            {
                (tx - 1.15f, tf, IdleActivity.Kind.Eat, true, house.fish),
                (tx + 1.15f, tf, IdleActivity.Kind.Drink, false, house.bottleRed),
                (ax - 1.0f, af, IdleActivity.Kind.Sleep, true, null),
                (FireCol + 0.5f - 0.9f, Top(FireCol).y, IdleActivity.Kind.Chat, true, null),
                (tx + 2.5f, tf, IdleActivity.Kind.Drink, false, house.bottleGreen),
                (ax + 0.3f, af, IdleActivity.Kind.Chat, false, null),
                (FireCol + 0.5f + 0.9f, Top(FireCol).y, IdleActivity.Kind.Eat, false, house.meat),
            };
            for (int k = 0; k < n && k < jobs.Count; k++)
            {
                var j = jobs[k];
                pendingSpawns.Add((new Vector2(j.x, j.floor), () => PirateAt(j.x, j.floor, danger, j.kind, j.right, j.prop)));
            }
        }

        /// <summary>Mercado: prateleiras com mercadorias e o comerciante atrás do balcão.</summary>
        void MarketHouse()
        {
            float mf = House(MarketCol, MarketW, "MERCADO");
            float mx = MarketCol + MarketW / 2f;
            WallProp(house.shelf, mx - 1.3f, mf + 2.0f, -30, 0.8f);
            WallProp(house.fish, mx - 1.6f, mf + 2.0f + 0.3f, -29, 1f);
            WallProp(house.meat, mx - 1.0f, mf + 2.0f + 0.3f, -29, 1f);
            WallProp(house.shelf, mx + 1.3f, mf + 2.0f, -30, 0.8f);
            WallProp(house.bottleBlue, mx + 1.0f, mf + 2.0f + 0.36f, -29, 0.6f);
            WallProp(house.shrimp, mx + 1.6f, mf + 2.0f + 0.3f, -29, 1f);
            FloorProp(house.barrel, mx + 2.3f, mf, -28, 0.6f);
            FloorProp(house.barrelFront, mx - 2.3f, mf, -28, 1f);
            Candle(MarketCol + 1.2f, mf + 1.9f);
            Merchant(MarketCol + MarketW / 2);
            FloorProp(house.table, mx - 0.55f, mf, -20, 0.6f);   // balcão na frente do comerciante
        }

        /// <summary>Taverna do porto: moradores comendo, bebendo e conversando (não lutam).</summary>
        void TavernHouse(int col, int width)
        {
            float pf = House(col, width, "TAVERNA");
            float px = col + width / 2f;
            Furnish(px, pf, false);
            var lib = director.Looks;
            if (lib == null || lib.visitors == null || lib.visitors.Length == 0) return;
            Villager(lib.visitors[(Node.seed + 1) % lib.visitors.Length], px - 1.15f, pf, IdleActivity.Kind.Eat, true, house.steak);
            Villager(lib.visitors[(Node.seed + 2) % lib.visitors.Length], px + 1.15f, pf, IdleActivity.Kind.Drink, false, house.bottleBlue);
            if (Node.seed % 2 == 0)
                Villager(lib.visitors[(Node.seed + 3) % lib.visitors.Length], px + 2.6f, pf, IdleActivity.Kind.Chat, false, null);
        }

        /// <summary>Contramestre: o chefe do forte (maior, mais forte, guarda o baú rico).</summary>
        void Boss(float x, float floor, int danger)
        {
            Vector2 world = (Vector2)Root.transform.position + new Vector2(x, floor + 0.1f);
            var lib = director.Looks;
            var look = lib != null ? lib.enemyPirate : null;
            if (look == null) return;
            var c = director.SpawnFighter(world, Team.Enemy, "Contramestre", null, look);
            c.maxHp = c.hp = Mathf.RoundToInt(120 * (1f + danger * 0.15f));
            c.damage = new Vector2Int(11 + danger, 16 + danger);
            c.attackCooldown = 1.1f;
            c.FacingRight = false;
            c.transform.localScale = Vector3.one * 1.2f;
            Guard(c);
            var act = c.gameObject.AddComponent<IdleActivity>();
            act.kind = IdleActivity.Kind.Drink;
            act.prop = house.bottleRed;
        }

        /// <summary>
        /// Uma construção de lado: parede do fundo, pilares, viga, telhado de telhas e a placa com o nome.
        /// Tudo fica atrás dos personagens (dá para entrar). Devolve a altura do chão.
        /// </summary>
        float House(int col, int width, string title)
        {
            const float H = 4f;
            float floor = beachTop + tops[col];
            float x0 = col, x1 = col + width;
            // Parede do fundo
            var back = Sprite(house.back, new Vector2((x0 + x1) / 2f, floor + H / 2f), -38);
            back.drawMode = SpriteDrawMode.Tiled;
            back.size = new Vector2(width - 2f, H);
            back.name = "ParedeDoFundo";
            // Pilares dos lados
            foreach (float px in new[] { x0 + 0.5f, x1 - 0.5f })
                for (int r = 0; r < (int)H; r++)
                    Sprite(r == 0 ? house.pillarBase : r == (int)H - 1 ? house.pillarTop : house.pillar, new Vector2(px, floor + r + 0.5f), -26);
            // Viga de cima
            for (float bx = x0 + 1.5f; bx < x1 - 1f; bx += 1f)
                Sprite(bx < x0 + 2f ? house.beamLeft : bx > x1 - 2f ? house.beamRight : house.beam, new Vector2(bx, floor + H - 0.5f), -27);
            // Telhado (passa meio bloco para fora de cada lado)
            for (float rx = x0; rx <= x1 + 0.01f; rx += 1f)
                Sprite(rx < x0 + 0.5f ? house.roofLeft : rx > x1 - 0.5f ? house.roofRight : house.roof, new Vector2(rx, floor + H + 0.32f), -25);
            Sign(title, new Vector2((x0 + x1) / 2f, floor + H + 1.15f));
            return floor;
        }

        /// <summary>Mesa com comida e bebida, cadeiras, janela, barril e vela (Taverna).</summary>
        void Furnish(float cx, float floor, bool messy)
        {
            WallProp(house.window, cx, floor + 2.55f, -32, 0.7f);
            FloorProp(house.table, cx, floor, -29, 0.6f);
            float tableTop = floor + house.table.bounds.size.y * 0.6f;
            FloorProp(house.bottleRed, cx + 0.35f, tableTop - 0.04f, -28, 0.55f);
            FloorProp(house.fish, cx - 0.3f, tableTop - 0.06f, -28, 1f);
            if (messy) FloorProp(house.skull, cx + 0.05f, tableTop - 0.04f, -28, 0.7f);
            var left = FloorProp(house.chair, cx - 1.55f, floor, -30, 0.55f);
            var right = FloorProp(house.chair, cx + 1.55f, floor, -30, 0.55f);
            right.flipX = true;
            left.flipX = false;
            FloorProp(house.barrel, cx + 3.0f, floor, -28, 0.6f);
            Candle(cx - 2.5f, floor + 1.9f);
        }

        SpriteRenderer FloorProp(Sprite s, float x, float floor, int order, float scale)
        {
            if (s == null) return Sprite(null, new Vector2(x, floor), order);
            var sr = Sprite(s, new Vector2(x, floor + s.bounds.extents.y * scale), order);
            sr.transform.localScale = Vector3.one * scale;
            return sr;
        }

        SpriteRenderer WallProp(Sprite s, float x, float y, int order, float scale)
        {
            var sr = Sprite(s, new Vector2(x, y), order);
            sr.transform.localScale = Vector3.one * scale;
            return sr;
        }

        void Candle(float x, float y)
        {
            if (house.candle == null || house.candle.Length == 0) return;
            var sr = WallProp(house.candle[0], x, y, -29, 1f);
            var anim = sr.gameObject.AddComponent<SpriteAnim>();
            anim.frames = house.candle;
            anim.fps = 8f;
            anim.loop = true;
            Glow.Add(sr.transform, new Vector2(0, 0.25f), Glow.Candle, 3.2f, 1.1f);
        }

        /// <summary>Fogueira acesa: ilumina em volta (bem mais à noite).</summary>
        void Fire(int col)
        {
            var sr = Sprite(campfire, Top(col), -24);
            Glow.Add(sr.transform, new Vector2(0, 0.4f), Glow.Fire, 5.5f, 1.5f);
        }

        /// <summary>Placa de madeira com o nome da construção (texto de verdade, na fonte do jogo).</summary>
        void Sign(string text, Vector2 local)
        {
            var go = new GameObject("Placa_" + text, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(Root.transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = -24;
            var rt = (RectTransform)go.transform;
            rt.localPosition = local;
            rt.localScale = Vector3.one / 32f;
            rt.sizeDelta = new Vector2(10, 10);
            var board = UIKit.Rect("Tabua", rt);
            board.anchorMin = board.anchorMax = new Vector2(0.5f, 0.5f);
            var img = board.gameObject.AddComponent<UnityEngine.UI.Image>();
            if (UIKit.Skin.Button != null) UIKit.UseSkin(img, UIKit.Skin.Button, UnityEngine.UI.Image.Type.Sliced);
            img.pixelsPerUnitMultiplier = 1f;
            var h = UIKit.Horizontal(board.gameObject, 0, 0);
            h.padding = new RectOffset(8, 8, 3, 5);
            var fit = board.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var t = UIKit.Text(board, text, 12, TextAnchor.MiddleCenter, UIKit.ButtonText);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        /// <summary>Morador do porto (não luta): só come, bebe e conversa na taverna.</summary>
        void Villager(CharacterLook look, float x, float floor, IdleActivity.Kind kind, bool right, Sprite prop)
        {
            var go = new GameObject("Morador");
            go.transform.SetParent(Root.transform, false);
            go.transform.localPosition = new Vector2(x, floor);
            var fig = go.AddComponent<LookFigure>();
            fig.Init(look, 7);
            fig.Pose(false, true, right);
            var act = go.AddComponent<IdleActivity>();
            act.kind = kind;
            act.friendly = true;
            act.facingRight = right;
            act.prop = prop;
        }

        /// <summary>Pirata da vila fazendo alguma coisa (comendo, bebendo, dormindo, conversando) até ver o bando.</summary>
        void PirateAt(float x, float floor, int danger, IdleActivity.Kind kind, bool right, Sprite prop)
        {
            Vector2 world = (Vector2)Root.transform.position + new Vector2(x, floor + 0.1f);
            var lib = director.Looks;
            var c = lib != null && lib.enemyPirate != null
                ? director.SpawnFighter(world, Team.Enemy, "Pirata", null, lib.enemyPirate)
                : director.SpawnFighter(world, Team.Enemy, "Pirata", null, new Color32(40, 40, 45, 255), new Color32(160, 30, 30, 255));
            c.maxHp = c.hp = Mathf.RoundToInt(50 * (1f + danger * 0.15f));
            c.damage = new Vector2Int(7 + danger, 11 + danger);
            c.attackCooldown = 1.0f;
            c.FacingRight = right;
            Guard(c);
            var act = c.gameObject.AddComponent<IdleActivity>();
            act.kind = kind;
            act.facingRight = right;
            act.prop = prop;
        }

        /// <summary>Deixa as colunas em volta de "col" na mesma altura dela (fora da praia).</summary>
        void Flatten(int col, int radius)
        {
            int h = tops[col];
            for (int c = col - radius; c <= col + radius; c++)
                if (c >= 5 && c < Columns) tops[c] = h;
        }

        Vector2 GameFlowHome() => GameFlow.Instance != null ? GameFlow.Instance.World.Home.pos : Vector2.zero;

        int Take(List<int> free)
        {
            if (free.Count == 0) return Columns - 2;
            int i = rng.Next(free.Count);
            int col = free[i];
            free.RemoveAt(i);
            return col;
        }

        Vector2 Top(int col) => new Vector2(col + 0.5f, beachTop + tops[col]);

        const int DetailRows = 4;   // linhas com peças de borda abaixo da praia; mais fundo, enchimento repetido

        /// <summary>Tem terra na célula (coluna x, linha r)? Linha 0 = altura da praia. Fora das colunas é mar (bordas dos dois lados).</summary>
        bool Solid(int x, int r) => x >= 0 && x < Columns && r <= tops[x];

        int MaskAt(int x, int r)
        {
            bool N = Solid(x, r + 1), E = Solid(x + 1, r), S = Solid(x, r - 1), W = Solid(x - 1, r);
            int m = 0;
            if (N) m |= 1;
            if (N && E && Solid(x + 1, r + 1)) m |= 2;
            if (E) m |= 4;
            if (S && E && Solid(x + 1, r - 1)) m |= 8;
            if (S) m |= 16;
            if (S && W && Solid(x - 1, r - 1)) m |= 32;
            if (W) m |= 64;
            if (N && W && Solid(x - 1, r + 1)) m |= 128;
            return m;
        }

        void BuildColumn(int i)
        {
            var set = i < 5 ? sand : grass;
            float top = beachTop + tops[i];
            float bottom = waterline - 30f;
            // Peças com borda perto da superfície; a parte submersa fica atrás da água translúcida.
            for (int r = tops[i]; r >= -DetailRows; r--)
            {
                var sr = Sprite(set.Get(MaskAt(i, r)), new Vector2(i + 0.5f, beachTop + r - 0.5f), r == tops[i] ? -45 : -46);
                sr.name = r == tops[i] ? "Topo" : "Terra";
            }
            float fillTop = beachTop - DetailRows - 1f;
            var fill = Sprite(set.Get(MaskAt(i, -DetailRows - 2)), new Vector2(i + 0.5f, (fillTop + bottom) / 2f), -46);
            fill.drawMode = SpriteDrawMode.Tiled;
            fill.size = new Vector2(1f, fillTop - bottom);
            var box = fill.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, top - bottom);
            box.offset = new Vector2(0, (top - bottom) / 2f - (fillTop - bottom) / 2f);
            box.compositeOperation = Collider2D.CompositeOperation.Merge;
        }

        SpriteRenderer Sprite(Sprite s, Vector2 local, int order)
        {
            var sr = new GameObject("Peca").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(Root.transform, false);
            sr.transform.localPosition = local;
            sr.sprite = s;
            sr.sortingOrder = order;
            return sr;
        }

        void Decor(Sprite s, int col, int order, float scale)
        {
            var sr = Sprite(s, Top(col), order);
            sr.transform.localScale = Vector3.one * scale;
        }

        static (string, int, int)[] Items(params (string, int, int)[] list) => list;

        void Chest(int col, int gold, (string name, int min, int max)[] items, bool treasure = false)
        {
            var sr = Sprite(Node.looted ? chestOpen : chestClosed, Top(col), -5);
            if (treasure) sr.color = new Color(1f, 0.9f, 0.6f);
            if (Node.looted) return;
            chestsTotal++;
            var it = sr.gameObject.AddComponent<Interactable>();
            it.prompt = treasure ? "E: abrir o baú do TESOURO" : "E: abrir o baú";
            it.onUse = () =>
            {
                var loot = new Dictionary<string, int>();
                foreach (var (name, min, max) in items) loot[name] = rng.Next(min, max + 1);
                sea.AddLoot(gold, loot);
                string txt = $"+{gold} ouro";
                foreach (var kv in loot) txt += $", {kv.Value} {kv.Key}";
                sea.Say((treasure ? "TESOURO! " : "Baú aberto: ") + txt);
                Fx.Spawn(Fx.Puff, sr.transform.position + Vector3.up * 0.6f, 0.5f, 1.6f);
                sr.sprite = chestOpen;
                GameStats.Add(GameStats.Chests);
                AudioManager.Play(Sfx.Chest);
                Destroy(it);
                if (++chestsOpened >= chestsTotal) Node.looted = true;
            };
            if (treasure) Bury(sr, it);
        }

        // ---------- Tesouro enterrado: um X na areia; cave em cima dele (botão Cavar ou G) ----------
        SpriteRenderer buriedChest, digMark;
        Interactable buriedUse;
        static Sprite xSprite;

        public bool HasBuriedTreasure => buriedChest != null;
        public bool NearDigMark(float worldX) => digMark != null && Mathf.Abs(digMark.transform.position.x - worldX) <= 0.9f;

        void Bury(SpriteRenderer chest, Interactable use)
        {
            chest.enabled = false;
            use.enabled = false;
            buriedChest = chest;
            buriedUse = use;
            digMark = Sprite(XSprite(), (Vector2)chest.transform.localPosition + Vector2.up * 0.02f, -4);
            digMark.transform.localScale = Vector3.one * 1.6f;
        }

        /// <summary>Cavar aqui: se for em cima do X, o baú do tesouro aparece.</summary>
        public bool TryDig(float worldX)
        {
            if (!NearDigMark(worldX) || buriedChest == null) return false;
            buriedChest.enabled = true;
            buriedUse.enabled = true;
            Fx.Spawn(Fx.Puff, buriedChest.transform.position + Vector3.up * 0.4f, 0.6f, 2f);
            Destroy(digMark.gameObject);
            buriedChest = null;
            buriedUse = null;
            digMark = null;
            return true;
        }

        /// <summary>X vermelho pintado na areia (16x10, 32 px por bloco, pivô embaixo).</summary>
        static Sprite XSprite()
        {
            if (xSprite != null) return xSprite;
            const int W = 16, H = 10;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            var red = new Color32(196, 38, 38, 255);
            var dark = new Color32(70, 22, 22, 255);
            bool Line(int x, int y)
            {
                for (int i = 0; i < 8; i++)
                {
                    int ax = 2 + i * 12 / 8, ay = 1 + i;
                    if ((x == ax || x == ax + 1) && y == ay) return true;
                    int bx = 12 - i * 12 / 8;
                    if ((x == bx || x == bx + 1) && y == ay) return true;
                }
                return false;
            }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (Line(x, y)) px[y * W + x] = red;
                    else if (Line(x - 1, y) || Line(x + 1, y) || Line(x, y - 1) || Line(x, y + 1)) px[y * W + x] = dark;
                }
            tex.SetPixels32(px);
            tex.Apply();
            xSprite = UnityEngine.Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 32f);
            return xSprite;
        }

        void Merchant(int col)
        {
            var go = new GameObject("Comerciante");
            go.transform.SetParent(Root.transform, false);
            go.transform.localPosition = Top(col) + new Vector2(-0.8f, 0);
            var lib = director.Looks;
            if (lib != null && lib.visitors != null && lib.visitors.Length > 0)
            {
                var fig = go.AddComponent<LookFigure>();
                fig.Init(lib.visitors[Node.seed % lib.visitors.Length], -26);   // atrás do balcão da barraca
                fig.Pose(false, true, false);
            }
            else
            {
                var sprites = CrewManager.Instance != null ? CrewManager.Instance.sprites : director.crewSprites;
                var fig = go.AddComponent<LayeredFigure>();
                fig.Init(sprites, new Color32(232, 178, 128, 255), new Color32(200, 160, 60, 255), new Color32(90, 55, 30, 255), 7);
                fig.Pose(false, true, false);
            }
            var it = go.AddComponent<Interactable>();
            it.prompt = "E: negociar com o comerciante";
            it.range = 1.6f;
            it.onUse = sea.OpenTrade;
        }

        void Crab(int col, int danger)
        {
            Vector2 world = (Vector2)Root.transform.position + Top(col) + Vector2.up * 0.1f;
            var kind = creatures[rng.Next(creatures.Length)];
            var c = director.SpawnCreature(world, kind.name, kind.frames);
            c.maxHp = c.hp = Mathf.RoundToInt(25 * (1f + danger * 0.15f));
            c.damage = new Vector2Int(3, 6 + danger);
            c.attackCooldown = 1.1f;
            c.attackRange = 0.9f;
            Guard(c);
        }

        void Pirate(int col, int danger)
        {
            Vector2 world = (Vector2)Root.transform.position + Top(col) + Vector2.up * 0.1f;
            var lib = director.Looks;
            var c = lib != null && lib.enemyPirate != null
                ? director.SpawnFighter(world, Team.Enemy, "Pirata", null, lib.enemyPirate)
                : director.SpawnFighter(world, Team.Enemy, "Pirata", null, new Color32(40, 40, 45, 255), new Color32(160, 30, 30, 255));
            c.maxHp = c.hp = Mathf.RoundToInt(50 * (1f + danger * 0.15f));
            c.damage = new Vector2Int(7 + danger, 11 + danger);
            c.attackCooldown = 1.0f;
            c.FacingRight = false;
            Guard(c);
        }

        void Guard(Combatant c)
        {
            var ai = c.GetComponent<CombatAI>();
            ai.regionMin = RegionMin;
            ai.regionMax = RegionMax;
            ai.waterlineY = waterline;
            ai.homeX = c.transform.position.x;
            enemies.Add(c);
        }

        // ---------- Chegada e partida ----------
        void Update()
        {
            if (departing != null)
            {
                // Seguindo em frente: a ilha fica para trás (esquerda). Voltando pra casa: sai pela direita.
                float speed = Mathf.Max(8f, sea.scroller != null ? sea.scroller.speed * 1.5f : 8f);
                departing.transform.position += Vector3.right * (departDir * speed * Time.deltaTime);
                float x = departing.transform.position.x;
                bool gone = departDir > 0 ? x > sea.shipView.RightEdge + 60f : x + Columns < sea.shipView.LeftEdge - 30f;
                if (gone) { Destroy(departing); departing = null; }
            }
            if (Root == null) return;
            if (!Arrived)
            {
                var p = Root.transform.position;
                p.x = Mathf.MoveTowards(p.x, targetX, 9f * Time.deltaTime);
                Root.transform.position = p;
                if (Mathf.Abs(p.x - targetX) < 0.01f) Land();
                return;
            }
            if (!clearedAnnounced && enemies.Count > 0 && AllEnemiesDown())
            {
                clearedAnnounced = true;
                Node.cleared = true;
                sea.Say($"{Node.name} está livre de inimigos! Abra os baús em paz.");
            }
        }

        bool AllEnemiesDown()
        {
            foreach (var e in enemies) if (e != null && !e.IsDown) return false;
            return true;
        }

        void Land()
        {
            Arrived = true;
            gangplank = Gangplank.Create(sea.shipView.EdgeDeck(true), new Vector2(Root.transform.position.x + 0.1f, beachTop), plank, "PranchaDaPraia");
            foreach (var (_, spawn) in pendingSpawns) spawn();
            pendingSpawns.Clear();
            sea.OnIslandReady(this);
        }

        public bool HasLiveEnemies => enemies.Exists(e => e != null && !e.IsDown);

        /// <summary>Sandbox: derruba todos os inimigos da ilha.</summary>
        public int SandboxKillAll()
        {
            int n = 0;
            foreach (var e in enemies) if (e != null && !e.IsDown) { e.TakeDamage(99999, null); n++; }
            return n;
        }

        /// <summary>Sai da ilha: some com a prancha e os inimigos; a ilha fica para trás.</summary>
        /// <param name="towardsHome">Voltando pra Enseada: a ilha sai pela direita (o navio dá meia-volta).</param>
        public void Leave(bool towardsHome = false)
        {
            if (gangplank != null) Destroy(gangplank);
            gangplank = null;
            foreach (var e in enemies) if (e != null) Destroy(e.gameObject);
            enemies.Clear();
            pendingSpawns.Clear();
            if (Root != null)
            {
                foreach (var it in Root.GetComponentsInChildren<Interactable>()) Destroy(it);
                // Sem colisão na hora: nada de arrastar a tripulação nem segurar navios
                foreach (var col in Root.GetComponentsInChildren<Collider2D>()) col.enabled = false;
                // Passa por trás do navio sem cobrir nada
                foreach (var sr in Root.GetComponentsInChildren<SpriteRenderer>()) sr.sortingOrder -= 40;
                departDir = towardsHome ? 1f : -1f;
                if (departing != null) Destroy(departing);
                departing = Root;
            }
            Root = null;
            Arrived = false;
        }
    }
}
