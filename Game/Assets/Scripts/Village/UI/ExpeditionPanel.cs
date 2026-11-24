using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>
    /// Janela de expedições, bem visual: masmorras em cartões (com o bicho de cada uma), o grupo em
    /// 4 vagas com rosto, a tripulação em cartões (rosto, ofício, vida, dano) e os suprimentos com ícones.
    /// Explicações ficam no botão "i" e nas dicas do mouse.
    /// </summary>
    public class ExpeditionPanel : MonoBehaviour
    {
        RectTransform dungeonCol, crewCol, sideCol;
        DungeonDef selectedDungeon;
        readonly List<CrewMember> selected = new List<CrewMember>();
        int rum;
        float refresh;
        int lastActive = -1;
        float crewScroll = 1f;
        ScrollRect crewScrollRect;
        // Peças da coluna da direita atualizadas no lugar (recriar a cada segundo fazia a janela piscar)
        Text rumText, foodText, progressText;
        RectTransform reportOverlay;
        int lastReports = -1;
        Button sendButton;
        Text sendLabel;

        static readonly Color Dark = new Color(0, 0, 0, 0.28f);

        public static ExpeditionPanel Create(Transform canvas)
        {
            ExpeditionPanel panel = null;
            var (win, content) = UIKit.Window("JanelaExpedicoes", canvas, "Expedições", new Vector2(1060, 540), () => panel.Hide());
            panel = win.gameObject.AddComponent<ExpeditionPanel>();
            var h = UIKit.Horizontal(content.gameObject, 0, 10);
            h.childAlignment = TextAnchor.UpperLeft;
            h.childForceExpandHeight = true;
            panel.dungeonCol = Column(content, 300);
            panel.crewCol = Column(content, 440);
            panel.sideCol = Column(content, 276);
            UIKit.Info(win, () =>
                "1. Escolha a masmorra (à esquerda).\n2. Clique nos tripulantes para montar o grupo (até 4).\n3. Leve rum se quiser e clique em Enviar.\n\n" +
                "O grupo vai, luta e volta sozinho. Comida: 1 peixe por pessoa. Rum: cada gole cura 25 de vida de quem estiver fraco; o que sobrar volta.\n\n" +
                "Vencer dá ouro, itens e experiência. Concluir uma masmorra libera a próxima. Quem vai sai do posto de trabalho até voltar.");
            win.gameObject.SetActive(false);
            return panel;
        }

        static RectTransform Column(Transform parent, float width)
        {
            var col = UIKit.Rect("Coluna", parent);
            col.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.18f);
            UIKit.Vertical(col.gameObject, 8, 6);
            UIKit.Size(col, width, 470);
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
            if (em != null && em.Reports.Count > 0) ShowReports();
        }

        public void Hide() => gameObject.SetActive(false);
        public bool IsOpen => gameObject.activeSelf;

        void Update()
        {
            var em = ExpeditionManager.Instance;
            if (em == null) return;
            // Chegou ou saiu um relatório (expedição voltou / prêmio coletado)
            if (em.Reports.Count != lastReports)
            {
                bool more = em.Reports.Count > lastReports && lastReports >= 0;
                lastReports = em.Reports.Count;
                if (more || (reportOverlay != null && reportOverlay.gameObject.activeSelf)) ShowReports();
                RebuildSide();
            }
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

        // ---------- Desenhos ----------
        static Sprite EnemyIcon(EnemyType t)
        {
            var em = ExpeditionManager.Instance;
            var lib = CrewManager.Instance != null ? CrewManager.Instance.looks : null;
            Sprite First(Sprite[] a) => a != null && a.Length > 0 ? a[0] : null;
            return t switch
            {
                EnemyType.Caranguejo => First(em.crab),
                EnemyType.Esqueleto => lib != null && lib.skeleton != null ? lib.skeleton.down[0] : First(em.skeleton),
                EnemyType.Serpente => First(em.serpent),
                EnemyType.Pirata => lib != null && lib.enemyPirate != null ? lib.enemyPirate.down[0] : null,
                EnemyType.CapitaoLendario => lib != null && lib.legendCaptain != null ? lib.legendCaptain.down[0] : null,
                _ => null,
            };
        }

        /// <summary>O "chefe" da masmorra: o último inimigo do último andar.</summary>
        static EnemyType Boss(DungeonDef d)
        {
            for (int s = d.stages.Count - 1; s >= 0; s--)
                if (d.stages[s].enemies.Count > 0) return d.stages[s].enemies[d.stages[s].enemies.Count - 1];
            return EnemyType.Caranguejo;
        }

        static Sprite ItemIcon(string name)
        {
            var vm = VillageManager.Instance;
            if (vm == null) return null;
            foreach (var it in vm.items) if (it != null && (it.displayName == name || it.name == name)) return it.icon;
            return null;
        }

        static Sprite Portrait(CrewMember m)
        {
            var cm = CrewManager.Instance;
            var look = cm != null ? cm.LookOf(m) : null;
            if (look == null) return VillageHUD.Instance != null ? VillageHUD.Instance.crewIcon : null;
            return look.portrait != null ? look.portrait : look.down[0];
        }

        static Image Icon(Transform parent, Sprite s, float w, float h, Color? tint = null)
        {
            var rt = UIKit.Rect("Icone", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s;
            img.enabled = s != null;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = tint ?? Color.white;
            UIKit.Size(rt, w, h);
            return img;
        }

        static RectTransform Row(Transform parent, float height, int spacing = 6)
        {
            var r = UIKit.Rect("Linha", parent);
            UIKit.Horizontal(r.gameObject, 0, spacing).childAlignment = TextAnchor.MiddleLeft;
            UIKit.Size(r, height: height);
            return r;
        }

        static Text Label(Transform parent, string s, int size, Color color, float width = -1, float height = -1)
        {
            var t = UIKit.Text(parent, s, size, TextAnchor.MiddleLeft, color);
            UIKit.Size(t, width, height);
            return t;
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
                bool cleared = em.IsCleared(d);

                var rt = UIKit.Rect("Masmorra", dungeonCol);
                var (btn, bg) = UIKit.ButtonBase(rt);
                btn.onClick.AddListener(() => { selectedDungeon = d; Rebuild(); });
                btn.interactable = unlocked;
                UIKit.SetSelected(bg, d == selectedDungeon);
                UIKit.Size(rt, height: 58);
                var h = UIKit.Horizontal(rt.gameObject, 0, 8);
                h.padding = new RectOffset(8, 8, 5, 9);
                h.childAlignment = TextAnchor.MiddleLeft;
                // Bloqueada: o bicho aparece só como silhueta
                Icon(rt, EnemyIcon(Boss(d)), 40, 40, unlocked ? Color.white : new Color(0, 0, 0, 0.55f));
                var texts = UIKit.Rect("Textos", rt);
                UIKit.Vertical(texts.gameObject, 0, 0);
                UIKit.Size(texts, flexWidth: 1);
                var n = Label(texts, d.displayName, 15, UIKit.ButtonText, height: 22);
                UIKit.Fit(n, 11);
                string sub = !unlocked ? "Bloqueada: vença a anterior"
                    : cleared ? $"Nível {d.recommendedLevel} · {UIKit.Colored("concluída", UIKit.ButtonGood)}"
                    : $"Nível {d.recommendedLevel} · {d.stages.Count} andares";
                Label(texts, sub, 12, UIKit.ButtonMuted, height: 18);
            }

            if (selectedDungeon != null) DungeonDetails(selectedDungeon);
        }

        void DungeonDetails(DungeonDef d)
        {
            var box = UIKit.Rect("Detalhes", dungeonCol);
            box.gameObject.AddComponent<Image>().color = Dark;
            var v = UIKit.Vertical(box.gameObject, 8, 6);
            v.padding = new RectOffset(10, 10, 8, 10);
            UIKit.Size(box, height: 150);

            var desc = UIKit.Text(box, d.description, 13, TextAnchor.UpperLeft, UIKit.MutedText);
            UIKit.Size(desc, height: 36);
            UIKit.Fit(desc, 10);

            // Inimigos (somando todos os andares)
            var counts = new Dictionary<EnemyType, int>();
            foreach (var st in d.stages) foreach (var e in st.enemies) counts[e] = counts.TryGetValue(e, out int c) ? c + 1 : 1;
            var foes = Row(box, 30, 4);
            Label(foes, "Inimigos", 13, UIKit.TextColor, 70);
            foreach (var kv in counts)
            {
                var slot = UIKit.Rect("Inimigo", foes);
                UIKit.Horizontal(slot.gameObject, 0, 2).childAlignment = TextAnchor.MiddleLeft;
                Icon(slot, EnemyIcon(kv.Key), 26, 26);
                Label(slot, $"x{kv.Value}", 13, UIKit.TextColor);
                slot.gameObject.AddComponent<Image>().color = Color.clear;
                var (hp, mn, mx, _) = EnemyStats.Get(kv.Key);
                UIKit.Tip(slot, $"{EnemyStats.Name(kv.Key)} x{kv.Value}\nVida {hp} · Dano {mn}-{mx}");
            }

            // Recompensa
            var prize = Row(box, 26, 4);
            Label(prize, "Prêmio", 13, UIKit.TextColor, 70);
            Icon(prize, VillageHUD.Instance != null ? VillageHUD.Instance.goldIcon : null, 20, 20);
            Label(prize, $"{d.goldReward.x}-{d.goldReward.y}", 13, UIKit.GoldColor, 62);
            Icon(prize, ItemIcon(d.itemReward), 20, 20);
            Label(prize, $"{d.itemAmount.x}-{d.itemAmount.y}", 13, UIKit.TextColor);

            var trip = Label(box, $"{d.stages.Count} andares · {d.travelSeconds:0} s de ida e {d.travelSeconds:0} s de volta", 12, UIKit.MutedText, height: 18);
            UIKit.Fit(trip, 10);
        }

        // ---------- Tripulação ----------
        void RebuildCrew()
        {
            var cm = CrewManager.Instance;
            if (crewScrollRect != null) crewScroll = crewScrollRect.verticalNormalizedPosition;
            UIKit.Clear(crewCol);
            Title(crewCol, $"Grupo  {selected.Count}/{ExpeditionManager.MaxPartySize}");

            // As 4 vagas do grupo (clicar numa vaga tira a pessoa)
            var slots = Row(crewCol, 92, 8);
            slots.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            for (int i = 0; i < ExpeditionManager.MaxPartySize; i++)
            {
                var slot = UIKit.Rect("Vaga", slots);
                slot.gameObject.AddComponent<Image>().color = Dark;
                UIKit.Size(slot, 96, 88);
                var v = UIKit.Vertical(slot.gameObject, 4, 2);
                v.childAlignment = TextAnchor.MiddleCenter;
                if (i < selected.Count)
                {
                    var m = selected[i];
                    var b = slot.gameObject.AddComponent<Button>();
                    b.targetGraphic = slot.GetComponent<Image>();
                    b.onClick.AddListener(() => { selected.Remove(m); RebuildCrew(); RebuildSide(); });
                    Icon(slot, Portrait(m), 52, 52);
                    var nm = UIKit.Text(slot, m.name.Split(' ')[0], 13, TextAnchor.MiddleCenter);
                    UIKit.Size(nm, height: 18);
                    UIKit.Fit(nm, 9);
                    UIKit.Tip(slot, $"{m.name}\nClique para tirar do grupo.");
                }
                else
                {
                    var plus = UIKit.Text(slot, "+", 30, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.25f));
                    UIKit.Size(plus, height: 50);
                    var e = UIKit.Text(slot, "vaga", 12, TextAnchor.MiddleCenter, UIKit.MutedText);
                    UIKit.Size(e, height: 18);
                }
            }

            Label(crewCol, "Tripulação (clique para chamar)", 13, UIKit.MutedText, height: 20);

            // Lista com rolagem: cartões em 2 colunas
            var view = UIKit.Rect("Lista", crewCol);
            view.gameObject.AddComponent<Image>().color = Color.clear;
            view.gameObject.AddComponent<RectMask2D>();
            var le = UIKit.Size(view, flexWidth: 1);
            le.flexibleHeight = 1;
            var list = UIKit.Rect("Cartoes", view);
            list.anchorMin = new Vector2(0, 1); list.anchorMax = new Vector2(1, 1); list.pivot = new Vector2(0.5f, 1);
            list.sizeDelta = Vector2.zero;
            var grid = list.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(208, 60);
            grid.spacing = new Vector2(8, 6);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = list; sr.viewport = view;
            sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 24;
            crewScrollRect = sr;

            foreach (var c in cm.Crew) CrewCard(list, c);
            if (cm.Crew.Count == 0)
                UIKit.Size(UIKit.Text(crewCol, "Ninguém no bando. Contrate na Taverna!", 15, TextAnchor.MiddleCenter, UIKit.MutedText), height: 40);

            Canvas.ForceUpdateCanvases();
            sr.verticalNormalizedPosition = crewScroll;
        }

        void CrewCard(Transform parent, CrewMember m)
        {
            bool avail = ExpeditionManager.Available(m);
            bool isSel = selected.Contains(m);
            var rt = UIKit.Rect("Tripulante", parent);
            var (btn, bg) = UIKit.ButtonBase(rt);
            UIKit.SetSelected(bg, isSel);
            btn.interactable = avail;
            btn.onClick.AddListener(() =>
            {
                if (selected.Contains(m)) selected.Remove(m);
                else if (selected.Count < ExpeditionManager.MaxPartySize) selected.Add(m);
                RebuildCrew();
                RebuildSide();
            });
            var h = UIKit.Horizontal(rt.gameObject, 0, 6);
            h.padding = new RectOffset(6, 6, 4, 8);
            h.childAlignment = TextAnchor.MiddleLeft;
            Icon(rt, Portrait(m), 42, 42, avail ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f));

            var texts = UIKit.Rect("Textos", rt);
            UIKit.Vertical(texts.gameObject, 0, 0);
            UIKit.Size(texts, flexWidth: 1);
            var n = Label(texts, m.name, 13, UIKit.ButtonText, height: 17);
            UIKit.Fit(n, 9);
            string state = m.onExpedition ? "em expedição" : m.atSea ? "no mar" : $"{CrewText.Name(m.profession)} nv {m.level}";
            Label(texts, state, 12, UIKit.ButtonMuted, height: 15);
            var dmg = m.CombatDamage;
            Label(texts, $"Vida {m.CombatHp} · Dano {dmg.x}-{dmg.y}", 12, UIKit.ButtonText, height: 15);

            string work = m.workplace != null ? $"\nTrabalha: {m.workplace.def.displayName} (para enquanto estiver fora)" : "";
            UIKit.Tip(rt, $"{m.name}\n{CrewText.Name(m.profession)} nível {m.level} · moral {m.morale} ({CrewText.MoraleLabel(m.morale)})" +
                          $"\nVida {m.CombatHp} · Dano {dmg.x}-{dmg.y}{work}");
        }

        // ---------- Suprimentos, grupos salvos e envio ----------
        void RebuildSide()
        {
            var em = ExpeditionManager.Instance;
            var cm = CrewManager.Instance;
            if (em == null || sideCol == null) return;
            UIKit.Clear(sideCol);

            Title(sideCol, "Suprimentos");
            var food = Row(sideCol, 30);
            Icon(food, cm.food != null ? cm.food.icon : null, 24, 24);
            foodText = Label(food, "", 14, UIKit.TextColor);
            UIKit.Tip(food, "Comida: 1 peixe por pessoa (sai do estoque).");

            var rumRow = Row(sideCol, 34);
            Icon(rumRow, cm.rum != null ? cm.rum.icon : null, 24, 24);
            var (minus, _, _) = UIKit.Button(rumRow, "-", () => { rum--; RefreshSide(); }, 18);
            UIKit.Size(minus, 32, 30);
            rumText = UIKit.Text(rumRow, "", 15, TextAnchor.MiddleCenter);
            UIKit.Size(rumText, 90, 30);
            var (plus, _, _) = UIKit.Button(rumRow, "+", () => { rum++; RefreshSide(); }, 18);
            UIKit.Size(plus, 32, 30);
            UIKit.Tip(rumRow, $"Rum: cada gole cura 25 de vida de quem estiver fraco. O que sobrar volta. Máximo {ExpeditionManager.MaxRum}.");

            Title(sideCol, "Grupos salvos");
            string[] names = { "A", "B", "C" };
            for (int i = 0; i < em.presets.Length; i++)
            {
                int idx = i;
                var p = em.presets[i];
                var row = Row(sideCol, 32);
                string desc = p.members.Count == 0 ? "vazio" : $"{p.members.Count} pessoa(s)";
                var (load, _, _) = UIKit.Button(row, $"{names[i]}: {desc}", () => LoadPreset(idx), 14);
                load.interactable = p.members.Count > 0;
                UIKit.Size(load, 170, 32);
                UIKit.Tip(load, p.members.Count == 0 ? "Grupo vazio: monte um grupo e clique em Salvar." : "Chamar este grupo: " + string.Join(", ", p.members));
                var (save, _, _) = UIKit.Button(row, "Salvar", () => SavePreset(idx), 13);
                save.interactable = selected.Count > 0;
                UIKit.Size(save, 76, 32);
                UIKit.Tip(save, "Guarda o grupo escolhido agora neste espaço.");
            }

            Title(sideCol, "Fora agora");
            progressText = UIKit.Text(sideCol, "", 13, TextAnchor.UpperLeft);
            UIKit.Size(progressText, height: 64);
            UIKit.Fit(progressText, 10);

            if (em.Reports.Count > 0)
            {
                var (rb, rl, rbg) = UIKit.Button(sideCol, $"Prêmios para coletar ({em.Reports.Count})", ShowReports, 15);
                UIKit.SetSelected(rbg, true);
                UIKit.Size(rb, height: 40);
            }

            var spacer = UIKit.Rect("Espaco", sideCol);
            UIKit.Size(spacer, height: 0).flexibleHeight = 1;

            var (send, label, _) = UIKit.Button(sideCol, "", () =>
            {
                if (em.Send(selectedDungeon, new List<CrewMember>(selected), rum))
                {
                    selected.Clear();
                    Rebuild();
                }
            }, 18);
            sendButton = send;
            sendLabel = label;
            UIKit.Fit(label, 11);
            UIKit.Size(send, height: 60);
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
            rumText.text = $"{rum} {UIKit.Colored($"/ {rumStock}", UIKit.MutedText)}";
            int fish = cm.food != null ? vm.GetStock(cm.food) : 0;
            foodText.text = $"{selected.Count} {UIKit.Colored($"/ {fish}  (1 por pessoa)", UIKit.MutedText)}";

            var sb = new System.Text.StringBuilder();
            if (em.Active.Count == 0) sb.Append(UIKit.Colored("Ninguém fora.", UIKit.MutedText));
            foreach (var e in em.Active) sb.AppendLine("• " + e.StatusText);
            progressText.text = sb.ToString();

            bool can = em.CanSend(selectedDungeon, selected, rum, out string why);
            sendButton.interactable = can;
            sendLabel.text = can ? "Enviar!" : why;
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
                CrewMember m = null;
                foreach (var c in CrewManager.Instance.Crew) if (c.name == n) m = c;
                if (m != null && ExpeditionManager.Available(m) && selected.Count < ExpeditionManager.MaxPartySize) selected.Add(m);
            }
            rum = p.rum;
            RebuildCrew();
            RebuildSide();
        }

        // ---------- Expedições concluídas (prêmio para coletar) ----------
        void ShowReports()
        {
            var em = ExpeditionManager.Instance;
            if (reportOverlay != null) Destroy(reportOverlay.gameObject);
            reportOverlay = null;
            if (em == null || em.Reports.Count == 0) return;

            // Fundo escurecido por cima da janela (bloqueia os cliques atrás)
            reportOverlay = UIKit.Rect("Concluidas", transform);
            UIKit.Stretch(reportOverlay);
            reportOverlay.offsetMax = new Vector2(0, -40);   // o cabeçalho (X) continua clicável
            reportOverlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.55f);

            var box = UIKit.Panel("Quadro", reportOverlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660, 0));
            var v = UIKit.Vertical(box.gameObject, 16, 8);
            v.padding = new RectOffset(18, 18, 14, 16);
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var title = UIKit.Text(box, em.Reports.Count == 1 ? "Expedição concluída!" : $"{em.Reports.Count} expedições concluídas!", 22, TextAnchor.MiddleCenter, UIKit.GoldColor);
            title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);
            UIKit.Size(title, height: 30);

            int shown = 0;
            foreach (var r in em.Reports)
            {
                if (shown++ == 3) { Label(box, $"… e mais {em.Reports.Count - 3}. Colete para ver as próximas.", 13, UIKit.MutedText, height: 20); break; }
                ReportRow(box, r);
            }

            var buttons = Row(box, 46, 10);
            buttons.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var (later, _, _) = UIKit.Button(buttons, "Depois", () => { Destroy(reportOverlay.gameObject); reportOverlay = null; }, 16);
            UIKit.Size(later, 140, 44);
            UIKit.Tip(later, "O prêmio fica guardado aqui até você coletar.");
            var (all, _, allBg) = UIKit.Button(buttons, em.Reports.Count > 1 ? "Coletar tudo" : "Coletar", () => em.CollectAll(), 18);
            UIKit.SetSelected(allBg, true);
            UIKit.Size(all, 220, 44);
        }

        void ReportRow(Transform parent, ExpeditionManager.Report r)
        {
            var em = ExpeditionManager.Instance;
            var d = em.FindDungeon(r.dungeon);
            var row = UIKit.Rect("Relatorio", parent);
            row.gameObject.AddComponent<Image>().color = Dark;
            var h = UIKit.Horizontal(row.gameObject, 0, 12);
            h.padding = new RectOffset(10, 10, 8, 8);
            h.childAlignment = TextAnchor.MiddleLeft;
            UIKit.Size(row, height: 86);

            Icon(row, d != null ? EnemyIcon(Boss(d)) : null, 48, 48, r.victory ? Color.white : new Color(0.5f, 0.5f, 0.5f));

            // Nome, resultado e o grupo
            var info = UIKit.Rect("Info", row);
            UIKit.Vertical(info.gameObject, 0, 2);
            UIKit.Size(info, 250, 70);
            var n = Label(info, d != null ? d.displayName : r.dungeon, 16, UIKit.GoldColor, height: 20);
            UIKit.Fit(n, 11);
            Label(info, r.victory
                    ? UIKit.Colored($"Vitória! {r.stagesCleared}/{r.totalStages} andares", new Color(0.55f, 0.95f, 0.5f))
                    : UIKit.Colored($"Derrota no andar {r.stagesCleared + 1} de {r.totalStages}", new Color(1f, 0.5f, 0.45f)), 13, UIKit.TextColor, height: 18);
            var faces = Row(info, 26, 3);
            foreach (var name in r.members)
            {
                var m = FindCrew(name);
                bool fell = r.fell.Contains(name);
                var face = Icon(faces, m != null ? Portrait(m) : (VillageHUD.Instance != null ? VillageHUD.Instance.crewIcon : null), 24, 24,
                    fell ? new Color(1f, 0.45f, 0.45f) : Color.white);
                face.raycastTarget = true;
                UIKit.Tip(face, fell ? $"{name}: caiu na luta (-15 de moral) · +{r.xp} XP" : $"{name}: +{r.xp} XP");
            }

            // Prêmio
            var loot = UIKit.Rect("Premio", row);
            UIKit.Vertical(loot.gameObject, 0, 2);
            UIKit.Size(loot, flexWidth: 1);
            LootLine(loot, VillageHUD.Instance != null ? VillageHUD.Instance.goldIcon : null, $"+{r.gold}", UIKit.GoldColor, "Ouro");
            if (r.itemAmount > 0) LootLine(loot, ItemIcon(r.item), $"+{r.itemAmount}", UIKit.TextColor, r.item);
            if (r.rumBack > 0)
                LootLine(loot, CrewManager.Instance.rum != null ? CrewManager.Instance.rum.icon : null, $"+{r.rumBack}", UIKit.TextColor, "Rum que sobrou e voltou");

            if (em.Reports.Count > 1)   // com uma só, o botão grande de baixo já basta
            {
                var (btn, _, bg) = UIKit.Button(row, "Coletar", () => em.Collect(r), 16);
                UIKit.SetSelected(bg, true);
                UIKit.Size(btn, 110, 44);
            }
        }

        static void LootLine(Transform parent, Sprite icon, string amount, Color color, string tip)
        {
            var l = Row(parent, 22, 5);
            Icon(l, icon, 20, 20);
            Label(l, amount, 16, color);
            l.gameObject.AddComponent<Image>().color = Color.clear;
            UIKit.Tip(l, tip);
        }

        static CrewMember FindCrew(string name)
        {
            if (CrewManager.Instance == null) return null;
            foreach (var c in CrewManager.Instance.Crew) if (c.name == name) return c;
            return null;
        }

        static void Title(Transform parent, string t)
        {
            var x = UIKit.Text(parent, t, 17, TextAnchor.MiddleLeft, UIKit.GoldColor);
            x.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
            UIKit.Size(x, height: 24);
        }
    }
}
