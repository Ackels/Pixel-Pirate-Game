using System;
using System.Collections.Generic;
using Pixel.Combat;
using Pixel.Village;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pixel.Ship
{
    public enum SeaMode { Docked, Editing, Sailing, Landed }

    /// <summary>
    /// Controla a cena do Mar (visão lateral): atracado na Enseada, editando no estaleiro,
    /// navegando rumo a uma ilha ou desembarcado nela. Lê navio, ouro e tripulação da vila.
    /// </summary>
    public class SeaController : MonoBehaviour
    {
        public static SeaController Instance { get; private set; }

        public ShipView shipView;
        public ShipEditor editor;
        public SideCaptainController captain;
        public Combatant captainCombatant;
        public SeaScroller scroller;
        public IslandBuilder islands;
        [Tooltip("Conversão de nós para unidades/s na tela.")]
        public float knotsToUnits = 0.6f;
        [Tooltip("Segundos de viagem por milha a 1 nó (tempo = milhas / nós × isto).")]
        public float secondsPerMileKnot = 3f;
        [Tooltip("Milhas por porção de peixe, por pessoa a bordo.")]
        public float milesPerRation = 12f;
        [Tooltip("Ouro por ponto de vida consertado.")]
        public float repairCostPerHp = 1f;
        [Range(0f, 1f)] public float defeatGoldLoss = 0.25f;

        [Header("Só para testar a cena do Mar sozinha (sem a vila)")]
        public ShipBlockDef[] fallbackBlocks;
        public ShipBlockDef fallbackHull, fallbackMast, fallbackSail;

        public SeaMode Mode { get; private set; } = SeaMode.Docked;
        public ShipLayout Layout { get; private set; }
        public ShipBlockDef[] Palette { get; private set; }
        public ShipStats Stats { get; private set; }
        public float DistanceMiles { get; private set; }
        public List<Combatant> CrewFighters { get; } = new List<Combatant>();
        public event Action Changed;
        public event Action<string> Message;
        public event Action TradeRequested;
        public event Action MapRequested;

        // Viagem
        public IslandNode Location { get; private set; }
        public IslandNode Destination { get; private set; }
        public float LegProgress => legSeconds > 0 ? Mathf.Clamp01(legElapsed / legSeconds) : 0f;
        public Interactable NearbyInteractable { get; private set; }
        public WorldMap World => GameFlow.Instance != null ? GameFlow.Instance.World : (testWorld ??= WorldMap.Generate(1234));

        WorldMap testWorld;
        float legSeconds, legElapsed, eventAt = -1f, defeatTimer = -1f, weatherAt = -1f;
        Weather legWeather;
        int legShortage;
        int testGold = 500, testDamage;
        readonly System.Random rng = new System.Random();

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Layout != null) Layout.Changed -= OnLayoutChanged;
            if (VillageManager.Instance != null) VillageManager.Instance.Changed -= RaiseChanged;
        }

        void Start()
        {
            var flow = GameFlow.Instance;
            Layout = flow != null ? flow.Ship : ShipLayout.Raft(fallbackHull, fallbackMast, fallbackSail);
            Palette = flow != null && flow.shipBlocks != null && flow.shipBlocks.Length > 0 ? flow.shipBlocks : fallbackBlocks;
            Layout.Changed += OnLayoutChanged;
            shipView.Bind(Layout);
            Stats = Layout.ComputeStats();
            editor.enabled = false;
            captain.Teleport(shipView.SpawnPoint());
            captain.FellOverboard += () => Say("Homem ao mar! O capitão voltou pro convés encharcado.");
            if (VillageManager.Instance != null) VillageManager.Instance.Changed += RaiseChanged;
            if (GetComponent<ShipLife>() == null) gameObject.AddComponent<ShipLife>();   // vida a bordo
            if (Damage > 0) shipView.ScatterDamage(Mathf.Clamp(Damage / 12, 1, 10));     // avarias da última viagem
            SpawnCrew();
            Location = World.Home;
            Changed?.Invoke();
        }

        void SpawnCrew()
        {
            var flow = GameFlow.Instance;
            if (flow == null || EncounterDirector.Instance == null) return;
            int i = 0;
            foreach (var m in flow.Embarked)
            {
                float offset = (i % 2 == 0 ? -1.2f : 1.2f) * (1 + i / 2);
                AddFighter(m, shipView.SpawnPoint(offset) + Vector2.up * 0.2f);
                i++;
            }
        }

        Combatant AddFighter(CrewMember m, Vector2 pos)
        {
            var look = CrewManager.Instance != null ? CrewManager.Instance.LookOf(m) : null;
            var c = look != null
                ? EncounterDirector.Instance.SpawnFighter(pos, Team.Player, m.name, shipView, look)
                : EncounterDirector.Instance.SpawnFighter(pos, Team.Player, m.name, shipView, m.outfit, m.hair, m.skin);
            c.maxHp = c.hp = m.CombatHp;
            c.damage = m.CombatDamage;
            c.link = m;
            c.Downed += _ => m.injured = true;
            c.gameObject.AddComponent<CrewLife>().member = m;
            CrewFighters.Add(c);
            return c;
        }

        void RaiseChanged() => Changed?.Invoke();

        // ---------- Modo Sandbox (testes) ----------
        public void SandboxRepair() { Damage = 0; shipView.ClearDamage(); RaiseChanged(); }

        public void SandboxRevealMap()
        {
            foreach (var n in World.Islands) n.revealed = true;
            RaiseChanged();
        }

        /// <summary>Termina a viagem atual na hora (se houver combate, chega logo depois dele).</summary>
        public bool SandboxArrive()
        {
            if (Mode != SeaMode.Sailing || legSeconds <= 0f) return false;
            legElapsed = legSeconds;
            return true;
        }

        public void SandboxHealAll()
        {
            void Heal(Combatant c)
            {
                if (c == null) return;
                if (c.IsDown) c.Revive(1f); else c.HealFull();
            }
            Heal(captainCombatant);
            foreach (var c in CrewFighters) Heal(c);
        }

        void OnLayoutChanged()
        {
            Stats = Layout.ComputeStats();
            Damage = Mathf.Min(Damage, Stats.hp);
            Changed?.Invoke();
        }

        public void Say(string msg) => Message?.Invoke(msg);

        bool InCombat => EncounterDirector.Instance != null && EncounterDirector.Instance.Busy;

        void Update()
        {
            if (Stats.hp > 0) shipView.damageLevel = (float)Damage / Stats.hp;
            bool arriving = islands != null && islands.Root != null && !islands.Arrived;
            if (Mode == SeaMode.Sailing && !InCombat && !arriving)
            {
                scroller.speed = Stats.speedKnots * knotsToUnits;
                float before = legElapsed;
                legElapsed += Time.deltaTime;
                DistanceMiles += Stats.speedKnots * Time.deltaTime / secondsPerMileKnot;
                if (eventAt > 0 && before < eventAt && legElapsed >= eventAt) SeaEvent();
                if (weatherAt > 0 && before < weatherAt && legElapsed >= weatherAt) BeginWeather();
                if (legElapsed >= legSeconds) Arrive();
            }
            else scroller.speed = arriving ? Stats.speedKnots * knotsToUnits * 0.4f : 0f;

            // Interagir (E) com baús e comerciantes
            NearbyInteractable = null;
            if ((Mode == SeaMode.Landed || Mode == SeaMode.Docked) && captain.isActiveAndEnabled)
            {
                NearbyInteractable = Interactable.Nearest(captain.transform.position);
                if (NearbyInteractable != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                    NearbyInteractable.onUse?.Invoke();
                if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame && !CrewCommander.Paused) Dig();
            }

            // Capitão caído na ilha = derrota
            if (Mode == SeaMode.Landed && captainCombatant != null && captainCombatant.IsDown && defeatTimer < 0)
            {
                defeatTimer = 2.5f;
                Say("DERROTA! O capitão caiu na ilha. A tripulação o arrasta de volta pro navio...");
            }
            if (defeatTimer >= 0)
            {
                defeatTimer -= Time.deltaTime;
                if (defeatTimer < 0) Defeat();
            }
        }

        // ---------- Ouro (da vila) ----------
        public int Gold => VillageManager.Instance != null ? VillageManager.Instance.Gold : testGold;

        public bool Spend(int amount)
        {
            if (VillageManager.Instance != null) return VillageManager.Instance.TrySpendGold(amount);
            if (testGold < amount) return false;
            testGold -= amount;
            Changed?.Invoke();
            return true;
        }

        public void Refund(int amount)
        {
            if (VillageManager.Instance != null) VillageManager.Instance.AddGold(amount);
            else { testGold += amount; Changed?.Invoke(); }
        }

        public void AddLoot(int gold, Dictionary<string, int> items)
        {
            Refund(gold);
            if (GameFlow.Instance != null) GameFlow.Instance.VoyageGold += gold;
            var vm = VillageManager.Instance;
            if (vm == null) return;
            foreach (var kv in items)
                foreach (var item in vm.items)
                    if (item != null && item.displayName == kv.Key) vm.AddStock(item, kv.Value);
        }

        // ---------- Vida do navio ----------
        int Damage
        {
            get => GameFlow.Instance != null ? GameFlow.Instance.ShipDamage : testDamage;
            set { if (GameFlow.Instance != null) GameFlow.Instance.ShipDamage = value; else testDamage = value; }
        }

        public int ShipHp => Mathf.Max(0, Stats.hp - Damage);
        public int RepairCost => Mathf.CeilToInt(Damage * repairCostPerHp);

        public void DamageShip(int amount)
        {
            Damage = Mathf.Min(Stats.hp, Damage + amount);
            Changed?.Invoke();
        }

        public void Repair()
        {
            if (Damage <= 0 || Mode != SeaMode.Docked) return;
            int cost = RepairCost;
            if (!Spend(cost)) { Say($"Ouro insuficiente para o conserto ({cost})."); return; }
            Damage = 0;
            shipView.ClearDamage();
            Say("Casco consertado! Pronto para zarpar.");
            Changed?.Invoke();
        }

        public void ReviveCrew()
        {
            foreach (var c in CrewFighters)
                if (c != null && c.IsDown) c.Revive(0.3f);
            if (captainCombatant != null && !captainCombatant.IsDown) captainCombatant.HealFull();
        }

        /// <summary>Derrota: perde parte do ouro e volta para a vila.</summary>
        public void Defeat()
        {
            int lost = Mathf.RoundToInt(Gold * defeatGoldLoss);
            if (lost > 0) Spend(lost);
            CrewManager.Instance?.Report($"Derrota no mar! Perdemos {lost} ouro e voltamos com o rabo entre as pernas.", new Color(1f, 0.45f, 0.4f));
            GoHome();
        }

        // ---------- Estaleiro ----------
        public bool CanEdit(out string reason)
        {
            if (Mode != SeaMode.Docked && Mode != SeaMode.Editing) { reason = "Só dá pra mexer no navio atracado na Enseada."; return false; }
            if (GameFlow.Instance != null && !GameFlow.Instance.HasShipyard) { reason = "Construa um Estaleiro na vila para editar o navio."; return false; }
            reason = null;
            return true;
        }

        public void StartEditing()
        {
            if (!CanEdit(out string why)) { Say(why); return; }
            Mode = SeaMode.Editing;
            EditingCamera(true);
            captain.gameObject.SetActive(false);
            foreach (var c in CrewFighters) if (c != null) c.gameObject.SetActive(false);
            editor.enabled = true;
            Changed?.Invoke();
        }

        float cameraY = float.NaN, cameraSize;
        /// <summary>
        /// No estaleiro a câmera afasta (mostra 22 blocos de altura) e desce um bloco: a grade inteira cabe entre a
        /// dica de cima e a barra de peças. O "pixel perfeito" fica desligado só enquanto edita
        /// (em janelas pequenas ele não deixa afastar).
        /// </summary>
        void EditingCamera(bool on)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var ppc = cam.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
            if (ppc != null) ppc.enabled = !on;
            var p = cam.transform.position;
            if (on)
            {
                if (float.IsNaN(cameraY)) { cameraY = p.y; cameraSize = cam.orthographicSize; }
                p.y = cameraY - 1f;
                cam.orthographicSize = 11f;
            }
            else if (!float.IsNaN(cameraY)) { p.y = cameraY; cam.orthographicSize = cameraSize; cameraY = float.NaN; }
            cam.transform.position = p;
        }

        public void StopEditing()
        {
            if (Mode != SeaMode.Editing) return;
            editor.enabled = false;
            editor.Selected = null;
            EditingCamera(false);
            Mode = SeaMode.Docked;
            captain.gameObject.SetActive(true);
            captain.Teleport(shipView.SpawnPoint());
            int i = 0;
            foreach (var c in CrewFighters)
            {
                if (c == null) continue;
                c.gameObject.SetActive(true);
                var p = shipView.SpawnPoint((i % 2 == 0 ? -1.2f : 1.2f) * (1 + i / 2)) + Vector2.up * 0.2f;
                c.transform.position = p;
                var ai = c.GetComponent<CombatAI>();
                if (ai) ai.homeX = p.x;
                i++;
            }
            Changed?.Invoke();
        }

        // ---------- Rumo ----------
        public void OpenMap() => MapRequested?.Invoke();

        public int People => 1 + CrewFighters.Count;
        public float LegMiles(IslandNode to) => Location == null ? 0f : WorldMap.Distance(Location, to);
        public float LegSecondsTo(IslandNode to) =>
            Stats.speedKnots <= 0 ? 0f : Mathf.Max(6f, LegMiles(to) / Stats.speedKnots * secondsPerMileKnot);
        public int RationsFor(IslandNode to) => Mathf.CeilToInt(LegMiles(to) / milesPerRation) * People;

        public ItemDef FoodItem => CrewManager.Instance != null ? CrewManager.Instance.food : null;
        public int FoodStock => FoodItem != null && VillageManager.Instance != null ? VillageManager.Instance.GetStock(FoodItem) : 999;

        public bool CanSetCourse(IslandNode to, out string reason)
        {
            if (to == null || to == Location) { reason = "Você já está aqui."; return false; }
            if (Mode != SeaMode.Docked && Mode != SeaMode.Landed) { reason = "Só dá pra traçar rumo parado (na Enseada ou numa ilha)."; return false; }
            if (Stats.speedKnots <= 0f) { reason = "Sem velas o navio não sai do lugar!"; return false; }
            if (ShipHp <= 0) { reason = "O casco está destruído: conserte antes de zarpar."; return false; }
            reason = null;
            return true;
        }

        public void SetCourse(IslandNode to)
        {
            if (!CanSetCourse(to, out string why)) { Say(why); return; }

            // Provisões: o peixe sai do estoque da vila
            int need = RationsFor(to);
            int have = Mathf.Min(need, FoodStock);
            if (FoodItem != null && have > 0) VillageManager.Instance.TryTakeStock(FoodItem, have);
            legShortage = need - have;

            if (Mode == SeaMode.Landed)
            {
                if (islands != null && islands.HasLiveEnemies) Say("Batemos em retirada! A ilha continua com inimigos.");
                LeaveIsland(to.type == IslandType.Home);
            }
            Destination = to;
            legSeconds = LegSecondsTo(to);
            legElapsed = 0f;
            eventAt = rng.NextDouble() < 0.4 ? legSeconds * (0.2f + (float)rng.NextDouble() * 0.6f) : -1f;
            EncounterDirector.Instance?.ScheduleLeg(legSeconds, to.Danger(World.Home.pos));
            // Clima do trecho: ~25% chuva, ~10% tempestade (começa em algum ponto da viagem)
            double w = rng.NextDouble();
            legWeather = w < 0.10 ? Weather.Storm : w < 0.35 ? Weather.Rain : Weather.Clear;
            weatherAt = legWeather != Weather.Clear ? legSeconds * (0.05f + (float)rng.NextDouble() * 0.4f) : -1f;
            Mode = SeaMode.Sailing;
            string food = legShortage > 0 ? $" Faltaram {legShortage} porções de peixe: a tripulação vai passar fome!" : $" Provisões: {need} peixe.";
            Say($"Rumo a {(to.revealed ? to.name : "uma ilha desconhecida")}! (~{legSeconds:0} s).{food}");
            Changed?.Invoke();
        }

        void LeaveIsland(bool towardsHome)
        {
            float edge = shipView.RightEdge + 0.3f;
            if (captain.transform.position.x > edge) captain.Teleport(shipView.SpawnPoint());
            foreach (var c in CrewFighters)
            {
                if (c == null || c.transform.position.x <= edge) continue;
                var p = shipView.SpawnPoint(UnityEngine.Random.Range(-2f, 2f)) + Vector2.up * 0.2f;
                c.transform.position = p;
                var rb = c.GetComponent<Rigidbody2D>();
                if (rb) { rb.position = p; rb.linearVelocity = Vector2.zero; }
            }
            SetCrewCharge(false);
            ClearCrewOrders();
            ReviveCrew();
            islands.Leave(towardsHome);
        }

        void Arrive()
        {
            var to = Destination;
            Destination = null;
            legSeconds = 0f;
            if (legShortage > 0)
            {
                foreach (var c in CrewFighters)
                    if (c != null && c.link is CrewMember m) m.morale = Mathf.Max(0, m.morale - 10);
                Say("Faltou peixe na viagem: a tripulação chegou com fome (-10 de moral).");
            }

            if (to.type == IslandType.Home)
            {
                GoHome();
                return;
            }
            // A tripulação descansa na viagem: recupera metade da vida que faltava
            foreach (var c in CrewFighters)
                if (c != null) { if (c.IsDown) c.Revive(0.3f); c.Heal((c.maxHp - c.hp) / 2); }
            if (captainCombatant != null) captainCombatant.Heal((captainCombatant.maxHp - captainCombatant.hp) / 2);

            Location = to;
            to.visited = true;
            GameStats.Add(GameStats.IslandsVisited);
            World.RevealAround(to);
            Mode = SeaMode.Landed;
            islands.Build(to);
            Changed?.Invoke();
        }

        /// <summary>Chamado quando a ilha terminou de encostar e a prancha está posta.</summary>
        public void OnIslandReady(IslandBuilder b)
        {
            SetCrewCharge(true);
            string what = b.Node.type switch
            {
                IslandType.Port => "Um porto amigo: fale com o comerciante (E).",
                IslandType.PirateCamp => b.Node.cleared ? "O acampamento está abandonado." : "Um acampamento pirata! Eles vêm aí...",
                _ => b.Node.cleared ? "A ilha está calma." : "Cuidado com os caranguejos!",
            };
            Say($"Chegamos em {b.Node.name}. {what}");
            Changed?.Invoke();
        }

        /// <summary>
        /// Passa a navegar no navio capturado: o desenho dele vira o seu (o antigo é vendido pela metade do valor),
        /// com o dano que ele tinha. Todo mundo vai para o convés novo.
        /// </summary>
        public void AdoptShip(ShipLayout captured, float damageFraction, out int soldFor)
        {
            soldFor = Stats.value / 2;
            Layout.ReplaceWith(captured, true);
            Stats = Layout.ComputeStats();
            Damage = Mathf.Clamp(Mathf.RoundToInt(damageFraction * Stats.hp), 0, Mathf.Max(0, Stats.hp - 1));
            shipView.ClearDamage();
            if (Damage > 0) shipView.ScatterDamage(Mathf.Clamp(Damage / 12, 1, 10));
            if (soldFor > 0) AddLoot(soldFor, new Dictionary<string, int>());
            ClearCrewOrders();
            captain.Teleport(shipView.SpawnPoint());
            int i = 0;
            foreach (var c in CrewFighters)
            {
                if (c == null) continue;
                var p = shipView.SpawnPoint((i % 2 == 0 ? -1.2f : 1.2f) * (1 + i / 2)) + Vector2.up * 0.2f;
                c.transform.position = p;
                var rb = c.GetComponent<Rigidbody2D>();
                if (rb) { rb.position = p; rb.linearVelocity = Vector2.zero; }
                var ai = c.GetComponent<CombatAI>();
                if (ai) { ai.homeX = p.x; ai.homeShip = shipView; }
                i++;
            }
            Changed?.Invoke();
        }

        /// <summary>O capitão usa o que estiver perto (baú, comerciante) — o mesmo que apertar E.</summary>
        public void InteractNearby()
        {
            var it = captain != null ? Interactable.Nearest(captain.transform.position) : null;
            if (it != null) it.onUse?.Invoke();
            else Say("Não tem nada por perto do capitão para usar (chegue perto de um baú ou do comerciante).");
        }

        bool digging;
        /// <summary>Tem um X de tesouro perto do capitão (o HUD mostra "G: cavar").</summary>
        public bool NearDigMark => Mode == SeaMode.Landed && islands != null && captain != null && islands.NearDigMark(captain.transform.position.x);

        /// <summary>O capitão cava onde está. Em cima do X da ilha do tesouro, aparece o baú.</summary>
        public void Dig()
        {
            if (digging) return;
            if (Mode != SeaMode.Landed || islands == null || islands.Root == null) { Say("Só dá pra cavar em terra firme, numa ilha."); return; }
            StartCoroutine(DigRoutine());
        }

        System.Collections.IEnumerator DigRoutine()
        {
            digging = true;
            for (int i = 0; i < 3; i++)
            {
                Vector2 p = captain.transform.position;
                Fx.Spawn(Fx.Puff, p + new Vector2(UnityEngine.Random.Range(-0.35f, 0.35f), 0.15f), 0.45f, 1.5f);
                AudioManager.PlayVaried(Sfx.Build, 0.7f, 0.2f);
                yield return new WaitForSeconds(0.4f);
            }
            digging = false;
            if (islands.TryDig(captain.transform.position.x)) { Say("Você desenterrou o baú do TESOURO! (E para abrir)"); AudioManager.Play(Sfx.Quest); }
            else Say(islands.HasBuriedTreasure ? "Só areia... o X está em outro lugar desta ilha." : "Só areia por aqui.");
        }

        /// <summary>Cancela as ordens dadas aos tripulantes (fim do combate, saída da ilha) e o alvo do capitão.</summary>
        public void ClearCrewOrders()
        {
            foreach (var c in CrewFighters)
                if (c != null) { var ai = c.GetComponent<CombatAI>(); if (ai) ai.ClearOrders(); }
            if (captain != null) captain.ClearAutoTarget();
        }

        void SetCrewCharge(bool on)
        {
            foreach (var c in CrewFighters)
                if (c != null) { var ai = c.GetComponent<CombatAI>(); if (ai) ai.charge = on; }
        }

        // ---------- Eventos no mar ----------
        void SeaEvent()
        {
            double roll = rng.NextDouble();
            if (roll < 0.34)
            {
                int seats = Mathf.Max(0, Stats.MaxCrew - 1);
                if (CrewFighters.Count < seats && CrewManager.Instance != null)
                    SeaDialog.Ask("Náufrago à vista!", "Um marinheiro agarrado num barril acena desesperado para o navio.",
                        new SeaDialog.Option("Resgatar (entra no bando)", Castaway),
                        new SeaDialog.Option("Seguir viagem", () => Say("Deixamos o náufrago para trás...")));
                else
                    SeaDialog.Ask("Náufrago à vista!", "Um marinheiro agarrado num barril acena... mas não há vaga a bordo. (Construa redes!)",
                        new SeaDialog.Option("Jogar água e biscoito (+2 de moral)", () =>
                        {
                            foreach (var c in CrewFighters) if (c != null && c.link is CrewMember cm) cm.morale = Mathf.Min(100, cm.morale + 2);
                            Say("A tripulação gostou da boa ação (+2 de moral).");
                        }),
                        new SeaDialog.Option("Seguir viagem", () => Say("Deixamos o náufrago para trás...")));
            }
            else if (roll < 0.67)
                SeaDialog.Ask("Uma garrafa boiando!", "Tem alguma coisa dentro dela...",
                    new SeaDialog.Option("Pescar a garrafa", Bottle),
                    new SeaDialog.Option("Ignorar", () => Say("A garrafa seguiu com a corrente.")));
            else
            {
                int lo = Mathf.RoundToInt(Stats.hp * 0.08f), hi = Mathf.RoundToInt(Stats.hp * 0.18f);
                SeaDialog.Ask("Tempestade à frente!", "Nuvens negras no caminho. Atravessar castiga o casco; contornar atrasa a viagem.",
                    new SeaDialog.Option($"Atravessar (-{lo} a {hi} de casco)", Storm),
                    new SeaDialog.Option("Contornar (+40% de tempo)", () =>
                    {
                        legSeconds *= 1.4f;
                        SeaSky.Instance?.StartWeather(Weather.Rain, 25f);
                        Say("Demos a volta na tempestade. A viagem vai demorar mais.");
                    }));
            }
        }

        void Castaway()
        {
            var cm = CrewManager.Instance;
            int seats = Mathf.Max(0, Stats.MaxCrew - 1);
            if (cm == null || CrewFighters.Count >= seats)
            {
                Say("Um náufrago agarrado num barril! Sem vaga a bordo, deixamos água e biscoito pra ele. (Construa Cabines!)");
                return;
            }
            var m = cm.CreateCastaway();
            GameFlow.Instance?.Embarked.Add(m);
            AddFighter(m, shipView.SpawnPoint(1.5f) + Vector2.up * 0.2f);
            Say($"Um náufrago! {m.name} ({CrewText.Name(m.profession)}) se junta ao bando, agradecido.");
        }

        void Bottle()
        {
            var island = World.RevealRandom(rng);
            if (island != null) { Say($"Uma garrafa com um mapa! Ele mostra {island.name}... e um X de tesouro!"); return; }
            int gold = rng.Next(15, 41);
            AddLoot(gold, new Dictionary<string, int>());
            Say($"Uma garrafa boiando com {gold} moedas de ouro dentro!");
        }

        void BeginWeather()
        {
            var sky = SeaSky.Instance;
            if (sky == null) return;
            float secs = Mathf.Max(25f, legSeconds * (0.5f + (float)rng.NextDouble() * 0.4f));
            sky.StartWeather(legWeather, secs);
            Say(legWeather == Weather.Storm ? "O céu fechou... tempestade chegando!" : "Começou a chover.");
        }

        void Storm()
        {
            SeaSky.Instance?.StartWeather(Weather.Storm, 30f);
            int dmg = Mathf.Max(3, Mathf.RoundToInt(Stats.hp * (0.08f + (float)rng.NextDouble() * 0.1f)));
            DamageShip(dmg);
            Say($"Tempestade! Ondas gigantes castigam o casco (-{dmg} de vida).");
        }

        // ---------- Porto ----------
        public const int FishPackPrice = 12, FishPackSize = 5, PortRumPrice = 22;

        public void OpenTrade() => TradeRequested?.Invoke();

        public void BuyFish()
        {
            AudioManager.Play(Sfx.Coin);
            if (FoodItem == null) return;
            if (!Spend(FishPackPrice)) { Say("Ouro insuficiente."); return; }
            VillageManager.Instance.AddStock(FoodItem, FishPackSize);
            Say($"Comprou {FishPackSize} peixes para a viagem.");
        }

        public void SellRum(bool all)
        {
            var vm = VillageManager.Instance;
            var rumItem = CrewManager.Instance != null ? CrewManager.Instance.rum : null;
            if (vm == null || rumItem == null) return;
            int n = all ? vm.GetStock(rumItem) : Mathf.Min(1, vm.GetStock(rumItem));
            if (n <= 0) { Say("Você não tem rum para vender."); return; }
            vm.TryTakeStock(rumItem, n);
            AddLoot(n * PortRumPrice, new Dictionary<string, int>());
            Say($"Vendeu {n} rum por {n * PortRumPrice} ouro. O porto paga melhor que os visitantes!");
        }

        public void GoHome()
        {
            if (GameFlow.Instance != null) GameFlow.Instance.ReturnToVillage();
            else Say("(Testando a cena do Mar sozinha: não há vila para voltar.)");
        }
    }
}
