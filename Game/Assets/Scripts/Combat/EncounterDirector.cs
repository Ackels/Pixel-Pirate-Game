using System;
using System.Collections.Generic;
using Pixel.Ship;
using Pixel.Village;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Pixel.Combat
{
    public enum EncounterState { None, Approaching, Engaged, Closing, Boarding, Victory, Sinking, Fled, Defeat }

    /// <summary>
    /// Encontros no mar: um navio aparece, os canhões trocam tiros, o jogador pode abordar
    /// (prancha + luta corpo a corpo) ou fugir. Vitória dá saque; derrota manda de volta pra vila.
    /// </summary>
    public class EncounterDirector : MonoBehaviour
    {
        public static EncounterDirector Instance { get; private set; }

        public SeaController sea;
        public SeaCamera seaCamera;

        [Header("Ritmo")]
        [Tooltip("Chance base de encontro por trecho de viagem (+5% por nível de perigo).")]
        public float encounterChance = 0.55f;
        public float holdGap = 7f;

        [Header("Visual")]
        public Sprite flagPirate, flagMerchant, plankSprite;
        public Sprite fxPuff, fxBoom, fxSplash, fxSlash, fxCannonball;
        public CrewManager.LayerSprites crewSprites;

        public EncounterState State { get; private set; }
        public bool Busy => State != EncounterState.None;
        public bool CanBoard => State == EncounterState.Engaged;
        public bool CanFlee => State == EncounterState.Approaching || State == EncounterState.Engaged;
        public string EnemyName { get; private set; }
        public int EnemyHp { get; private set; }
        public int EnemyMaxHp { get; private set; }
        public int EnemyCrewAlive
        {
            get { int n = 0; foreach (var c in enemyCrew) if (c != null && !c.IsDown) n++; return n; }
        }
        public event Action Changed;

        ShipView enemyShip;
        readonly List<Combatant> enemyCrew = new List<Combatant>();
        readonly List<float> playerReload = new List<float>(), enemyReload = new List<float>();
        bool pirate;
        float timer = float.PositiveInfinity, stateTimer, enemySpeedKnots, holdX;
        int legDanger;
        GameObject bridge;
        int encounters;

        void Awake()
        {
            Instance = this;
            Fx.Puff = fxPuff; Fx.Boom = fxBoom; Fx.Splash = fxSplash; Fx.Slash = fxSlash; Fx.Cannonball = fxCannonball;
        }

        /// <summary>Sorteia se vai haver um encontro neste trecho e quando.</summary>
        public void ScheduleLeg(float legSeconds, int danger)
        {
            legDanger = danger;
            timer = Random.value < encounterChance + danger * 0.05f
                ? Mathf.Max(3f, legSeconds * Random.Range(0.25f, 0.6f))
                : float.PositiveInfinity;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Say(string msg) => sea.Say(msg);

        void SetState(EncounterState s)
        {
            State = s;
            stateTimer = 0f;
            Changed?.Invoke();
        }

        void Update()
        {
            stateTimer += Time.deltaTime;
            switch (State)
            {
                case EncounterState.None:
                    // Só aparece navio com o mar livre (sem ilha chegando ou indo embora)
                    if (sea.Mode == SeaMode.Sailing && (sea.islands == null || !sea.islands.Present))
                    {
                        timer -= Time.deltaTime;
                        if (timer <= 0f) SpawnEnemy();
                    }
                    if (seaCamera)
                    {
                        // Em terra, a câmera acompanha o capitão pela ilha
                        bool landed = sea.Mode == SeaMode.Landed && sea.islands != null && sea.islands.Root != null;
                        seaCamera.targetX = landed && sea.captain != null
                            ? Mathf.Clamp(sea.captain.transform.position.x - 4f, 0f, Mathf.Max(0f, sea.islands.RegionMax - 18f))
                            : 0f;
                    }
                    break;

                case EncounterState.Approaching:
                    MoveEnemyTowards(holdX, 6f);
                    if (Mathf.Abs(enemyShip.origin.x - holdX) < 0.05f)
                    {
                        SpawnEnemyCrew();
                        SetState(EncounterState.Engaged);
                        Say(pirate ? "Piratas! Eles vão atirar — e abordar se você fraquejar." : "Ao alcance dos canhões! Clique em Abordar para tomar a carga.");
                    }
                    break;

                case EncounterState.Engaged:
                    FireCannons();
                    if (pirate && (sea.ShipHp < sea.Stats.hp / 2 || stateTimer > 25f))
                    {
                        Say("Os piratas estão vindo abordar o seu navio!");
                        StartClosing();
                    }
                    break;

                case EncounterState.Closing:
                    float boardX = sea.shipView.RightEdge + 1.4f - (enemyShip.LeftEdge - enemyShip.origin.x);
                    MoveEnemyTowards(boardX, 4f);
                    if (Mathf.Abs(enemyShip.origin.x - boardX) < 0.05f) StartBoarding();
                    break;

                case EncounterState.Boarding:
                    if (EnemyCrewAlive == 0) Win();
                    break;

                case EncounterState.Victory:
                    if (stateTimer > 2.5f) MoveEnemyTowards(enemyShip.origin.x + 30f, 5f);
                    if (stateTimer > 6f) EndEncounter();
                    break;

                case EncounterState.Sinking:
                    enemyShip.transform.position += Vector3.down * (1.6f * Time.deltaTime);
                    enemyShip.transform.rotation = Quaternion.Euler(0, 0, Mathf.Min(stateTimer * 6f, 18f));
                    if (stateTimer > 4f) EndEncounter();
                    break;

                case EncounterState.Fled:
                    MoveEnemyTowards(enemyShip.origin.x + 40f, 8f);
                    if (stateTimer > 3f) EndEncounter();
                    break;

                case EncounterState.Defeat:
                    if (stateTimer > 2.5f) { SetState(EncounterState.None); sea.Defeat(); }
                    break;
            }

            if (State != EncounterState.None && State != EncounterState.Defeat)
            {
                if (sea.ShipHp <= 0) Lose("O seu navio foi a pique!");
                else if (sea.captainCombatant != null && sea.captainCombatant.IsDown) Lose("O capitão caiu em combate!");
            }

            if (enemyShip != null && seaCamera != null && State != EncounterState.None)
                seaCamera.targetX = Mathf.Max(0f, (sea.shipView.LeftEdge + enemyShip.RightEdge) / 2f);
        }

        // ---------- Inimigo ----------
        void SpawnEnemy()
        {
            encounters++;
            pirate = encounters > 1 && Random.value < Mathf.Clamp01(0.15f + legDanger * 0.15f);
            EnemyName = pirate ? "Navio Pirata" : "Navio Mercante";
            enemySpeedKnots = pirate ? 7.5f : 5f;

            var layout = pirate ? PirateLayout() : MerchantLayout();
            var go = new GameObject(EnemyName, typeof(Rigidbody2D), typeof(CompositeCollider2D));
            go.SetActive(false);
            var view = go.AddComponent<ShipView>();
            view.movable = true;
            view.facingLeft = true;
            view.sailTint = pirate ? new Color(0.35f, 0.32f, 0.32f) : new Color(0.85f, 0.92f, 1f);
            go.SetActive(true);
            view.Bind(layout);

            view.TryGetBounds(out int minX, out _);
            holdX = sea.shipView.RightEdge + holdGap - minX;
            float startX = holdX + 30f;
            go.transform.position = new Vector3(startX, sea.shipView.origin.y, 0);
            enemyShip = view;
            AddFlag(view, layout);

            var stats = layout.ComputeStats();
            EnemyMaxHp = EnemyHp = Mathf.RoundToInt(stats.hp * (pirate ? 0.35f : 0.45f));
            playerReload.Clear(); enemyReload.Clear();
            foreach (var _ in sea.shipView.CannonMuzzles()) playerReload.Add(Random.Range(1f, 2.5f));
            foreach (var _ in view.CannonMuzzles()) enemyReload.Add(Random.Range(1.5f, 3f));

            SetState(EncounterState.Approaching);
            Say(pirate ? "Bandeira negra no horizonte! Navio pirata!" : "Navio mercante à vista! Carga gorda...");
        }

        void AddFlag(ShipView view, ShipLayout layout)
        {
            Vector2Int top = new Vector2Int(-1, -1);
            foreach (var kv in layout.Blocks)
                if (kv.Value.role == BlockRole.Mast && kv.Key.y > top.y) top = kv.Key;
            if (top.x < 0) return;
            var f = new GameObject("Bandeira").AddComponent<SpriteRenderer>();
            f.transform.SetParent(view.transform, false);
            f.transform.localPosition = (Vector2)top + new Vector2(0.5f, 1.5f);
            f.sprite = pirate ? flagPirate : flagMerchant;
            f.flipX = true;
            f.sortingOrder = -9;
        }

        ShipBlockDef Def(BlockRole role)
        {
            foreach (var d in sea.Palette) if (d.role == role) return d;
            return sea.Palette[0];
        }

        ShipLayout MerchantLayout()
        {
            var l = new ShipLayout();
            var hull = Def(BlockRole.Hull); var mast = Def(BlockRole.Mast); var sail = Def(BlockRole.Sail);
            for (int x = 8; x <= 14; x++) l.Set(new Vector2Int(x, 1), hull, false);
            for (int x = 7; x <= 15; x++) l.Set(new Vector2Int(x, 2), hull, false);
            for (int y = 3; y <= 6; y++) l.Set(new Vector2Int(11, y), mast, false);
            for (int y = 4; y <= 6; y++) { l.Set(new Vector2Int(10, y), sail, false); l.Set(new Vector2Int(12, y), sail, false); }
            l.Set(new Vector2Int(8, 3), Def(BlockRole.Cannon), false);
            l.Set(new Vector2Int(14, 3), Def(BlockRole.Cabin), false);
            return l;
        }

        ShipLayout PirateLayout()
        {
            var l = new ShipLayout();
            var hull = Def(BlockRole.Hull); var mast = Def(BlockRole.Mast); var sail = Def(BlockRole.Sail);
            for (int x = 8; x <= 15; x++) l.Set(new Vector2Int(x, 0), hull, false);
            for (int x = 6; x <= 17; x++) l.Set(new Vector2Int(x, 1), hull, false);
            for (int x = 5; x <= 18; x++) l.Set(new Vector2Int(x, 2), hull, false);
            foreach (int mx in new[] { 9, 14 })
            {
                for (int y = 3; y <= 7; y++) l.Set(new Vector2Int(mx, y), mast, false);
                for (int y = 4; y <= 7; y++) { l.Set(new Vector2Int(mx - 1, y), sail, false); l.Set(new Vector2Int(mx + 1, y), sail, false); }
            }
            l.Set(new Vector2Int(6, 3), Def(BlockRole.Cannon), false);
            l.Set(new Vector2Int(7, 3), Def(BlockRole.Cannon), false);
            l.Set(new Vector2Int(16, 3), Def(BlockRole.Cabin), false);
            l.Set(new Vector2Int(17, 3), Def(BlockRole.Cabin), false);
            return l;
        }

        void MoveEnemyTowards(float x, float speed)
        {
            if (enemyShip == null) return;
            var p = enemyShip.origin;
            float nx = Mathf.MoveTowards(p.x, x, speed * Time.deltaTime);
            float dx = nx - p.x;
            enemyShip.transform.position = new Vector3(nx, p.y, 0);
            // Quem está em pé no navio inimigo anda junto com ele (senão o navio "escorrega" por baixo)
            if (Mathf.Abs(dx) > 0f)
                foreach (var c in enemyCrew)
                {
                    if (c == null || c.IsDown || !enemyShip.ContainsX(c.transform.position.x - dx)) continue;
                    var rb = c.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.position += new Vector2(dx, 0);
                    c.transform.position += new Vector3(dx, 0, 0);
                    var ai = c.GetComponent<CombatAI>();
                    if (ai != null) ai.homeX += dx;
                }
        }

        void SpawnEnemyCrew()
        {
            enemyCrew.Clear();
            int count = pirate ? Random.Range(3, 6) : Random.Range(2, 4);
            for (int i = 0; i < count; i++)
            {
                var spawn = enemyShip.SpawnPoint(Random.Range(-2.5f, 2.5f)) + Vector2.up * 0.2f;
                var c = SpawnFighter(spawn, Team.Enemy, pirate ? "Pirata" : "Marinheiro", enemyShip,
                    pirate ? new Color32(40, 40, 45, 255) : new Color32(70, 110, 190, 255),
                    pirate ? new Color32(160, 30, 30, 255) : new Color32(90, 60, 30, 255));
                c.maxHp = c.hp = pirate ? 55 : 35;
                c.damage = pirate ? new Vector2Int(8, 12) : new Vector2Int(5, 8);
                c.attackCooldown = pirate ? 0.85f : 1f;
                c.FacingRight = false;
                enemyCrew.Add(c);
            }
        }

        public Combatant SpawnFighter(Vector2 pos, Team team, string name, ShipView home, Color outfit, Color hair, Color? skin = null)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var capsule = go.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.6f, 1.35f);
            capsule.offset = new Vector2(0, 0.68f);
            var captainCol = sea.captain != null ? sea.captain.GetComponent<Collider2D>() : null;
            if (captainCol != null) capsule.sharedMaterial = captainCol.sharedMaterial;

            var fig = go.AddComponent<LayeredFigure>();
            var sprites = CrewManager.Instance != null ? CrewManager.Instance.sprites : crewSprites;
            Color[] skins = { new Color32(250, 214, 175, 255), new Color32(232, 178, 128, 255), new Color32(190, 130, 85, 255), new Color32(120, 80, 52, 255) };
            fig.Init(sprites, skin ?? skins[Random.Range(0, skins.Length)], outfit, hair, team == Team.Player ? 9 : 8);

            var c = go.AddComponent<Combatant>();
            c.team = team;
            c.displayName = name;
            c.knockOutInsteadOfDeath = team == Team.Player;
            var ai = go.AddComponent<CombatAI>();
            ai.homeShip = home;
            ai.homeX = pos.x;
            ai.charge = false;
            return c;
        }

        /// <summary>Cria um bicho (ex.: caranguejo) que luta, com sprite simples.</summary>
        public Combatant SpawnCreature(Vector2 pos, string name, Sprite[] frames)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.7f, 0.5f);
            box.offset = new Vector2(0, 0.25f);
            var captainCol = sea.captain != null ? sea.captain.GetComponent<Collider2D>() : null;
            if (captainCol != null) box.sharedMaterial = captainCol.sharedMaterial;
            go.AddComponent<SimpleFigure>().Init(frames, 8);
            var c = go.AddComponent<Combatant>();
            c.team = Team.Enemy;
            c.displayName = name;
            var ai = go.AddComponent<CombatAI>();
            ai.homeX = pos.x;
            ai.moveSpeed = 2.6f;
            ai.jumpSpeed = 8f;
            return c;
        }

        // ---------- Canhões ----------
        void FireCannons()
        {
            var pm = sea.shipView.CannonMuzzles();
            for (int i = 0; i < pm.Count && i < playerReload.Count; i++)
            {
                playerReload[i] -= Time.deltaTime;
                if (playerReload[i] > 0) continue;
                playerReload[i] = Random.Range(2.6f, 3.4f);
                bool hit = Random.value < 0.7f;
                Vector2 target = hit ? enemyShip.RandomBlockPoint() : new Vector2(enemyShip.CenterX + Random.Range(-6f, 6f), enemyShip.WaterlineY - 0.2f);
                Cannonball.Fire(pm[i], target, hit, () => { if (enemyShip != null) DamageEnemy(Random.Range(8, 13)); });
            }

            var em = enemyShip.CannonMuzzles();
            for (int i = 0; i < em.Count && i < enemyReload.Count; i++)
            {
                enemyReload[i] -= Time.deltaTime;
                if (enemyReload[i] > 0) continue;
                enemyReload[i] = Random.Range(2.8f, 3.8f);
                bool hit = Random.value < 0.6f;
                Vector2 target = hit ? sea.shipView.RandomBlockPoint() : new Vector2(sea.shipView.CenterX + Random.Range(-6f, 6f), sea.shipView.WaterlineY - 0.2f);
                int dmg = pirate ? Random.Range(9, 14) : Random.Range(6, 10);
                Cannonball.Fire(em[i], target, hit, () => { sea.DamageShip(dmg); if (seaCamera) seaCamera.Shake(0.35f); });
            }
        }

        void DamageEnemy(int dmg)
        {
            if (State != EncounterState.Engaged && State != EncounterState.Approaching) return;
            EnemyHp = Mathf.Max(0, EnemyHp - dmg);
            Changed?.Invoke();
            if (EnemyHp <= 0) Sink();
        }

        // ---------- Ações do jogador ----------
        public void Board()
        {
            if (!CanBoard) return;
            Say("Preparar ganchos! Encostando para a abordagem...");
            StartClosing();
        }

        void StartClosing() => SetState(EncounterState.Closing);

        void StartBoarding()
        {
            bridge = Gangplank.Create(sea.shipView.EdgeDeck(true), enemyShip.EdgeDeck(false), plankSprite, "PranchaDeAbordagem");

            foreach (var c in sea.CrewFighters) if (c != null) { var ai = c.GetComponent<CombatAI>(); if (ai) ai.charge = true; }
            if (pirate) foreach (var c in enemyCrew) if (c != null) { var ai = c.GetComponent<CombatAI>(); if (ai) ai.charge = true; }
            SetState(EncounterState.Boarding);
            Say("ABORDAR! Derrube a tripulação inimiga! (F, J ou clique para golpear)");
        }

        public void Flee()
        {
            if (!CanFlee) return;
            if (sea.Stats.speedKnots > enemySpeedKnots)
            {
                Say($"Fugimos! Nosso navio ({sea.Stats.speedKnots:0.0} nós) é mais rápido.");
                ClearEnemyCrew();
                SetState(EncounterState.Fled);
            }
            else Say($"Não dá pra fugir: eles fazem {enemySpeedKnots:0.0} nós e nós só {sea.Stats.speedKnots:0.0}. Ponha mais velas!");
        }

        // ---------- Resultados ----------
        void Win()
        {
            int gold = pirate ? Random.Range(80, 161) : Random.Range(35, 71);
            var items = new Dictionary<string, int> { { "Rum", pirate ? Random.Range(4, 11) : Random.Range(2, 7) } };
            if (!pirate) { items["Peixe"] = Random.Range(4, 11); items["Cana"] = Random.Range(3, 9); }
            sea.AddLoot(gold, items);
            GameStats.Add(GameStats.SeaVictories);
            AudioManager.Play(Sfx.Chest);
            Say($"VITÓRIA! O {EnemyName.ToLower()} se rendeu. Saque: {gold} ouro{ItemsText(items)}.");
            RemoveBridge();
            sea.ReviveCrew();
            SetState(EncounterState.Victory);
        }

        void Sink()
        {
            int gold = (pirate ? Random.Range(80, 161) : Random.Range(35, 71)) / 2;
            sea.AddLoot(gold, new Dictionary<string, int>());
            GameStats.Add(GameStats.SeaVictories);
            Say($"O {EnemyName.ToLower()} afundou! Você recolheu {gold} ouro dos destroços (abordando ganharia mais).");
            ClearEnemyCrew();
            SetState(EncounterState.Sinking);
        }

        void Lose(string why)
        {
            Say($"DERROTA! {why} Recuando para a Enseada...");
            RemoveBridge();
            SetState(EncounterState.Defeat);
        }

        void EndEncounter()
        {
            ClearEnemyCrew();
            RemoveBridge();
            if (enemyShip != null) Destroy(enemyShip.gameObject);
            enemyShip = null;
            sea.ReviveCrew();
            foreach (var c in sea.CrewFighters) if (c != null) { var ai = c.GetComponent<CombatAI>(); if (ai) ai.charge = false; }
            timer = float.PositiveInfinity;   // no máximo um encontro por trecho
            SetState(EncounterState.None);
        }

        void ClearEnemyCrew()
        {
            foreach (var c in enemyCrew) if (c != null) Destroy(c.gameObject);
            enemyCrew.Clear();
        }

        void RemoveBridge()
        {
            if (bridge != null) Destroy(bridge);
            bridge = null;
        }

        static string ItemsText(Dictionary<string, int> items)
        {
            string s = "";
            foreach (var kv in items) s += $", {kv.Value} {kv.Key}";
            return s;
        }
    }
}
