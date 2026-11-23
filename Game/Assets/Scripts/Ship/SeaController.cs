using System;
using System.Collections.Generic;
using Pixel.Combat;
using Pixel.Village;
using UnityEngine;

namespace Pixel.Ship
{
    public enum SeaMode { Docked, Editing, Sailing }

    /// <summary>
    /// Controla a cena do Mar (visão lateral): atracado nas docas, editando no estaleiro ou navegando.
    /// Lê o navio, o ouro e a tripulação da vila (que continua carregada por baixo).
    /// </summary>
    public class SeaController : MonoBehaviour
    {
        public static SeaController Instance { get; private set; }

        public ShipView shipView;
        public ShipEditor editor;
        public SideCaptainController captain;
        public Combatant captainCombatant;
        public SeaScroller scroller;
        [Tooltip("Conversão de nós para unidades/s na tela.")]
        public float knotsToUnits = 0.6f;
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

        int testGold = 500, testDamage;

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
            SpawnCrew();
            Changed?.Invoke();
        }

        void SpawnCrew()
        {
            var flow = GameFlow.Instance;
            var director = EncounterDirector.Instance;
            if (flow == null || director == null) return;
            int i = 0;
            foreach (var m in flow.Embarked)
            {
                float offset = (i % 2 == 0 ? -1.2f : 1.2f) * (1 + i / 2);
                var c = director.SpawnFighter(shipView.SpawnPoint(offset) + Vector2.up * 0.2f, Team.Player, m.name, shipView, m.outfit, m.hair, m.skin);
                c.maxHp = c.hp = m.CombatHp;
                c.damage = m.CombatDamage;
                c.link = m;
                var member = m;
                c.Downed += _ => member.injured = true;
                CrewFighters.Add(c);
                i++;
            }
        }

        void RaiseChanged() => Changed?.Invoke();

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
            if (Mode == SeaMode.Sailing && !InCombat)
            {
                scroller.speed = Stats.speedKnots * knotsToUnits;
                DistanceMiles += Stats.speedKnots * Time.deltaTime / 60f; // 1 nó = 1 milha por "hora" (1 min real)
            }
            else scroller.speed = 0f;
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
            if (Damage <= 0 || Mode == SeaMode.Sailing) return;
            int cost = RepairCost;
            if (!Spend(cost)) { Say($"Ouro insuficiente para o conserto ({cost})."); return; }
            Damage = 0;
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

        // ---------- Modos ----------
        public bool CanEdit(out string reason)
        {
            if (Mode == SeaMode.Sailing) { reason = "Só dá pra mexer no navio atracado."; return false; }
            if (GameFlow.Instance != null && !GameFlow.Instance.HasShipyard) { reason = "Construa um Estaleiro na vila para editar o navio."; return false; }
            reason = null;
            return true;
        }

        public bool CanSail(out string reason)
        {
            if (Mode != SeaMode.Docked) { reason = null; return false; }
            if (Stats.speedKnots <= 0f) { reason = "Sem velas o navio não sai do lugar!"; return false; }
            if (ShipHp <= 0) { reason = "O casco está destruído: conserte antes de zarpar."; return false; }
            reason = null;
            return true;
        }

        public void StartEditing()
        {
            if (!CanEdit(out string why)) { Say(why); return; }
            Mode = SeaMode.Editing;
            captain.gameObject.SetActive(false);
            foreach (var c in CrewFighters) if (c != null) c.gameObject.SetActive(false);
            editor.enabled = true;
            Changed?.Invoke();
        }

        public void StopEditing()
        {
            if (Mode != SeaMode.Editing) return;
            editor.enabled = false;
            editor.Selected = null;
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

        public void SetSail()
        {
            if (!CanSail(out string why)) { if (why != null) Say(why); return; }
            Mode = SeaMode.Sailing;
            Say($"Zarpamos! Velocidade: {Stats.speedKnots:0.0} nós. {CrewFighters.Count} tripulante(s) a bordo.");
            Changed?.Invoke();
        }

        public void GoHome()
        {
            if (GameFlow.Instance != null) GameFlow.Instance.ReturnToVillage();
            else Say("(Testando a cena do Mar sozinha: não há vila para voltar.)");
        }
    }
}
