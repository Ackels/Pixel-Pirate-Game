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

        [Header("Sprites")]
        public Sprite grassTop, dirt, sandTop, sand, chestClosed, chestOpen, tent, campfire, palm, market, plank;
        public Sprite[] crabFrames;

        public const int Columns = 30;
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
            beachTop = waterline + 0.5f;
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
                BuildColumn(i, beachTop + tops[i], i < 5);
            }

            var free = new List<int>();
            for (int i = 6; i < Columns - 1; i++) free.Add(i);
            int danger = node.Danger(GameFlowHome());

            // Coqueiros de enfeite
            for (int k = 0; k < 4; k++) Decor(palm, Take(free), -36, 1f);

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
                    int tentCol = Columns - 8;
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
                    int stallCol = 12;
                    Decor(market, stallCol, -25, 1f);
                    Merchant(stallCol);
                    break;
            }

            if (node.hasTreasureHint && !node.looted) Chest(Columns - 3, rng.Next(120, 201), Items(("Rum", 3, 6)), true);
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

        void BuildColumn(int i, float top, bool isSand)
        {
            float bottom = waterline - 30f;
            // Topo na frente da água de trás (-49); o enchimento fica atrás dela (-50),
            // então a parte submersa da ilha some no mar e a praia continua visível.
            var topSr = Sprite(isSand ? sandTop : grassTop, new Vector2(i + 0.5f, top - 0.5f), -45);
            topSr.name = "Topo";
            var fill = Sprite(isSand ? sand : dirt, new Vector2(i + 0.5f, (top - 1f + bottom) / 2f), -60);
            fill.drawMode = SpriteDrawMode.Tiled;
            fill.size = new Vector2(1f, top - 1f - bottom);
            var box = fill.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, top - bottom);
            box.offset = new Vector2(0, 0.5f);
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
                Destroy(it);
                if (++chestsOpened >= chestsTotal) Node.looted = true;
            };
        }

        void Merchant(int col)
        {
            var go = new GameObject("Comerciante");
            go.transform.SetParent(Root.transform, false);
            go.transform.localPosition = Top(col) + new Vector2(-0.8f, 0);
            var sprites = CrewManager.Instance != null ? CrewManager.Instance.sprites : director.crewSprites;
            var fig = go.AddComponent<LayeredFigure>();
            fig.Init(sprites, new Color32(232, 178, 128, 255), new Color32(200, 160, 60, 255), new Color32(90, 55, 30, 255), 7);
            fig.Pose(false, true, false);
            var it = go.AddComponent<Interactable>();
            it.prompt = "E: negociar com o comerciante";
            it.range = 1.6f;
            it.onUse = sea.OpenTrade;
        }

        void Crab(int col, int danger)
        {
            Vector2 world = (Vector2)Root.transform.position + Top(col) + Vector2.up * 0.1f;
            var c = director.SpawnCreature(world, "Caranguejo", crabFrames);
            c.maxHp = c.hp = Mathf.RoundToInt(25 * (1f + danger * 0.15f));
            c.damage = new Vector2Int(3, 6 + danger);
            c.attackCooldown = 1.1f;
            c.attackRange = 0.9f;
            Guard(c);
        }

        void Pirate(int col, int danger)
        {
            Vector2 world = (Vector2)Root.transform.position + Top(col) + Vector2.up * 0.1f;
            var c = director.SpawnFighter(world, Team.Enemy, "Pirata", null, new Color32(40, 40, 45, 255), new Color32(160, 30, 30, 255));
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
