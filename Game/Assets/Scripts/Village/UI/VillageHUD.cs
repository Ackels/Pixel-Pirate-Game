using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>
    /// Interface da vila, montada por código, no estilo de madeira pirata:
    /// recursos (topo esquerdo), dia e ajuda (topo direito), barra de ícones embaixo
    /// (Construir, Tripulação, Expedições, Crônicas, Menu) e o menu de construções que abre pelo martelo.
    /// </summary>
    public class VillageHUD : MonoBehaviour
    {
        public static VillageHUD Instance { get; private set; }

        public BuildMode buildMode;
        public Sprite goldIcon;
        public Sprite crewIcon;

        public RectTransform FloatingRoot { get; private set; }
        /// <summary>Aviso de interação (ex.: "Aperte E para embarcar"). Aparece quando não há dica de construção.</summary>
        public string Prompt { get; set; }

        string toast;
        float toastTimer;
        /// <summary>Aviso rápido no meio da tela (ex.: "Jogo salvo").</summary>
        public void Toast(string msg) { toast = msg; toastTimer = 2.5f; }
        public Font UIFont => UIKit.Font;

        const float DockY = 12, DockHeight = 76;
        static readonly string[] Categories = { "Moradia", "Produção", "Serviços", "Decoração" };
        static int CategoryOf(BuildingDef d) => d.kind switch
        {
            BuildingKind.House or BuildingKind.Tavern => 0,
            BuildingKind.Producer => 1,
            BuildingKind.Decoration => 3,
            _ => 2,
        };

        Text dayText, goldText, crewText, hintText;
        Image dayBar;
        RectTransform hintPanel, buildTray, helpPanel, dockPanel;
        EventLog eventLog;
        bool trayOpen;
        readonly List<(ItemDef item, Text text)> itemTexts = new List<(ItemDef, Text)>();
        readonly List<(BuildingDef def, Button button, Image bg, List<(Text text, ItemDef item, int need)> cost)> buildButtons = new List<(BuildingDef, Button, Image, List<(Text, ItemDef, int)>)>();
        Image pathBg;
        readonly List<Image> tabs = new List<Image>();
        int category;
        Image demolishBg, buildDock, crewDock, expDock, chronDock, equipDock;
        static readonly Color Lacking = new Color(0.65f, 0.12f, 0.1f);
        EquipPanel equipPanel;
        RectTransform expBadge;
        BuildingInfoPanel infoPanel;
        TavernPanel tavernPanel;
        CrewPanel crewPanel;
        GameMenu menu;
        SandboxPanel sandbox;
        ExpeditionPanel expeditionPanel;
        ChronicleUI chronicles;
        Camera cam;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        public bool BuildMenuOpen => trayOpen;

        void Start()
        {
            cam = Camera.main;
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var vm = VillageManager.Instance;
            var cm = CrewManager.Instance;

            var canvasGo = new GameObject("Canvas_HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            FloatingRoot = UIKit.Rect("TextosFlutuantes", root);
            UIKit.Stretch(FloatingRoot);

            // --- Topo esquerdo: recursos em uma linha (ícone + número; o nome aparece ao passar o mouse) ---
            var top = UIKit.Panel("Recursos", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -12), new Vector2(0, 50));
            var topLayout = UIKit.Horizontal(top.gameObject, 12, 16);
            topLayout.padding = new RectOffset(14, 14, 6, 6);
            top.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            goldText = Resource(top, goldIcon, UIKit.GoldColor, () => "Ouro\nPaga construções, salários e contratações.");
            foreach (var item in vm.items)
            {
                if (item == null) continue;
                var it = item;
                itemTexts.Add((item, Resource(top, item.icon, item.textColor, () => $"{it.displayName}: {VillageManager.Instance.GetStock(it)} no estoque")));
            }
            crewText = Resource(top, crewIcon, UIKit.TextColor, () =>
            {
                var c = CrewManager.Instance;
                var v = VillageManager.Instance;
                return c == null ? "Tripulação" : $"Tripulação: {c.Crew.Count}\nCamas: {v.HousingCapacity} ({c.FreeBeds} livres)\nBeleza da vila: {v.Beauty} (+{v.BeautyMorale} de moral por dia)\nTecla C abre a lista.";
            });

            // --- Topo direito: dia + botão de ajuda ---
            var day = UIKit.Panel("Dia", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -12), new Vector2(150, 50));
            UIKit.Vertical(day.gameObject, 8, 2).padding = new RectOffset(14, 14, 6, 8);
            dayText = UIKit.Text(day, "", 17, TextAnchor.MiddleCenter, UIKit.GoldColor);
            UIKit.Size(dayText, height: 22);
            var barBg = UIKit.Rect("BarraDoDia", day);
            barBg.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);
            UIKit.Size(barBg, height: 6);
            var bar = UIKit.Rect("Preenchimento", barBg);
            UIKit.Stretch(bar);
            dayBar = bar.gameObject.AddComponent<Image>();
            dayBar.color = UIKit.GoldColor;
            UIKit.Tip(day, () => CrewManager.Instance == null ? null
                : $"Dia {CrewManager.Instance.Day}\nNo fim do dia cada tripulante come 1 peixe e recebe o salário.");

            var (helpBtn, _, _) = UIKit.Button(root, "?", ToggleHelp, 20);
            var hrt = (RectTransform)helpBtn.transform;
            hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(1, 1);
            hrt.sizeDelta = new Vector2(44, 50);
            hrt.anchoredPosition = new Vector2(-12 - 150 - 12, -12);
            UIKit.Tip(helpBtn, "Ajuda: teclas e comandos");

            helpPanel = UIKit.Panel("Ajuda", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -74), new Vector2(400, 150));
            var helpText = UIKit.Text(helpPanel,
                "WASD / setas: andar com o capitão\n" +
                "Martelo (B): construir · Shift: vários · botão direito: cancelar\n" +
                "Clique num prédio: ver detalhes\n" +
                "C: tripulação · I: equipamento · X: expedições · J: crônicas\n" +
                "Esc: menu (salvar, sair)", 14, TextAnchor.MiddleLeft);
            UIKit.Stretch(helpText.rectTransform, 14);
            UIKit.Fit(helpText, 10);
            helpPanel.gameObject.SetActive(false);

            // --- Registro de eventos (embaixo à esquerda) ---
            eventLog = EventLog.Create(root);
            foreach (var g in eventLog.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;   // não atrapalha cliques no mapa

            // --- Embaixo: barra de ícones ---
            var dock = dockPanel = UIKit.Panel("Barra", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, DockY), new Vector2(0, DockHeight));
            var dl = UIKit.Horizontal(dock.gameObject, 10, 8);
            dl.childAlignment = TextAnchor.MiddleCenter;
            dock.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            buildDock = DockButton(dock, UIKit.Skin.Icon("martelo"), "B", "Construir (B)", ToggleBuildMenu);
            crewDock = DockButton(dock, crewIcon, "C", "Tripulação (C)", ToggleCrew);
            equipDock = DockButton(dock, UIKit.Skin.Icon("espada"), "I", "Equipamento (I): armas, chapéus e amuletos", ToggleEquip);
            expDock = DockButton(dock, UIKit.Skin.Icon("pergaminho"), "X", "Expedições (X)\nPrecisa do Quadro de Expedições.", ToggleExpeditions);
            UIKit.Tip(expDock, () =>
            {
                var em = ExpeditionManager.Instance;
                int n = em != null ? em.Reports.Count : 0;
                return n > 0 ? $"Expedições (X)\n{n} prêmio(s) esperando para coletar!" : "Expedições (X)\nPrecisa do Quadro de Expedições.";
            });
            // Selo "!" quando alguma expedição voltou e o prêmio ainda não foi coletado
            expBadge = UIKit.Rect("Aviso", expDock.transform);
            expBadge.anchorMin = expBadge.anchorMax = expBadge.pivot = new Vector2(1, 1);
            expBadge.anchoredPosition = new Vector2(6, 6);
            expBadge.sizeDelta = new Vector2(22, 22);
            var badgeImg = expBadge.gameObject.AddComponent<Image>();
            UIKit.UseSkin(badgeImg, UIKit.Skin.Button, Image.Type.Sliced);
            badgeImg.color = new Color(1f, 0.42f, 0.3f);   // laranja-avermelhado: destaca até com o botão verde
            badgeImg.raycastTarget = false;
            var bang = UIKit.Text(expBadge, "!", 16, TextAnchor.MiddleCenter, Color.white);
            UIKit.Stretch(bang.rectTransform);
            bang.rectTransform.offsetMin = new Vector2(0, 3);
            bang.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1, -1);
            expBadge.gameObject.SetActive(false);
            chronDock = DockButton(dock, UIKit.Skin.Icon("livro"), "J", "Crônicas (J): as missões do bando", ToggleChronicles);
            DockButton(dock, UIKit.Skin.Icon("menu"), "Esc", "Menu (Esc): salvar, som, sair", () => { CloseWindows(); CloseBuildMenu(); menu.OpenPause(); });

            // --- Menu de construções (abre pelo martelo) ---
            BuildBuildMenu(root, vm);

            // --- Dica do modo construção / avisos ---
            hintPanel = UIKit.Panel("Dica", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, DockY + DockHeight + 14), new Vector2(760, 36));
            hintText = UIKit.Text(hintPanel, "", 16, TextAnchor.MiddleCenter);
            foreach (var g in hintPanel.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            UIKit.Stretch(hintText.rectTransform, 8);
            UIKit.Fit(hintText, 11);

            // --- Janelas ---
            tavernPanel = TavernPanel.Create(root);
            crewPanel = CrewPanel.Create(root);
            equipPanel = EquipPanel.Create(root, false, Toast);
            expeditionPanel = ExpeditionPanel.Create(root);
            ExpeditionStrip.Create(root);
            infoPanel = BuildingInfoPanel.Create(root, () => { infoPanel.Hide(); tavernPanel.Show(); }, () => { infoPanel.Hide(); expeditionPanel.Show(); });
            chronicles = ChronicleUI.Create(root);
            sandbox = SandboxPanel.Create(root, false, m => { Toast(m); return true; });
            menu = GameMenu.Create(root);
            menu.OpenStart();

            vm.Changed += Refresh;
            buildMode.SelectionChanged += Refresh;
            if (cm != null) cm.Changed += Refresh;
            Refresh();
        }

        void BuildBuildMenu(RectTransform root, VillageManager vm)
        {
            buildTray = UIKit.Panel("Construcoes", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, DockY + DockHeight + 14), new Vector2(0, 0));
            var tl = UIKit.Vertical(buildTray.gameObject, 14, 10);
            tl.childForceExpandWidth = false;
            tl.childAlignment = TextAnchor.UpperCenter;
            var fit = buildTray.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Abas das categorias + Demolir
            var tabRow = UIKit.Rect("Abas", buildTray);
            UIKit.Horizontal(tabRow.gameObject, 0, 6);
            for (int i = 0; i < Categories.Length; i++)
            {
                int c = i;
                var (tb, _, tbg) = UIKit.Button(tabRow, Categories[i], () => { category = c; Refresh(); }, 15);
                UIKit.Size(tb, 112, 36);
                tabs.Add(tbg);
            }
            var gap = UIKit.Rect("Espaco", tabRow);
            UIKit.Size(gap, 18, 10);
            var (db, dlabel, dbg) = UIKit.Button(tabRow, "Demolir", () =>
            {
                CloseWindows();
                if (buildMode.Demolishing) buildMode.Cancel(); else buildMode.StartDemolish();
            }, 15);
            UIKit.Size(db, 120, 36);
            var bomb = UIKit.Skin.Icon("bomba");
            if (bomb != null)
            {
                var ic = UIKit.Rect("Icone", db.transform);
                ic.anchorMin = ic.anchorMax = ic.pivot = new Vector2(0, 0.5f);
                ic.anchoredPosition = new Vector2(8, 2);
                ic.sizeDelta = new Vector2(24, 24);
                var im = ic.gameObject.AddComponent<Image>();
                im.sprite = bomb; im.preserveAspect = true; im.raycastTarget = false;
                dlabel.rectTransform.offsetMin = new Vector2(30, 8);
            }
            UIKit.Tip(db, "Demolir: clique num prédio para derrubar (devolve metade do custo), numa árvore ou pedra para limpar o terreno (rende madeira ou pedra), ou num caminho seu para tirar.");
            demolishBg = dbg;

            // Caminho: pinta caminhos de terra (todo mundo anda mais rápido neles)
            var (pb, plabel, pbg) = UIKit.Button(tabRow, "Caminho", () =>
            {
                CloseWindows();
                if (buildMode.Paving) buildMode.Cancel(); else buildMode.StartPaving();
            }, 15);
            UIKit.Size(pb, 120, 36);
            var shovel = UIKit.Skin.Icon("pa");
            if (shovel != null)
            {
                var ic = UIKit.Rect("Icone", pb.transform);
                ic.anchorMin = ic.anchorMax = ic.pivot = new Vector2(0, 0.5f);
                ic.anchoredPosition = new Vector2(8, 2);
                ic.sizeDelta = new Vector2(24, 24);
                var im = ic.gameObject.AddComponent<Image>();
                im.sprite = shovel; im.preserveAspect = true; im.raycastTarget = false;
                plabel.rectTransform.offsetMin = new Vector2(30, 8);
            }
            UIKit.Tip(pb, () => $"Caminho: clique ou arraste para fazer caminhos de terra ({VillageManager.Instance.pathCost} ouro por bloco).\nTodo mundo anda {Mathf.RoundToInt((VillageManager.Instance.pathSpeed - 1f) * 100)}% mais rápido em cima deles.\nPara tirar: Demolir e clique no caminho.");
            pathBg = pbg;

            // Cartões das construções (os de outras categorias ficam escondidos)
            var grid = UIKit.Rect("Cartoes", buildTray);
            UIKit.Horizontal(grid.gameObject, 0, 8);
            foreach (var def in vm.buildable)
            {
                if (def == null) continue;
                var d = def;
                var rt = UIKit.Rect("Cartao", grid);
                var (btn, bg) = UIKit.ButtonBase(rt);
                btn.onClick.AddListener(() =>
                {
                    CloseWindows();
                    if (buildMode.Selected == d) buildMode.Cancel(); else buildMode.Select(d);
                });
                UIKit.Size(rt, 112, 104, flexWidth: 0);
                var v = UIKit.Vertical(rt.gameObject, 6, 0);
                v.padding = new RectOffset(6, 6, 6, 10);
                v.childAlignment = TextAnchor.UpperCenter;

                var iconRt = UIKit.Rect("Icone", rt);
                var img = iconRt.gameObject.AddComponent<Image>();
                img.sprite = def.icon != null ? def.icon : def.sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                UIKit.Size(iconRt, height: 42);
                var name = UIKit.Text(rt, def.displayName, 14, TextAnchor.MiddleCenter, UIKit.ButtonText);
                UIKit.Size(name, height: 22);
                UIKit.Fit(name, 10);
                // Custo: ouro e materiais, cada um com ícone (fica vermelho o que falta)
                var costRow = UIKit.Rect("Custo", rt);
                var ch = UIKit.Horizontal(costRow.gameObject, 0, 3);
                ch.childAlignment = TextAnchor.MiddleCenter;
                UIKit.Size(costRow, height: 18);
                var cost = new List<(Text, ItemDef, int)>();
                void CostPart(Sprite icon, string label, ItemDef item, int need)
                {
                    var im = UIKit.Rect("Icone", costRow).gameObject.AddComponent<Image>();
                    im.sprite = icon; im.preserveAspect = true; im.raycastTarget = false;
                    UIKit.Size(im, 14, 14);
                    var t = UIKit.Text(costRow, label, 13, TextAnchor.MiddleLeft, UIKit.ButtonText);
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    cost.Add((t, item, need));
                }
                CostPart(goldIcon, d.cost.ToString(), null, d.cost);
                if (d.materials != null)
                    foreach (var m in d.materials)
                        if (m.item != null && m.amount > 0) CostPart(m.item.icon, m.amount.ToString(), m.item, m.amount);

                UIKit.Tip(rt, () =>
                {
                    string s = $"{d.displayName} · {VillageManager.CostText(d)}\n{d.description}\nTamanho: {d.footprint.x}x{d.footprint.y}";
                    if (d.beauty > 0) s += $"\nBeleza +{d.beauty}";
                    if (!VillageManager.Instance.CanAfford(d, out string miss)) s += "\n" + UIKit.Colored(miss, new Color(1f, 0.5f, 0.45f));
                    return s;
                });
                buildButtons.Add((def, btn, bg, cost));
            }
            buildTray.gameObject.SetActive(false);
        }

        void Update()
        {
            if (hintText == null) return;
            toastTimer -= Time.unscaledDeltaTime;
            string hint = !string.IsNullOrEmpty(buildMode.Hint) ? buildMode.Hint
                : toastTimer > 0 ? toast : Prompt;
            hintPanel.gameObject.SetActive(!string.IsNullOrEmpty(hint));
            hintText.text = buildMode.Active && !string.IsNullOrEmpty(buildMode.Hint)
                ? hint + UIKit.Colored("   ·   botão direito: cancelar", UIKit.MutedText) : hint;

            // Posicionando um prédio: o que fica embaixo some, para dar para construir perto da praia.
            // Ao colocar (ou cancelar) o menu de construções volta sozinho.
            bool placing = buildMode.Active;
            if (buildTray.gameObject.activeSelf != (trayOpen && !placing)) buildTray.gameObject.SetActive(trayOpen && !placing);
            if (dockPanel.gameObject.activeSelf == placing) dockPanel.gameObject.SetActive(!placing);
            if (eventLog.gameObject.activeSelf == placing) eventLog.gameObject.SetActive(!placing);
            if (placing)
            {
                // Dica no topo, sem cobrir a parte de baixo do mapa
                hintPanel.anchorMin = hintPanel.anchorMax = hintPanel.pivot = new Vector2(0.5f, 1);
                hintPanel.sizeDelta = new Vector2(520, 36);
                hintPanel.anchoredPosition = new Vector2(0, -12);
            }
            else
            {
                // A dica fica logo acima do que estiver aberto embaixo (barra ou menu de construções)
                float y = DockY + DockHeight + 14;
                if (buildTray.gameObject.activeSelf) y += buildTray.rect.height + 14;
                hintPanel.anchorMin = hintPanel.anchorMax = hintPanel.pivot = new Vector2(0.5f, 0);
                hintPanel.sizeDelta = new Vector2(760, 36);
                hintPanel.anchoredPosition = new Vector2(0, y);
            }

            var cm = CrewManager.Instance;
            if (cm != null)
            {
                dayText.text = $"Dia {cm.Day}";
                ((RectTransform)dayBar.transform).anchorMax = new Vector2(cm.DayProgress, 1);
            }

            // Botões da barra ficam verdes enquanto a janela deles está aberta
            Mark(buildDock, BuildMenuOpen || buildMode.Active);
            Mark(crewDock, crewPanel.gameObject.activeSelf);
            Mark(equipDock, equipPanel.IsOpen);
            Mark(expDock, expeditionPanel.IsOpen);
            var exm = ExpeditionManager.Instance;
            bool pending = exm != null && exm.Reports.Count > 0;
            if (expBadge.gameObject.activeSelf != pending) expBadge.gameObject.SetActive(pending);
            if (pending) expBadge.localScale = Vector3.one * (1f + 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f)));
            Mark(chronDock, chronicles.WindowOpen);

            HandleInput();
        }

        static void Mark(Image bg, bool on)
        {
            var want = on ? UIKit.Skin.ButtonOn : UIKit.Skin.Button;
            if (bg != null && want != null && bg.sprite != want) UIKit.SetSelected(bg, on);
        }

        void HandleInput()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (buildMode.Active || buildMode.LastActionFrame == Time.frameCount || GameMenu.IsOpen) return;

            if (kb != null && kb.bKey.wasPressedThisFrame) ToggleBuildMenu();
            if (kb != null && kb.cKey.wasPressedThisFrame) ToggleCrew();
            if (kb != null && kb.iKey.wasPressedThisFrame) ToggleEquip();
            if (kb != null && kb.xKey.wasPressedThisFrame) ToggleExpeditions();
            if (kb != null && kb.jKey.wasPressedThisFrame) ToggleChronicles();
            if (kb != null && kb.escapeKey.wasPressedThisFrame && sandbox != null && sandbox.IsOpen) { sandbox.Hide(); return; }
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                bool anyOpen = tavernPanel.gameObject.activeSelf || crewPanel.gameObject.activeSelf || infoPanel.gameObject.activeSelf || equipPanel.IsOpen
                               || expeditionPanel.IsOpen || chronicles.WindowOpen || BuildMenuOpen || helpPanel.gameObject.activeSelf;
                if (anyOpen) { CloseWindows(); CloseBuildMenu(); helpPanel.gameObject.SetActive(false); }
                else menu.OpenPause();
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && cam != null)
            {
                bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (overUI) return;
                Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                var b = VillageManager.Instance.BuildingAt(world);
                if (b != null) { CloseWindows(); infoPanel.Show(b); }
                else infoPanel.Hide();
            }
        }

        void ToggleBuildMenu()
        {
            if (BuildMenuOpen) { CloseBuildMenu(); return; }
            CloseWindows();
            trayOpen = true;
            buildTray.gameObject.SetActive(true);
            buildTray.SetAsLastSibling();
            Refresh();
        }

        void CloseBuildMenu()
        {
            if (buildMode.Active) buildMode.Cancel();
            trayOpen = false;
            buildTray.gameObject.SetActive(false);
        }

        void ToggleHelp() => helpPanel.gameObject.SetActive(!helpPanel.gameObject.activeSelf);

        void ToggleCrew()
        {
            bool open = crewPanel.gameObject.activeSelf;
            CloseWindows();
            CloseBuildMenu();
            if (!open) crewPanel.Show();
        }

        void ToggleEquip()
        {
            bool open = equipPanel.IsOpen;
            CloseWindows();
            CloseBuildMenu();
            if (!open) equipPanel.Show();
        }

        void ToggleExpeditions()
        {
            bool open = expeditionPanel.IsOpen;
            CloseWindows();
            CloseBuildMenu();
            if (open) return;
            if (ExpeditionManager.Instance == null || (!ExpeditionManager.Instance.HasBoard && ExpeditionManager.Instance.Reports.Count == 0)) { Toast("Construa o Quadro de Expedições (em Serviços) para mandar grupos às masmorras."); return; }
            expeditionPanel.Show();
        }

        void ToggleChronicles()
        {
            bool open = chronicles.WindowOpen;
            CloseWindows();
            CloseBuildMenu();
            if (!open) chronicles.ToggleWindow();
        }

        void CloseWindows()
        {
            expeditionPanel.Hide();
            chronicles.HideWindow();
            tavernPanel.Hide();
            crewPanel.Hide();
            equipPanel.Hide();
            infoPanel.Hide();
        }

        void Refresh()
        {
            var vm = VillageManager.Instance;
            if (vm == null || goldText == null) return;
            var cm = CrewManager.Instance;
            goldText.text = vm.Gold.ToString();
            foreach (var (item, text) in itemTexts) text.text = vm.GetStock(item).ToString();
            if (cm != null) crewText.text = $"{cm.Crew.Count}/{vm.HousingCapacity}";

            // Se o prédio escolhido é de outra aba, mostra a aba dele
            if (buildMode.Selected != null) category = CategoryOf(buildMode.Selected);
            for (int i = 0; i < tabs.Count; i++) UIKit.SetSelected(tabs[i], i == category);
            foreach (var (def, btn, bg, cost) in buildButtons)
            {
                btn.gameObject.SetActive(CategoryOf(def) == category);
                UIKit.SetSelected(bg, buildMode.Selected == def);
                bool afford = vm.CanAfford(def, out _);
                btn.interactable = afford || buildMode.Selected == def;
                foreach (var (t, item, need) in cost)
                {
                    int have = item == null ? vm.Gold : vm.GetStock(item);
                    t.color = have >= need ? UIKit.ButtonText : Lacking;
                }
            }
            UIKit.SetSelected(demolishBg, buildMode.Demolishing);
            UIKit.SetSelected(pathBg, buildMode.Paving);
        }

        Text Resource(Transform parent, Sprite icon, Color color, System.Func<string> tip)
        {
            var row = UIKit.Rect("Recurso", parent);
            UIKit.Horizontal(row.gameObject, 0, 5).childAlignment = TextAnchor.MiddleLeft;
            var hit = row.gameObject.AddComponent<Image>();
            hit.color = Color.clear;                 // só para o mouse "enxergar" o recurso e mostrar a dica
            var iconRt = UIKit.Rect("Icone", row);
            var img = iconRt.gameObject.AddComponent<Image>();
            img.sprite = icon;
            img.enabled = icon != null;
            img.preserveAspect = true;
            img.raycastTarget = false;
            UIKit.Size(iconRt, 26, 26);
            var t = UIKit.Text(row, "", 18, TextAnchor.MiddleLeft, color);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
            UIKit.Tip(row, tip);
            return t;
        }

        Image DockButton(Transform parent, Sprite icon, string key, string tip, UnityEngine.Events.UnityAction onClick)
        {
            var rt = UIKit.Rect("Botao", parent);
            var (btn, bg) = UIKit.ButtonBase(rt);
            btn.onClick.AddListener(onClick);
            btn.onClick.AddListener(() => AudioManager.Play(Sfx.Click));
            UIKit.Size(rt, 58, 58);

            var iconRt = UIKit.Rect("Icone", rt);
            iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(0, 3);
            iconRt.sizeDelta = new Vector2(32, 32);
            var img = iconRt.gameObject.AddComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            img.raycastTarget = false;

            // Tecla de atalho no canto
            var k = UIKit.Text(rt, key, 10, TextAnchor.LowerRight, UIKit.ButtonText);
            var krt = k.rectTransform;
            krt.anchorMin = Vector2.zero; krt.anchorMax = Vector2.one;
            krt.offsetMin = new Vector2(2, 7); krt.offsetMax = new Vector2(-5, -2);
            UIKit.Tip(rt, tip);
            return bg;
        }
    }
}
