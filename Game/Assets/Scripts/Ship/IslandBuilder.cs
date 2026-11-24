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
        [Tooltip("Bichos das ilhas selvagens (um é sorteado para cada inimigo).")]
        public Creature[] creatures;

        public const int Columns = 30;
        const int CampTentCol = Columns - 8, PortStallCol = 12;
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
            // Chão plano embaixo das construções largas (senão ficam com um pedaço no ar)
            if (node.type == IslandType.PirateCamp) { Flatten(CampTentCol, 2); Flatten(CampTentCol - 3, 1); }
            if (node.type == IslandType.Port) Flatten(PortStallCol, 2);
            for (int i = 0; i < Columns; i++) BuildColumn(i);

            var free = new List<int>();
            for (int i = 6; i < Columns - 1; i++) free.Add(i);
            int danger = node.Danger(GameFlowHome());

            // Coqueiros de enfeite (na frente e no fundo) e tufos de grama
            for (int k = 0; k < 4; k++) Decor(palm, Take(free), -36, 1f);
            if (backPalms != null && backPalms.Length > 0)
                for (int k = 0; k < 5; k++)
                {
                    int col = 6 + rng.Next(Columns - 7);
                    Decor(backPalms[rng.Next(backPalms.Length)], col, -50, 1f);
                }
            if (grassDecor != null)
                for (int k = 0; k < 6; k++) Decor(grassDecor, 6 + rng.Next(Columns - 8), -44, 1f);

            switch (node.type)
            {
                case IslandType.Wild:
                    for (int k = 0; k < rng.Next(2, 4); k++) Chest(Take(free), rng.Next(10, 31), Items(("Peixe", 2, 5), ("Cana", 2, 5)));
                    if (!node.cleared)
                        for (int k = 0; k < rng.Next(2, 5) + danger / 2; k++)
                        {
                            int col = Take(free);
                            pendingSpawns.Add((Top(col), () => Crab(col, danger)));
                        }
                    break;

                case IslandType.PirateCamp:
                    int tentCol = CampTentCol;
                    Decor(tent, tentCol, -25, 1f);
                    Decor(campfire, tentCol - 3, -24, 1f);
                    free.Remove(tentCol); free.Remove(tentCol - 3);
                    for (int k = 0; k < rng.Next(2, 5); k++) Chest(Take(free), rng.Next(30, 71), Items(("Rum", 2, 5)));
                    if (!node.cleared)
                        for (int k = 0; k < 2 + rng.Next(0, 2) + danger / 2; k++)
                        {
                            int col = Take(free);
                            pendingSpawns.Add((Top(col), () => Pirate(col, danger)));
                        }
                    break;

                case IslandType.Port:
                    int stallCol = PortStallCol;
                    Decor(market, stallCol, -25, 1f);
                    Merchant(stallCol);
                    break;
            }

            if (node.hasTreasureHint && !node.looted) Chest(Columns - 3, rng.Next(120, 201), Items(("Rum", 3, 6)), true);
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
