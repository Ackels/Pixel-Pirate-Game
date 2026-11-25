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
        [Tooltip("Distância (blocos) entre os dois navios no combate. Como no Pixel Piracy, bem afastados.")]
        public float shipGap = 11f;

        [Header("Visual")]
        public Sprite flagPirate, flagMerchant, plankSprite;
        public Sprite fxPuff, fxBoom, fxSplash, fxSlash, fxCannonball;
        public CrewManager.LayerSprites crewSprites;
        [Tooltip("Visuais dos personagens (usado se a vila não estiver carregada).")]
        public CharacterLibrary looks;

        /// <summary>Catálogo de visuais (o da vila, se existir).</summary>
        public CharacterLibrary Looks => CrewManager.Instance != null && CrewManager.Instance.looks != null ? CrewManager.Instance.looks : looks;

        public EncounterState State { get; private set; }
        public bool Busy => State != EncounterState.None;
        /// <summary>Abordar só depois de amolecer o inimigo com os canhões (sem canhão no navio: depois de um tempo).</summary>
        public const float BoardHullFraction = 0.6f;
        /// <summary>Abordar a qualquer hora (como no Pixel Piracy). Com muitos inimigos é arriscado: os canhões derrubam tripulantes deles.</summary>
        public bool CanBoard => State == EncounterState.Engaged;
        public string BoardHint => null;
        public bool CanFlee => State == EncounterState.Approaching || State == EncounterState.Engaged;
        /// <summary>Os seus canhões estão atirando (botão Atirar / Cessar fogo).</summary>
        public bool PlayerFiring { get; private set; }
        /// <summary>O inimigo está atirando: piratas atacam sozinhos; mercantes só revidam depois do seu primeiro tiro.</summary>
        public bool Hostile { get; private set; }
        public bool HasCannons => sea.shipView.CannonMuzzles().Count > 0;
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
            Fx.Explosion = fxExplosion; Fx.Flash = fxFlash; Fx.SplashAnim = fxSplashAnim; Fx.Smoke = fxSmoke;
            Fx.Debris = fxDebris; Fx.Cracks = fxCracks; Fx.CannonFire = cannonFireFrames;
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
                    // O relógio do encontro corre desde a partida; o navio só aparece com o mar livre
                    // (sem ilha chegando ou indo embora). Antes o relógio parava enquanto a ilha de onde
                    // saímos sumia, e em trechos curtos (navio rápido) o encontro nunca chegava.
                    if (sea.Mode == SeaMode.Sailing)
                    {
                        timer -= Time.deltaTime;
                        if (timer <= 0f && (sea.islands == null || !sea.islands.Present)) SpawnEnemy();
                    }
                    if (seaCamera)
                    {
                        // Em terra, a câmera acompanha o capitão pela ilha
                        bool landed = sea.Mode == SeaMode.Landed && sea.islands != null && sea.islands.Root != null;
                        seaCamera.targetX = landed && sea.captain != null
                            ? Mathf.Clamp(sea.captain.transform.position.x - 3f, 0f, Mathf.Max(0f, sea.islands.RegionMax - 13f))
                            : 0f;
                    }
                    break;

                case EncounterState.Approaching:
                    MoveEnemyTowards(holdX, 6f);
                    if (Mathf.Abs(enemyShip.origin.x - holdX) < 0.05f)
                    {
                        SetState(EncounterState.Engaged);
                        Say(pirate
                            ? $"Piratas! {EnemyCrewAlive} no convés deles. Atire para derrubar alguns antes de abordar (ou aborde já, se for corajoso)."
                            : $"Um mercante com {EnemyCrewAlive} marinheiro(s). Atirar, Abordar com ganchos, ou Seguir viagem.");
                    }
                    break;

                case EncounterState.Engaged:
                    // Nada acontece sozinho do seu lado: você dá as ordens. Os piratas abrem fogo depois de alguns segundos.
                    if (pirate && !Hostile && stateTimer > 4f) { Hostile = true; Say("Os piratas abriram fogo!"); Changed?.Invoke(); }
                    FireCannons();
                    if (pirate && (sea.ShipHp < sea.Stats.hp / 2 || stateTimer > 45f))
                    {
                        Say("Os piratas lançaram as cordas: vão abordar o seu navio!");
                        StartBoarding(true);
                    }
                    break;

                case EncounterState.Closing:   // não é mais usado (a abordagem agora é com cordas, sem encostar)
                    StartBoarding(false);
                    break;

                case EncounterState.Boarding:
                    if (EnemyCrewAlive == 0) Win();
                    break;

                case EncounterState.Victory:
                    if (!leaving) break;   // esperando o capitão decidir: Capturar, Saquear ou Deixar ir
                    // Espera todo mundo voltar pelos ganchos (no máximo 8 s; quem ficou é trazido de volta) e só então parte
                    if (float.IsPositiveInfinity(leaveAt) && (AllAboard() || stateTimer > 8f))
                    {
                        BringEveryoneBack();
                        leaveAt = stateTimer + 0.8f;
                    }
                    if (stateTimer > leaveAt) MoveEnemyTowards(enemyShip.origin.x + 30f, 5f);
                    if (stateTimer > leaveAt + 3.5f) EndEncounter();
                    break;

                case EncounterState.Sinking:
                    if (enemyShip == null) { EndEncounter(); break; }
                    sinkFx -= Time.deltaTime;
                    if (sinkFx <= 0f && stateTimer < 2.4f)
                    {
                        sinkFx = Random.Range(0.18f, 0.35f);
                        var p = enemyShip.RandomBlockPoint();
                        Fx.Play(Fx.Explosion, p, 16f, false, 37, Random.Range(0.6f, 1f));
                        Fx.Splinters(p, 5);
                        AudioManager.PlayVaried(Sfx.Wood, 0.6f, 0.25f);
                    }
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
            {
                float enemyRight = enemyShip.RightEdge - enemyShip.origin.x + holdX;   // onde ele vai ficar
                float left = sea.shipView.LeftEdge - 1.5f, right = enemyRight + 1.5f;
                seaCamera.targetX = (left + right) / 2f;
                seaCamera.targetWidth = right - left;
            }
            else if (seaCamera != null) seaCamera.targetWidth = 0f;
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
            view.sailTint = pirate ? Color.white : new Color(0.85f, 0.92f, 1f);   // pirata já usa a vela preta
            go.SetActive(true);
            view.Bind(layout);

            view.TryGetBounds(out int minX, out _);
            holdX = sea.shipView.RightEdge + shipGap - minX;
            float startX = holdX + 30f;
            go.transform.position = new Vector3(startX, sea.shipView.origin.y, 0);
            enemyShip = view;
            AddFlag(view, layout);
            Physics2D.SyncTransforms();
            SpawnEnemyCrew();   // a tripulação já aparece no convés enquanto o navio se aproxima

            var stats = layout.ComputeStats();
            EnemyMaxHp = EnemyHp = Mathf.RoundToInt(stats.hp * (pirate ? 0.35f : 0.45f));
            playerReload.Clear(); enemyReload.Clear();
            PlayerFiring = false;
            Hostile = false;
            foreach (var _ in sea.shipView.CannonMuzzles()) playerReload.Add(Random.Range(1f, 2.5f));
            foreach (var _ in view.CannonMuzzles()) enemyReload.Add(Random.Range(1.5f, 3f));

            SetState(EncounterState.Approaching);
            AudioManager.Instance?.PlayBattle(true);
            Say(pirate ? "Bandeira negra no horizonte! Navio pirata!" : "Navio mercante à vista! Carga gorda...");
            AskOnSight();
        }

        /// <summary>Chance de escapar logo que o navio aparece (depende da diferença de velocidade).</summary>
        public float EscapeChance => Mathf.Round(Mathf.Clamp(0.5f + (sea.Stats.speedKnots - enemySpeedKnots) * 0.12f, 0.15f, 0.95f) * 20f) / 20f;

        /// <summary>Tela de decisão quando o navio aparece: enfrentar ou fugir (mercante: deixar passar).</summary>
        void AskOnSight()
        {
            if (pirate)
            {
                int pct = Mathf.RoundToInt(EscapeChance * 100f);
                SeaDialog.Ask("Encontro hostil! O que vamos fazer, capitão?",
                    $"Bandeira negra no horizonte: um navio pirata ({enemySpeedKnots:0.0} nós; o nosso faz {sea.Stats.speedKnots:0.0}).",
                    new SeaDialog.Option("Enfrentar!", () => Say("Às armas! Preparem os canhões!")),
                    new SeaDialog.Option($"Fugir ({pct}% de chance)", () =>
                    {
                        if (Random.value < EscapeChance) { Say("Escapamos! Os piratas ficaram para trás."); ClearEnemyCrew(); SetState(EncounterState.Fled); }
                        else Say("Não deu para fugir: eles nos alcançaram! Às armas!");
                    }));
            }
            else
                SeaDialog.Ask("Navio mercante à vista! O que vamos fazer, capitão?",
                    "Um navio de carga navegando devagar. Atacar rende ouro e mercadorias.",
                    new SeaDialog.Option("Interceptar!", () => Say("Vamos atrás da carga!")),
                    new SeaDialog.Option("Deixar passar", () => { Say("Deixamos o mercante seguir viagem."); ClearEnemyCrew(); SetState(EncounterState.Fled); }));
        }

        void AddFlag(ShipView view, ShipLayout layout)
        {
            Vector2Int top = new Vector2Int(-1, -1);
            foreach (var kv in layout.Blocks)
                if (kv.Value.role == BlockRole.Mast && kv.Key.y > top.y) top = kv.Key;
            if (top.x < 0) return;
            var f = new GameObject("Bandeira").AddComponent<SpriteRenderer>();
            f.transform.SetParent(view.transform, false);
            f.transform.localPosition = (Vector2)top + new Vector2(0.5f, 1f);
            f.sprite = pirate ? flagPirate : flagMerchant;
            f.flipX = true;
            f.sortingOrder = -9;
            var frames = pirate ? flagPirateFrames : flagMerchantFrames;
            if (frames != null && frames.Length > 0)
            {
                var anim = f.gameObject.AddComponent<SpriteAnim>();
                anim.frames = frames; anim.fps = 10f; anim.loop = true;
            }
        }

        ShipBlockDef Def(BlockRole role)
        {
            foreach (var d in sea.Palette) if (d.role == role) return d;
            return sea.Palette[0];
        }

        /// <summary>Peça pelo nome do asset (cai para a do papel indicado se não existir).</summary>
        ShipBlockDef Named(string assetName, BlockRole fallback)
        {
            foreach (var d in sea.Palette) if (d != null && d.name == assetName) return d;
            return Def(fallback);
        }

        /// <summary>Linha de casco com as pontas em rampa (◥ à esquerda, ◤ à direita), para o navio afinar embaixo.</summary>
        void HullRow(ShipLayout l, int y, int x0, int x1, ShipBlockDef hull, ShipBlockDef slope, bool taper)
        {
            for (int x = x0; x <= x1; x++) l.Set(new Vector2Int(x, y), hull, false);
            if (!taper || slope == null) return;
            l.Set(new Vector2Int(x0, y), slope, 3, false);
            l.Set(new Vector2Int(x1, y), slope, 2, false);
        }

        ShipLayout MerchantLayout()
        {
            var l = new ShipLayout();
            var hull = Def(BlockRole.Hull); var mast = Def(BlockRole.Mast); var sail = Def(BlockRole.Sail);
            var slope = Named("CascoRampa", BlockRole.Hull);
            HullRow(l, 1, 8, 14, hull, slope, true);
            HullRow(l, 2, 7, 15, hull, slope, false);
            for (int y = 3; y <= 6; y++) l.Set(new Vector2Int(11, y), mast, false);
            for (int y = 4; y <= 6; y++) { l.Set(new Vector2Int(10, y), sail, false); l.Set(new Vector2Int(12, y), sail, false); }
            l.Set(new Vector2Int(8, 3), Def(BlockRole.Cannon), false);
            l.Set(new Vector2Int(14, 3), Named("Caixote", BlockRole.Cabin), false);
            l.Set(new Vector2Int(13, 3), Named("Barril", BlockRole.Cabin), false);
            return l;
        }

        ShipLayout PirateLayout()
        {
            var l = new ShipLayout();
            var hull = Named("CascoEscuro", BlockRole.Hull); var mast = Def(BlockRole.Mast);
            var sail = Named("VelaPreta", BlockRole.Sail);
            var slope = Named("CascoEscuroRampa", BlockRole.Hull);
            HullRow(l, 0, 8, 15, hull, slope, true);
            HullRow(l, 1, 6, 17, hull, slope, true);
            HullRow(l, 2, 5, 18, hull, slope, false);
            foreach (int mx in new[] { 9, 14 })
            {
                for (int y = 3; y <= 7; y++) l.Set(new Vector2Int(mx, y), mast, false);
                for (int y = 4; y <= 7; y++) { l.Set(new Vector2Int(mx - 1, y), sail, false); l.Set(new Vector2Int(mx + 1, y), sail, false); }
            }
            l.Set(new Vector2Int(6, 3), Def(BlockRole.Cannon), false);
            l.Set(new Vector2Int(7, 3), Def(BlockRole.Cannon), false);
            l.Set(new Vector2Int(16, 3), Named("Barril", BlockRole.Cabin), false);
            l.Set(new Vector2Int(17, 3), Named("Lanterna", BlockRole.Cabin), false);
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
                var lib = Looks;
                CharacterLook look = null;
                if (lib != null) look = pirate ? lib.enemyPirate : lib.visitors[Random.Range(0, lib.visitors.Length)];
                var c = look != null
                    ? SpawnFighter(spawn, Team.Enemy, pirate ? "Pirata" : "Marinheiro", enemyShip, look)
                    : SpawnFighter(spawn, Team.Enemy, pirate ? "Pirata" : "Marinheiro", enemyShip,
                        pirate ? new Color32(40, 40, 45, 255) : new Color32(70, 110, 190, 255),
                        pirate ? new Color32(160, 30, 30, 255) : new Color32(90, 60, 30, 255));
                c.maxHp = c.hp = pirate ? 55 : 35;
                c.damage = pirate ? new Vector2Int(8, 12) : new Vector2Int(5, 8);
                c.attackCooldown = pirate ? 0.85f : 1f;
                c.FacingRight = false;
                enemyCrew.Add(c);
            }
        }

        /// <summary>Lutador com o visual de um personagem (o mesmo da vila).</summary>
        [Header("Animações (quadros)")]
        public Sprite[] fxExplosion;
        public Sprite[] fxFlash, fxSplashAnim, fxSmoke, fxDebris, fxCracks, cannonFireFrames, flagPirateFrames, flagMerchantFrames;

        public Combatant SpawnFighter(Vector2 pos, Team team, string name, ShipView home, CharacterLook look)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var capsule = go.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.55f, 0.9f);
            capsule.offset = new Vector2(0, 0.45f);
            var captainCol = sea.captain != null ? sea.captain.GetComponent<Collider2D>() : null;
            if (captainCol != null) capsule.sharedMaterial = captainCol.sharedMaterial;
            go.AddComponent<LookFigure>().Init(look, team == Team.Player ? 9 : 8);
            // Inimigos seguram uma arma (só o desenho); marinheiros mercantes, uma arma simples
            if (team == Team.Enemy && Armory.Instance != null) HandWeapon.Set(go, Armory.Instance.EnemyWeapon(name != "Marinheiro"));
            var c = go.AddComponent<Combatant>();
            c.team = team;
            c.displayName = name;
            c.figureHeight = LookFigure.SideScale;
            c.knockOutInsteadOfDeath = team == Team.Player;
            var ai = go.AddComponent<CombatAI>();
            ai.homeShip = home;
            ai.homeX = pos.x;
            ai.charge = false;
            return c;
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
            box.size = new Vector2(0.8f, 0.6f);
            box.offset = new Vector2(0, 0.3f);
            var captainCol = sea.captain != null ? sea.captain.GetComponent<Collider2D>() : null;
            if (captainCol != null) box.sharedMaterial = captainCol.sharedMaterial;
            go.AddComponent<SimpleFigure>().Init(frames, 8);
            var c = go.AddComponent<Combatant>();
            c.team = Team.Enemy;
            c.displayName = name;
            c.figureHeight = 0.8f;
            var ai = go.AddComponent<CombatAI>();
            ai.homeX = pos.x;
            ai.moveSpeed = 2.6f;
            ai.jumpSpeed = 8f;
            return c;
        }

        // ---------- Canhões ----------
        void FireCannons()
        {
            var pm = sea.shipView.Cannons();
            for (int i = 0; PlayerFiring && i < pm.Count && i < playerReload.Count; i++)
            {
                playerReload[i] -= Time.deltaTime;
                if (playerReload[i] > 0) continue;
                playerReload[i] = Random.Range(2.6f, 3.4f);
                bool hit = Random.value < 0.7f;
                Vector2 target = hit ? AimAt(enemyShip, Team.Enemy) : new Vector2(enemyShip.CenterX + Random.Range(-6f, 6f), enemyShip.WaterlineY - 0.2f);
                sea.shipView.PlayCannonFire(pm[i].go);
                if (seaCamera) seaCamera.Shake(0.08f);
                Cannonball.Fire(pm[i].muzzle, target, hit, () =>
                {
                    if (enemyShip == null) return;
                    enemyShip.AddDamageDecal(target);
                    HurtAround(target, Team.Enemy);
                    if (Random.value < 0.3f) SplinterHit();
                    DamageEnemy(Random.Range(8, 13));
                }, true);
            }

            var em = enemyShip.Cannons();
            for (int i = 0; Hostile && i < em.Count && i < enemyReload.Count; i++)
            {
                enemyReload[i] -= Time.deltaTime;
                if (enemyReload[i] > 0) continue;
                enemyReload[i] = Random.Range(2.8f, 3.8f);
                bool hit = Random.value < 0.6f;
                Vector2 target = hit ? AimAt(sea.shipView, Team.Player) : new Vector2(sea.shipView.CenterX + Random.Range(-6f, 6f), sea.shipView.WaterlineY - 0.2f);
                int dmg = pirate ? Random.Range(9, 14) : Random.Range(6, 10);
                enemyShip.PlayCannonFire(em[i].go);
                Cannonball.Fire(em[i].muzzle, target, hit, () =>
                {
                    sea.DamageShip(dmg);
                    sea.shipView.AddDamageDecal(target);
                    HurtAround(target, Team.Player);
                    if (seaCamera) seaCamera.Shake(0.35f);
                }, false);
            }
        }

        /// <summary>Onde a bala vai cair: às vezes mira em alguém no convés (25%), senão num bloco qualquer.</summary>
        Vector2 AimAt(ShipView ship, Team team)
        {
            if (Random.value < 0.25f)
            {
                var people = new List<Combatant>();
                foreach (var c in Combatant.All)
                    if (c != null && c.team == team && !c.IsDown && ship.ContainsX(c.transform.position.x)) people.Add(c);
                if (people.Count > 0) return (Vector2)people[Random.Range(0, people.Count)].transform.position + Vector2.up * 0.5f;
            }
            return ship.RandomBlockPoint();
        }

        /// <summary>Estilhaços: um tripulante inimigo qualquer leva um bom dano (é assim que os canhões "afinam" a tripulação deles antes da abordagem).</summary>
        void SplinterHit()
        {
            var alive = enemyCrew.FindAll(c => c != null && !c.IsDown && enemyShip != null && enemyShip.ContainsX(c.transform.position.x));
            if (alive.Count == 0) return;
            var v = alive[Random.Range(0, alive.Count)];
            v.TakeDamage(Random.Range(18, 31), null);
            Fx.Splinters((Vector2)v.transform.position + Vector2.up * 0.5f, 4);
            if (v.IsDown) Say($"Estilhaços derrubaram um {v.displayName.ToLower()}! Restam {EnemyCrewAlive}.");
        }

        /// <summary>A explosão da bala fere e empurra quem estiver perto (do time atingido).</summary>
        void HurtAround(Vector2 point, Team team)
        {
            bool any = false;
            foreach (var c in new List<Combatant>(Combatant.All))
            {
                if (c == null || c.team != team || c.IsDown) continue;
                Vector2 body = (Vector2)c.transform.position + Vector2.up * 0.5f;
                Vector2 d = body - point;
                if (d.magnitude > 1.15f) continue;
                c.TakeDamage(Random.Range(10, 17), null);
                var rb = c.GetComponent<Rigidbody2D>();
                if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
                    rb.linearVelocity = new Vector2(Mathf.Sign(d.x == 0 ? 1 : d.x) * 4f, 6f);
                any = true;
            }
            if (any)
            {
                AudioManager.PlayVaried(Sfx.Hurt, 0.8f);
                if (team == Team.Enemy && Random.value < 0.5f) Say("Bala certeira no convés deles!");
            }
        }

        void DamageEnemy(int dmg)
        {
            if (State != EncounterState.Engaged && State != EncounterState.Approaching) return;
            EnemyHp = Mathf.Max(0, EnemyHp - dmg);
            Changed?.Invoke();
            if (EnemyHp <= 0) Sink();
        }

        // ---------- Modo Sandbox (testes) ----------
        /// <summary>Faz aparecer um navio agora (só navegando, sem ilha na tela).</summary>
        public bool SandboxEncounter()
        {
            if (State != EncounterState.None || sea.Mode != SeaMode.Sailing || (sea.islands != null && sea.islands.Present)) return false;
            SpawnEnemy();
            return true;
        }

        /// <summary>Vence o combate atual: na abordagem derruba a tripulação; antes dela, afunda o navio.</summary>
        public bool SandboxWin()
        {
            switch (State)
            {
                case EncounterState.Approaching:
                case EncounterState.Engaged:
                    DamageEnemy(99999);
                    return true;
                case EncounterState.Closing:
                case EncounterState.Boarding:
                    foreach (var c in enemyCrew) if (c != null && !c.IsDown) c.TakeDamage(99999, null);
                    return true;
            }
            return false;
        }

        // ---------- Ações do jogador ----------
        public void Board()
        {
            if (!CanBoard) return;
            Say("Lançar ganchos! Todos para o navio deles! (Capitão: clique num inimigo para ir até ele · F/J: golpear)");
            StartBoarding(false);
        }

        /// <summary>Atirar / Cessar fogo. Atirar num mercante faz ele revidar.</summary>
        public void ToggleFire()
        {
            if (State != EncounterState.Engaged || !HasCannons) return;
            PlayerFiring = !PlayerFiring;
            if (PlayerFiring && !Hostile)
            {
                Hostile = true;
                Say("Fogo! O mercante vai revidar...");
            }
            else Say(PlayerFiring ? "Fogo à vontade!" : "Cessar fogo!");
            Changed?.Invoke();
        }

        Vector2 ropeAnchor;

        public bool EnemyShipContains(float x) => enemyShip != null && enemyShip.ContainsX(x);

        /// <summary>De um navio para o outro (o seu e o inimigo): aí só dá pra ir na corda.</summary>
        public bool NeedsCrossing(Vector2 from, Vector2 to)
        {
            if (enemyShip == null || State == EncounterState.Sinking || State == EncounterState.Fled) return false;
            bool fromMine = sea.shipView.ContainsX(from.x), toMine = sea.shipView.ContainsX(to.x);
            bool fromTheirs = enemyShip.ContainsX(from.x), toTheirs = enemyShip.ContainsX(to.x);
            return (fromMine && toTheirs) || (fromTheirs && toMine);
        }

        /// <summary>
        /// Leva alguém (capitão ou tripulante com ordem) para o outro navio balançando na corda. Antes da abordagem,
        /// tenta abordar (precisa do casco deles em 60%); se não pode, avisa e devolve false.
        /// </summary>
        public bool TryCross(Component who, Vector2 dest)
        {
            if (who == null) return false;
            if (State == EncounterState.Engaged)
            {
                var team = who.GetComponent<Combatant>();
                if (team == null || team.team != Team.Player) return false;
                Board();
            }
            if (State != EncounterState.Boarding && State != EncounterState.Victory) return false;
            if (RopeSwing.IsBusy(who.gameObject)) return true;
            bool toTheirs = enemyShip.ContainsX(dest.x);
            var ship = toTheirs ? enemyShip : sea.shipView;
            float side = who.transform.position.x < dest.x ? -0.8f : 0.8f;
            Vector2 land = ship.DeckNear(dest.x + side) + Vector2.up * 0.1f;
            RopeSwing.Begin(who, land, AnchorFor(!toTheirs));
            return true;
        }

        /// <summary>Onde a corda fica presa: no topo do mastro do navio de onde a pessoa sai.</summary>
        Vector2 AnchorFor(bool fromEnemy)
        {
            Vector2 mine = sea.shipView.EdgeDeck(true), theirs = enemyShip.EdgeDeck(false);
            var a = new Vector2((mine.x + theirs.x) / 2f, Mathf.Max(mine.y, theirs.y) + 4.5f);
            if (fromEnemy ? enemyShip.MastTop(false, out var mast) : sea.shipView.MastTop(true, out mast))
                a = new Vector2(Mathf.Lerp(mast.x, (mine.x + theirs.x) / 2f, 0.35f), Mathf.Max(mast.y, a.y - 1.5f));
            return a;
        }

        /// <summary>
        /// Abordagem com cordas (os navios não se aproximam): ganchos prendem os dois cascos, a tripulação de quem
        /// aborda atravessa balançando e uma prancha fica para o capitão andar (e para todos voltarem).
        /// </summary>
        void StartBoarding(bool byEnemy)
        {
            PlayerFiring = false;
            // Sem ponte: cada um joga o próprio gancho no outro navio e atravessa andando pela corda (RopeSwing)
            ropeAnchor = AnchorFor(byEnemy);

            int i = 0;
            foreach (var c in sea.CrewFighters)
            {
                if (c == null) continue;
                var ai = c.GetComponent<CombatAI>();
                if (ai) ai.charge = !byEnemy;   // abordados pelo inimigo: defendem o próprio navio
                if (!byEnemy && !c.IsDown)
                    RopeSwing.Begin(c, enemyShip.SpawnPoint(Random.Range(-2.5f, 1.5f)) + Vector2.up * 0.1f, ropeAnchor, 0.2f + i++ * 0.2f);
            }
            i = 0;
            // Quem é abordado defende o próprio navio (senão os dois grupos trocam de navio sem lutar)
            if (byEnemy)
                foreach (var c in enemyCrew)
                {
                    if (c == null) continue;
                    var ai = c.GetComponent<CombatAI>();
                    if (ai) ai.charge = true;
                    if (byEnemy && !c.IsDown)
                        RopeSwing.Begin(c, sea.shipView.SpawnPoint(Random.Range(-1.5f, 2.5f)) + Vector2.up * 0.1f, ropeAnchor, 0.3f + i++ * 0.25f);
                }
            SetState(EncounterState.Boarding);
            if (byEnemy) Say("Defendam o navio! (F, J ou clique para golpear)");
        }

        public void Flee()
        {
            if (!CanFlee) return;
            if (!Hostile)
            {
                Say(pirate ? "Escapamos antes que atirassem!" : "Deixamos o mercante seguir viagem.");
                ClearEnemyCrew();
                SetState(EncounterState.Fled);
            }
            else if (sea.Stats.speedKnots > enemySpeedKnots)
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
            // A rendição dá o ouro do cofre deles; a carga (itens) só vem saqueando o navio
            int gold = sea.AddLoot(pirate ? Random.Range(80, 161) : Random.Range(35, 71), new Dictionary<string, int>());
            var gear = sea.FindGear(pirate ? 0.5f : 0.35f, legDanger, sea.captain.transform.position);
            GameStats.Add(GameStats.SeaVictories);
            AudioManager.Play(Sfx.Chest);
            sea.CheckLegendRumor();
            Say($"VITÓRIA! O {EnemyName.ToLower()} se rendeu: +{gold} ouro{SeaController.GearText(gear)}. Capture o navio, ou volte para o seu (clique no seu navio) e saqueie a carga!");
            sea.ReviveCrew();
            sea.ClearCrewOrders();
            // A tripulação volta pela prancha; ela só é recolhida quando todos estiverem a bordo
            int i = 0;
            foreach (var c in sea.CrewFighters)
            {
                if (c == null) continue;
                var ai = c.GetComponent<CombatAI>();
                var home = sea.shipView.SpawnPoint((i % 2 == 0 ? -1.2f : 1.2f) * (1 + i / 2));
                if (ai != null) { ai.charge = false; ai.homeX = home.x; }
                // Quem está no navio deles volta balançando na corda
                if (!c.IsDown && c.transform.position.x > sea.shipView.RightEdge + 0.1f)
                    RopeSwing.Begin(c, home + Vector2.up * 0.1f, ropeAnchor, 0.8f + i * 0.2f);
                i++;
            }
            leaveAt = float.PositiveInfinity;
            leaving = false;
            SetState(EncounterState.Victory);
        }

        bool leaving;
        /// <summary>Navio rendido esperando a decisão do capitão.</summary>
        public bool Deciding => State == EncounterState.Victory && !leaving;
        /// <summary>Saquear só com todos de volta ao próprio navio (ele vai explodir).</summary>
        public bool CanPlunder => Deciding && AllAboard();

        /// <summary>Deixar o navio rendido ir embora (todos voltam, a prancha é recolhida).</summary>
        public void LetGo()
        {
            if (!Deciding) return;
            leaving = true;
            stateTimer = 0f;
            leaveAt = float.PositiveInfinity;
            ReturnAll();
            Say($"Deixamos o {EnemyName.ToLower()} ir embora.");
            Changed?.Invoke();
        }

        /// <summary>Capitão e tripulação que estão no navio inimigo voltam para o nosso pelo gancho.</summary>
        public void ReturnAll()
        {
            if (enemyShip == null) return;
            int i = 0;
            void Back(Component who)
            {
                if (who == null || !enemyShip.ContainsX(who.transform.position.x) || RopeSwing.IsBusy(who.gameObject)) return;
                RopeSwing.Begin(who, sea.shipView.DeckNear(sea.shipView.RightEdge - 1.5f - i * 0.8f) + Vector2.up * 0.1f, ropeAnchor, i * 0.25f);
                i++;
            }
            if (sea.captainCombatant != null && !sea.captainCombatant.IsDown) Back(sea.captain);
            foreach (var c in sea.CrewFighters) if (c != null && !c.IsDown) Back(c);
        }

        /// <summary>O capitão está no navio inimigo (o HUD mostra "Voltar ao navio").</summary>
        public bool CaptainAway => enemyShip != null && sea.captain != null && enemyShip.ContainsX(sea.captain.transform.position.x);

        /// <summary>Saquear: tira a carga e explode o navio rendido. Tem chance de cada mercadoria.</summary>
        public void Plunder()
        {
            if (!CanPlunder) { if (Deciding) Say("Todos de volta ao nosso navio antes de explodir o deles!"); return; }
            int gold = pirate ? Random.Range(40, 91) : Random.Range(20, 51);
            var items = new Dictionary<string, int>();
            void Chance(string name, float p, int min, int max) { if (Random.value < p) items[name] = Random.Range(min, max + 1); }
            Chance("Rum", 0.8f, pirate ? 4 : 2, pirate ? 10 : 6);
            Chance("Peixe", pirate ? 0.4f : 0.9f, 3, 10);
            Chance("Cana", pirate ? 0.3f : 0.8f, 3, 9);
            gold = sea.AddLoot(gold, items);
            var gear = sea.FindGear(pirate ? 0.8f : 0.6f, legDanger + 1, sea.captain.transform.position);
            AudioManager.PlayVaried(Sfx.Boom, 1f, 0.05f);
            if (seaCamera) seaCamera.Shake(0.45f);
            Say($"Saque! +{gold} ouro{ItemsText(items)}{SeaController.GearText(gear)}. BUM! O navio deles foi pelos ares.");
            ClearEnemyCrew();
            RemoveBridge();
            SetState(EncounterState.Sinking);
        }


        /// <summary>Capturar: passa a navegar no navio rendido (o seu é vendido pela metade do valor).</summary>
        public void Capture()
        {
            if (!Deciding) return;
            var newStats = enemyShip.Layout.ComputeStats();
            int sell = sea.Stats.value / 2;
            SeaDialog.Ask($"Capturar o {EnemyName.ToLower()}?",
                $"Você passa a navegar nele: {newStats.blocks} blocos, {newStats.cannons} canhões, {newStats.speedKnots:0.0} nós, " +
                $"tripulação máx. {newStats.MaxCrew}. Ele vem avariado (casco {Mathf.RoundToInt(100f * EnemyHp / Mathf.Max(1, EnemyMaxHp))}%).\n" +
                $"O seu navio atual é vendido por {sell} ouro (metade do valor). Dá para reformar o novo no Estaleiro.",
                new SeaDialog.Option("Capturar!", DoCapture),
                new SeaDialog.Option("Cancelar", () => { }));
        }

        void DoCapture()
        {
            if (!Deciding || enemyShip == null) return;
            float damage = 1f - (float)EnemyHp / Mathf.Max(1, EnemyMaxHp);
            sea.AdoptShip(enemyShip.Layout, damage, out int sold);
            AudioManager.Play(Sfx.Quest);
            Say($"O navio agora é nosso! O antigo foi vendido por {sold} ouro. Conserte o casco nas docas.");
            ClearEnemyCrew();
            RemoveBridge();
            Destroy(enemyShip.gameObject);
            enemyShip = null;
            EndEncounter();
        }

        float leaveAt = float.PositiveInfinity;
        float sinkFx;

        /// <summary>Capitão e tripulação (de pé) já estão do lado do seu navio.</summary>
        bool AllAboard()
        {
            float edge = sea.shipView.RightEdge + 0.1f;
            if (sea.captain != null && sea.captain.transform.position.x > edge) return false;
            foreach (var c in sea.CrewFighters)
                if (c != null && !c.IsDown && c.transform.position.x > edge) return false;
            return true;
        }

        void BringEveryoneBack()
        {
            float edge = sea.shipView.RightEdge + 0.1f;
            if (sea.captain != null && sea.captain.transform.position.x > edge) sea.captain.Teleport(sea.shipView.SpawnPoint());
            int i = 0;
            foreach (var c in sea.CrewFighters)
            {
                if (c == null) continue;
                if (c.transform.position.x > edge)
                {
                    var p = sea.shipView.SpawnPoint((i % 2 == 0 ? -1.2f : 1.2f) * (1 + i / 2)) + Vector2.up * 0.2f;
                    c.transform.position = p;
                    var body = c.GetComponent<Rigidbody2D>();
                    if (body != null) { body.position = p; body.linearVelocity = Vector2.zero; }
                }
                i++;
            }
        }

        void Sink()
        {
            int gold = sea.AddLoot((pirate ? Random.Range(80, 161) : Random.Range(35, 71)) / 2, new Dictionary<string, int>());
            GameStats.Add(GameStats.SeaVictories);
            AudioManager.PlayVaried(Sfx.Boom, 1f, 0.05f);
            if (seaCamera) seaCamera.Shake(0.4f);
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
            sea.ClearCrewOrders();
            foreach (var c in sea.CrewFighters) if (c != null) { var ai = c.GetComponent<CombatAI>(); if (ai) ai.charge = false; }
            timer = float.PositiveInfinity;   // no máximo um encontro por trecho
            leaving = false;
            PlayerFiring = false;
            Hostile = false;
            AudioManager.Instance?.PlayBattle(false);
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
