using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>
    /// Janela de expedições: escolher a masmorra, montar o grupo (até 4), levar rum como curativo,
    /// salvar o grupo como "Grupo A/B/C" e enviar.
    /// </summary>
    public class ExpeditionPanel : MonoBehaviour
    {
        RectTransform dungeonCol, crewCol, sideCol;
        DungeonDef selectedDungeon;
        readonly List<CrewMember> selected = new List<CrewMember>();
        int rum;
        float refresh;
        int lastActive = -1;
        // Peças da coluna da direita atualizadas no lugar (recriar a cada segundo fazia a janela piscar)
        Text rumText, progressText;
        Button sendButton;
        Text sendLabel;

        public static ExpeditionPanel Create(Transform canvas)
        {
            ExpeditionPanel panel = null;
            var (win, content) = UIKit.Window("JanelaExpedicoes", canvas, "Expedições — Masmorras", new Vector2(1060, 520), () => panel.Hide());
            panel = win.gameObject.AddComponent<ExpeditionPanel>();
            var h = UIKit.Horizontal(content.gameObject, 0, 10);
            h.childAlignment = TextAnchor.UpperLeft;
            h.childForceExpandHeight = true;
            panel.dungeonCol = Column(content, 300);
            panel.crewCol = Column(content, 430);
            panel.sideCol = Column(content, 290);
            win.gameObject.SetActive(false);
            return panel;
        }

        static RectTransform Column(Transform parent, float width)
        {
            var col = UIKit.Rect("Coluna", parent);
            col.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.18f);
            UIKit.Vertical(col.gameObject, 8, 6);
            UIKit.Size(col, width, 450);
            return col;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            var em = ExpeditionManager.Instance;
            if (selectedDungeon == null && em != null)
                for (int i = em.dungeons.Length - 1; i >= 0; i--)
                    if (em.IsUnlocked(i)) { selectedDungeon = em.dungeons[i]; break; }
            selected.RemoveAll(m => !ExpeditionManager.Available(m) || !CrewManager.Instance.IsCrew(m));
            Rebuild();
        }

        public void Hide() => gameObject.SetActive(false);
        public bool IsOpen => gameObject.activeSelf;

        void Update()
        {
            var em = ExpeditionManager.Instance;
            if (em == null) return;
            // Uma expedição saiu ou voltou: a lista de quem está livre mudou
            if (em.Active.Count != lastActive) { lastActive = em.Active.Count; Rebuild(); return; }
            refresh -= Time.unscaledDeltaTime;
            if (refresh <= 0f) { refresh = 0.25f; RefreshSide(); }
        }

        void Rebuild()
        {
            if (ExpeditionManager.Instance != null) lastActive = ExpeditionManager.Instance.Active.Count;
            RebuildDungeons();
            RebuildCrew();
            RebuildSide();
        }

        // ---------- Masmorras ----------
        void RebuildDungeons()
        {
            var em = ExpeditionManager.Instance;
            UIKit.Clear(dungeonCol);
            Title(dungeonCol, "Masmorras");
            for (int i = 0; i < em.dungeons.Length; i++)
            {
                var d = em.dungeons[i];
                bool unlocked = em.IsUnlocked(i);
                string state = em.IsCleared(d) ? UIKit.Colored(" (concluída)", new Color(0.55f, 0.95f, 0.5f))
                    : unlocked ? "" : UIKit.Colored(" (bloqueada)", UIKit.MutedText);
                var (b, label, bg) = UIKit.Button(dungeonCol, $"{d.displayName}{state}\nNível recomendado {d.recommendedLevel} · {d.stages.Count} andares", () =>
                {
                    selectedDungeon = d;
                    Rebuild();
                }, 14);
                label.alignment = TextAnchor.MiddleLeft;
                bg.color = d == selectedDungeon ? UIKit.ButtonSelected : UIKit.ButtonColor;
                b.interactable = unlocked;
                UIKit.Size(b, height: 50);
            }

            if (selectedDungeon != null)
            {
                var d = selectedDungeon;
                string stages = "";
                for (int s = 0; s < d.stages.Count; s++)
                {
                    var counts = new Dictionary<EnemyType, int>();
                    foreach (var e in d.stages[s].enemies) counts[e] = counts.TryGetValue(e, out int n) ? n + 1 : 1;
                    var parts = new List<string>();
                    foreach (var kv in counts) parts.Add($"{kv.Value} {EnemyStats.Name(kv.Key)}");
                    stages += $"Andar {s + 1}: {string.Join(", ", parts)}\n";
                }
                var info = UIKit.Text(dungeonCol, $"{UIKit.Colored(d.description, UIKit.MutedText)}\n\n{stages}" +
                    $"Recompensa: {d.goldReward.x}-{d.goldReward.y} ouro + {d.itemAmount.x}-{d.itemAmount.y} {d.itemReward}\n" +
                    $"Viagem: {d.travelSeconds:0} s para ir e {d.travelSeconds:0} s para voltar", 13, TextAnchor.UpperLeft);
                UIKit.Size(info, height: 190);
            }
        }

        // ---------- Tripulação ----------
        void RebuildCrew()
        {
            var cm = CrewManager.Instance;
            UIKit.Clear(crewCol);
            Title(crewCol, $"Grupo ({selected.Count}/{ExpeditionManager.MaxPartySize}) — clique para escolher");
            foreach (var c in cm.Crew)
            {
                var m = c;
                bool avail = ExpeditionManager.Available(m);
                bool isSel = selected.Contains(m);
                string where = m.onExpedition ? "em expedição" : m.atSea ? "no mar"
                    : m.workplace != null ? $"trabalha: {m.workplace.def.displayName} (vai parar)" : "livre";
                var dmg = m.CombatDamage;
                var (b, label, bg) = UIKit.Button(crewCol,
                    $"{(isSel ? "[X]" : "[  ]")} {m.name} — {CrewText.Name(m.profession)} nv {m.level}\n" +
                    $"Vida {m.CombatHp} · Dano {dmg.x}-{dmg.y} · moral {m.morale} · {where}", () =>
                    {
                        if (selected.Contains(m)) selected.Remove(m);
                        else if (selected.Count < ExpeditionManager.MaxPartySize) selected.Add(m);
                        RebuildCrew();
                        RebuildSide();
                    }, 13);
                label.alignment = TextAnchor.MiddleLeft;
                bg.color = isSel ? UIKit.ButtonSelected : UIKit.ButtonColor;
                b.interactable = avail;
                UIKit.Size(b, height: 44);
            }
            if (cm.Crew.Count == 0)
                UIKit.Size(UIKit.Text(crewCol, "Ninguém no bando. Contrate na Taverna!", 15, TextAnchor.MiddleCenter, UIKit.MutedText), height: 40);
        }

        // ---------- Rum, grupos salvos e envio ----------
        void RebuildSide()
        {
            var em = ExpeditionManager.Instance;
            var vm = VillageManager.Instance;
            var cm = CrewManager.Instance;
            if (em == null || sideCol == null) return;
            UIKit.Clear(sideCol);

            Title(sideCol, "Suprimentos");
            int rumStock = cm.rum != null ? vm.GetStock(cm.rum) : 0;
            rum = Mathf.Clamp(rum, 0, Mathf.Min(ExpeditionManager.MaxRum, rumStock));
            var rumRow = UIKit.Rect("Rum", sideCol);
            UIKit.Horizontal(rumRow.gameObject, 0, 6);
            UIKit.Size(rumRow, height: 36);
            var (minus, _, _) = UIKit.Button(rumRow, "-", () => { rum--; RefreshSide(); }, 18);
            UIKit.Size(minus, 36, 32);
            rumText = UIKit.Text(rumRow, "", 15, TextAnchor.MiddleCenter);
            UIKit.Size(rumText, 150, 32);
            var (plus, _, _) = UIKit.Button(rumRow, "+", () => { rum++; RefreshSide(); }, 18);
            UIKit.Size(plus, 36, 32);
            UIKit.Size(UIKit.Text(sideCol, "Cada gole de rum cura 25 de vida de quem estiver fraco. O que sobrar volta.\nComida: 1 peixe por pessoa.", 12, TextAnchor.UpperLeft, UIKit.MutedText), height: 44);

            Title(sideCol, "Grupos salvos");
            string[] names = { "A", "B", "C" };
            for (int i = 0; i < em.presets.Length; i++)
            {
                int idx = i;
                var p = em.presets[i];
                var row = UIKit.Rect("Grupo", sideCol);
                UIKit.Horizontal(row.gameObject, 0, 6);
                UIKit.Size(row, height: 34);
                string desc = p.members.Count == 0 ? "vazio" : $"{p.members.Count} pessoa(s), {p.rum} rum";
                var (load, _, _) = UIKit.Button(row, $"Grupo {names[i]}: {desc}", () => LoadPreset(idx), 13);
                load.interactable = p.members.Count > 0;
                UIKit.Size(load, 190, 32);
                var (save, _, _) = UIKit.Button(row, "Salvar", () => SavePreset(idx), 13);
                save.interactable = selected.Count > 0;
                UIKit.Size(save, 70, 32);
            }

            Title(sideCol, "Em andamento");
            progressText = UIKit.Text(sideCol, "", 12, TextAnchor.UpperLeft);
            UIKit.Size(progressText, height: 64);

            var (send, label, _) = UIKit.Button(sideCol, "", () =>
            {
                if (em.Send(selectedDungeon, new List<CrewMember>(selected), rum))
                {
                    selected.Clear();
                    Rebuild();
                }
            }, 14);
            sendButton = send;
            sendLabel = label;
            UIKit.Size(send, height: 56);
            RefreshSide();
        }

        /// <summary>Atualiza textos e o botão de envio sem recriar nada.</summary>
        void RefreshSide()
        {
            var em = ExpeditionManager.Instance;
            var vm = VillageManager.Instance;
            var cm = CrewManager.Instance;
            if (em == null || rumText == null) return;
            int rumStock = cm.rum != null ? vm.GetStock(cm.rum) : 0;
            rum = Mathf.Clamp(rum, 0, Mathf.Min(ExpeditionManager.MaxRum, rumStock));
            rumText.text = $"Rum: {rum} (tem {rumStock})";

            var sb = new System.Text.StringBuilder();
            if (em.Active.Count == 0) sb.Append(UIKit.Colored("Nenhuma expedição fora.", UIKit.MutedText));
            foreach (var e in em.Active) sb.AppendLine("• " + e.StatusText);
            progressText.text = sb.ToString();

            bool can = em.CanSend(selectedDungeon, selected, rum, out string why);
            sendButton.interactable = can;
            sendLabel.text = can ? $"ENVIAR EXPEDIÇÃO\n({selected.Count} tripulante(s), {selected.Count} peixe, {rum} rum)" : why;
        }

        void SavePreset(int i)
        {
            var p = ExpeditionManager.Instance.presets[i];
            p.members.Clear();
            foreach (var m in selected) p.members.Add(m.name);
            p.rum = rum;
            RebuildSide();
        }

        void LoadPreset(int i)
        {
            var p = ExpeditionManager.Instance.presets[i];
            selected.Clear();
            foreach (var n in p.members)
            {
                var m = CrewManager.Instance.Crew is List<CrewMember> list ? list.Find(c => c.name == n) : null;
                if (m == null) foreach (var c in CrewManager.Instance.Crew) if (c.name == n) m = c;
                if (m != null && ExpeditionManager.Available(m) && selected.Count < ExpeditionManager.MaxPartySize) selected.Add(m);
            }
            rum = p.rum;
            RebuildCrew();
            RebuildSide();
        }

        static void Title(Transform parent, string t)
        {
            var x = UIKit.Text(parent, t, 16, TextAnchor.MiddleLeft, UIKit.GoldColor);
            UIKit.Size(x, height: 24);
        }
    }
}
