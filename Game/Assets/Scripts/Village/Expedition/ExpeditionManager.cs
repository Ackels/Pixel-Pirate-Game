using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Pixel.Village
{
    /// <summary>
    /// Expedições automáticas (estilo "masmorra"): manda grupos de até 4 tripulantes, sem o capitão,
    /// para masmorras em sequência. O combate roda sozinho enquanto você cuida da vila ou navega.
    /// </summary>
    public class ExpeditionManager : MonoBehaviour
    {
        public static ExpeditionManager Instance { get; private set; }
        public const int MaxPartySize = 4;
        public const int MaxRum = 6;

        public DungeonDef[] dungeons;
        [Header("Sprites dos inimigos (2 quadros, virados para a direita)")]
        public Sprite[] crab, skeleton, serpent;

        [Serializable]
        public class Preset { public List<string> members = new List<string>(); public int rum; }

        public List<Expedition> Active { get; } = new List<Expedition>();
        public Preset[] presets = { new Preset(), new Preset(), new Preset() };
        public event Action Changed;

        readonly HashSet<string> cleared = new HashSet<string>();

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        // ---------- Consultas ----------
        public bool HasBoard =>
            VillageManager.Instance != null && VillageManager.Instance.FindNearest(BuildingKind.Expedition, Vector2.zero) != null;

        public bool IsCleared(DungeonDef d) => d != null && cleared.Contains(d.name);
        public bool IsUnlocked(int index) => index == 0 || (index < dungeons.Length && IsCleared(dungeons[index - 1]));
        public IEnumerable<string> ClearedNames => cleared;

        public static bool Available(CrewMember m) => !m.atSea && !m.onExpedition;

        public bool CanSend(DungeonDef d, List<CrewMember> members, int rum, out string reason)
        {
            var vm = VillageManager.Instance;
            var cm = CrewManager.Instance;
            int index = Array.IndexOf(dungeons, d);
            if (!HasBoard) { reason = "Construa o Quadro de Expedições na vila."; return false; }
            if (d == null || index < 0) { reason = "Escolha uma masmorra."; return false; }
            if (!IsUnlocked(index)) { reason = "Conclua a masmorra anterior primeiro."; return false; }
            if (members.Count == 0) { reason = "Escolha pelo menos 1 tripulante."; return false; }
            if (members.Count > MaxPartySize) { reason = $"No máximo {MaxPartySize} tripulantes."; return false; }
            foreach (var m in members) if (!Available(m)) { reason = $"{m.name} não está disponível."; return false; }
            if (cm.food != null && vm.GetStock(cm.food) < members.Count) { reason = $"Precisa de {members.Count} peixe (1 por pessoa)."; return false; }
            if (rum > 0 && (cm.rum == null || vm.GetStock(cm.rum) < rum)) { reason = $"Você não tem {rum} rum no estoque."; return false; }
            reason = null;
            return true;
        }

        // ---------- Enviar ----------
        public bool Send(DungeonDef d, List<CrewMember> members, int rum)
        {
            if (!CanSend(d, members, rum, out string why)) { CrewManager.Instance?.Report(why, new Color(1f, 0.6f, 0.5f)); return false; }
            var vm = VillageManager.Instance;
            var cm = CrewManager.Instance;
            if (cm.food != null) vm.TryTakeStock(cm.food, members.Count);
            if (rum > 0) vm.TryTakeStock(cm.rum, rum);

            var e = new Expedition { dungeon = d, rum = rum };
            foreach (var m in members)
            {
                m.onExpedition = true;
                if (m.agent != null) m.agent.gameObject.SetActive(false);
                var dmg = m.CombatDamage;
                e.party.Add(new ExpeditionFighter
                {
                    name = m.name, member = m, hp = m.CombatHp, maxHp = m.CombatHp,
                    dmgMin = dmg.x, dmgMax = dmg.y, interval = 1f, timer = Random.Range(0.2f, 1f),
                });
            }
            Active.Add(e);
            cm.AssignAll();
            cm.Report($"Expedição partiu para {d.displayName} com {members.Count} tripulante(s) e {rum} rum.", new Color(0.75f, 0.65f, 1f));
            Changed?.Invoke();
            return true;
        }

        void Update()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                if (!Active[i].Tick(Time.deltaTime)) continue;
                var e = Active[i];
                Active.RemoveAt(i);
                Resolve(e);
            }
        }

        // ---------- Volta ----------
        void Resolve(Expedition e)
        {
            var vm = VillageManager.Instance;
            var cm = CrewManager.Instance;
            var d = e.dungeon;
            int total = d.stages.Count;

            int gold = e.victory
                ? Random.Range(d.goldReward.x, d.goldReward.y + 1)
                : Mathf.RoundToInt(Random.Range(d.goldReward.x, d.goldReward.y + 1) * 0.3f * e.stagesCleared / Mathf.Max(1, total));
            vm.AddGold(gold);
            string itemTxt = "";
            if (e.victory && !string.IsNullOrEmpty(d.itemReward))
            {
                int n = Random.Range(d.itemAmount.x, d.itemAmount.y + 1);
                foreach (var it in vm.items)
                    if (it != null && it.displayName == d.itemReward) { vm.AddStock(it, n); itemTxt = $", {n} {it.displayName}"; }
            }
            if (e.rum > 0 && cm.rum != null) vm.AddStock(cm.rum, e.rum); // rum que sobrou volta

            int hurt = 0;
            var board = vm.FindNearest(BuildingKind.Expedition, Vector2.zero);
            foreach (var f in e.party)
            {
                var m = f.member;
                m.onExpedition = false;
                if (!cm.IsCrew(m)) continue;   // desertou enquanto estava fora
                if (!f.Alive) { m.morale = Mathf.Max(0, m.morale - 15); hurt++; }
                else if (e.victory) m.morale = Mathf.Min(100, m.morale + 5);
                cm.AddXp(m, e.stagesCleared * 3 + (e.victory ? 5 : 0));
                if (m.agent != null)
                {
                    m.agent.transform.position = (board != null ? board.Door : cm.idleArea) + Random.insideUnitCircle * 0.8f;
                    m.agent.gameObject.SetActive(true);
                }
            }
            cm.AssignAll();

            bool firstClear = e.victory && cleared.Add(d.name);
            if (e.victory)
            {
                cm.Report($"Expedição voltou de {d.displayName}: VITÓRIA! +{gold} ouro{itemTxt}.", new Color(0.55f, 0.95f, 0.5f));
                int idx = Array.IndexOf(dungeons, d);
                if (firstClear && d == dungeons[dungeons.Length - 1])
                    cm.Report("O CAPITÃO LENDÁRIO FOI DERROTADO! Seu bando é a lenda dos mares!", new Color(1f, 0.85f, 0.3f));
                else if (firstClear && idx + 1 < dungeons.Length)
                    cm.Report($"Nova masmorra liberada: {dungeons[idx + 1].displayName}!", new Color(1f, 0.85f, 0.3f));
            }
            else cm.Report($"Expedição em {d.displayName} fracassou no andar {e.stage + 1}. Trouxe {gold} ouro.", new Color(1f, 0.55f, 0.45f));
            if (hurt > 0) cm.Report($"{hurt} expedicionário(s) voltaram feridos (-15 de moral).", new Color(1f, 0.65f, 0.4f));
            VillageHUD.Instance?.Toast(e.victory ? $"Expedição vitoriosa! +{gold} ouro" : "A expedição voltou derrotada.");
            AudioManager.Play(e.victory ? Sfx.Chest : Sfx.Hit);
            Changed?.Invoke();
        }

        // ---------- Salvar / carregar ----------
        public void RestoreFromSave(List<string> clearedNames, Preset[] savedPresets)
        {
            Active.Clear();
            cleared.Clear();
            if (clearedNames != null) foreach (var n in clearedNames) cleared.Add(n);
            if (savedPresets != null)
                for (int i = 0; i < presets.Length && i < savedPresets.Length; i++)
                    if (savedPresets[i] != null) presets[i] = savedPresets[i];
            Changed?.Invoke();
        }
    }
}
