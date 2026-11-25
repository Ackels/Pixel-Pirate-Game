using Pixel.Combat;
using Pixel.Village;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Pixel.Ship
{
    /// <summary>Interface da cena do Mar: ouro, atributos do navio, botões de ação e paleta do estaleiro.</summary>
    public class SeaHUD : MonoBehaviour
    {
        public SeaController sea;
        [Tooltip("Papel, casa, porto, selvagem, acampamento, desconhecido, navio")]
        public Sprite[] mapIcons;
        MapPanel map;
        RectTransform trade;

        Text goldText, fishText, rumText, crewText, titleText, dayText, hintText, helpText, shipLine;
        Image dayBar, legBar, hullBar, captainBar;
        Text hullText, captainText;
        GameObject captainRow, legRow;
        RectTransform actionBar, helpPanel, hintRect, shipyard;
        BlockCategory shipCategory = BlockCategory.Casco;
        readonly System.Collections.Generic.Dictionary<string, ShipBlockDef> familyChoice = new System.Collections.Generic.Dictionary<string, ShipBlockDef>();
        SeaMode builtFor = (SeaMode)(-1);
        EncounterState builtEncounter = (EncounterState)(-1);
        bool builtCanBoard, builtFiring, builtHostile;
        int buttonsBuilt;
        readonly System.Collections.Generic.List<Button> builtButtons = new System.Collections.Generic.List<Button>();
        bool builtPaused, builtDeciding, builtPlunder, builtAway;
        CrewCommander commander;
        Text enemyText;
        string message;
        float messageTimer;

        void Start()
        {
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("Canvas_Mar", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            SandboxPanel.Create(root, true, m => { sea.Say(m); return true; });
            // Ordens à tripulação (pausa tática com P e arrastar a espada) — fica embaixo das janelas
            commander = gameObject.AddComponent<CrewCommander>();
            commander.Init((RectTransform)root, sea);
            SeaDialog.Create(root);   // tela de decisão (encontros e eventos), por cima de tudo

            // --- Topo esquerdo: recursos (igual à vila; o nome aparece ao passar o mouse) ---
            var vhud = VillageHUD.Instance;
            var cm = CrewManager.Instance;
            var top = UIKit.Panel("Recursos", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -12), new Vector2(0, 50));
            UIKit.Horizontal(top.gameObject, 12, 16).padding = new RectOffset(14, 14, 6, 6);
            top.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            goldText = Resource(top, vhud != null ? vhud.goldIcon : null, UIKit.GoldColor, () => "Ouro\nO mesmo cofre da vila.");
            if (cm != null && cm.food != null)
                fishText = Resource(top, cm.food.icon, cm.food.textColor, () => $"{cm.food.displayName}: provisões da viagem (saem do estoque da vila).");
            if (cm != null && cm.rum != null)
                rumText = Resource(top, cm.rum.icon, cm.rum.textColor, () => $"{cm.rum.displayName} no estoque da vila. Venda nos portos!");
            crewText = Resource(top, vhud != null ? vhud.crewIcon : null, UIKit.TextColor,
                () => $"A bordo: você + {sea.CrewFighters.Count} tripulante(s)\nVagas no navio: {Mathf.Max(0, sea.Stats.MaxCrew - 1)}");

            // --- Logo abaixo: o navio (casco e capitão em barras; o resto na dica) ---
            var ship = UIKit.Panel("Navio", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -76), new Vector2(250, 0));
            var sv = UIKit.Vertical(ship.gameObject, 0, 4);
            sv.padding = new RectOffset(12, 12, 8, 10);
            ship.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            (hullBar, hullText) = Bar(ship, "Casco", out _);
            (captainBar, captainText) = Bar(ship, "Capitão", out captainRow);
            shipLine = UIKit.Text(ship, "", 13, TextAnchor.MiddleLeft, UIKit.MutedText);
            UIKit.Size(shipLine, height: 18);
            UIKit.Tip(ship, ShipDetails);

            // --- Topo direito: dia + ajuda ---
            var day = UIKit.Panel("Dia", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -12), new Vector2(150, 50));
            UIKit.Vertical(day.gameObject, 8, 2).padding = new RectOffset(14, 14, 6, 8);
            dayText = UIKit.Text(day, "", 17, TextAnchor.MiddleCenter, UIKit.GoldColor);
            UIKit.Size(dayText, height: 22);
            dayBar = BarFill(day, UIKit.GoldColor);
            UIKit.Tip(day, () => "O dia continua correndo na vila enquanto você navega.");

            var (helpBtn, _, _) = UIKit.Button(root, "?", () => helpPanel.gameObject.SetActive(!helpPanel.gameObject.activeSelf), 20);
            var hrt = (RectTransform)helpBtn.transform;
            hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(1, 1);
            hrt.sizeDelta = new Vector2(44, 50);
            hrt.anchoredPosition = new Vector2(-12 - 150 - 12, -12);
            UIKit.Tip(helpBtn, "Ajuda: teclas e comandos");
            helpPanel = UIKit.Panel("Ajuda", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -74), new Vector2(330, 0));
            UIKit.Vertical(helpPanel.gameObject, 14, 0);
            helpPanel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            helpText = UIKit.Text(helpPanel, "", 14, TextAnchor.UpperLeft);
            helpPanel.gameObject.SetActive(false);

            // --- Centro do topo: onde estou / para onde vou (com a barra da viagem) ---
            var titlePanel = UIKit.Panel("Titulo", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(380, 0));
            var tv = UIKit.Vertical(titlePanel.gameObject, 0, 3);
            tv.padding = new RectOffset(14, 14, 6, 8);
            titlePanel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            titleText = UIKit.Text(titlePanel, "", 20, TextAnchor.MiddleCenter, UIKit.GoldColor);
            UIKit.Size(titleText, height: 26);
            UIKit.Fit(titleText, 12);
            legBar = BarFill(titlePanel, new Color(0.55f, 0.85f, 1f));
            legRow = legBar.transform.parent.gameObject;

            var enemy = UIKit.Panel("Inimigo", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(380, 54),
                new Color(0.35f, 0.1f, 0.08f, 0.9f));
            enemyText = UIKit.Text(enemy, "", 16, TextAnchor.MiddleCenter);
            UIKit.Stretch(enemyText.rectTransform, 6);

            // --- Embaixo: ações ---
            actionBar = UIKit.Panel("Acoes", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(0, 84));
            var layout = UIKit.Horizontal(actionBar.gameObject, 8, 8);
            layout.childAlignment = TextAnchor.MiddleCenter;
            actionBar.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            shipyard = UIKit.Panel("Estaleiro", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(0, 0));
            var sv2 = UIKit.Vertical(shipyard.gameObject, 12, 8);
            sv2.childForceExpandWidth = false;
            sv2.childAlignment = TextAnchor.UpperCenter;
            var sfit = shipyard.gameObject.AddComponent<ContentSizeFitter>();
            sfit.horizontalFit = sfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            shipyard.gameObject.SetActive(false);
            sea.editor.StateChanged += () => { if (sea.Mode == SeaMode.Editing) BuildShipyard(); };

            var hintPanel = UIKit.Panel("Dica", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(760, 36));
            hintRect = hintPanel;
            foreach (var g in hintPanel.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            hintText = UIKit.Text(hintPanel, "", 16, TextAnchor.MiddleCenter);
            UIKit.Stretch(hintText.rectTransform, 8);
            UIKit.Fit(hintText, 11);

            // No estaleiro a barra só é refeita ao trocar de bloco (para não "engolir" cliques).
            sea.Changed += () => { if (sea.Mode != SeaMode.Editing) Refresh(); };
            sea.Message += m => { message = m; messageTimer = 4.5f; };
            map = MapPanel.Create(root, sea, mapIcons);
            sea.MapRequested += map.Show;
            BuildTrade(root);
            sea.TradeRequested += () => { trade.gameObject.SetActive(true); trade.SetAsLastSibling(); };
            sea.Changed += () => { if (sea.Mode != SeaMode.Landed && trade != null) trade.gameObject.SetActive(false); };
            Refresh();
        }

        void Update()
        {
            if (sea == null) return;
            var director = EncounterDirector.Instance;
            var est = director != null ? director.State : EncounterState.None;
            bool canBoard = director != null && director.CanBoard;
            bool firing = director != null && director.PlayerFiring, hostile = director != null && director.Hostile;
            if (builtFor != sea.Mode || builtEncounter != est || canBoard != builtCanBoard || firing != builtFiring || hostile != builtHostile
                || CrewCommander.Paused != builtPaused
                || (director != null && (director.Deciding != builtDeciding || director.CanPlunder != builtPlunder || director.CaptainAway != builtAway)))
            {
                builtAway = director != null && director.CaptainAway;
                builtPaused = CrewCommander.Paused;
                builtDeciding = director != null && director.Deciding;
                builtPlunder = director != null && director.CanPlunder;
                builtCanBoard = canBoard;
                builtFiring = firing;
                builtHostile = hostile;
                Refresh();
            }

            enemyText.transform.parent.gameObject.SetActive(est != EncounterState.None);
            if (director != null && est != EncounterState.None)
            {
                string crewInfo = $" · Tripulação: {director.EnemyCrewAlive}";
                enemyText.text = UIKit.Colored(director.EnemyName, UIKit.GoldColor) + $"\nCasco: {director.EnemyHp}/{director.EnemyMaxHp}{crewInfo}";
            }

            var kb = Keyboard.current;
            if (sea.Mode == SeaMode.Editing && kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (sea.editor.Selected != null) { sea.editor.Selected = null; BuildShipyard(); }
                else sea.StopEditing();
            }
            else if (kb != null && kb.escapeKey.wasPressedThisFrame && helpPanel.gameObject.activeSelf) helpPanel.gameObject.SetActive(false);

            messageTimer -= Time.unscaledDeltaTime;
            string hint = messageTimer > 0 ? message
                : sea.Mode == SeaMode.Editing ? sea.editor.Hint
                : sea.NearbyInteractable != null ? sea.NearbyInteractable.prompt
                : sea.NearDigMark ? "Tem um X aqui! G (ou o botão da pá): cavar" : null;
            hintText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(hint));
            hintText.text = hint;
            // No estaleiro a dica vai para o topo (embaixo fica a barra de peças)
            bool top = sea.Mode == SeaMode.Editing;
            hintRect.anchorMin = hintRect.anchorMax = hintRect.pivot = new Vector2(0.5f, top ? 1 : 0);
            hintRect.anchoredPosition = new Vector2(0, top ? -64 : 110);

            // Recursos e dia
            var cm = CrewManager.Instance;
            var vm = VillageManager.Instance;
            goldText.text = sea.Gold.ToString();
            if (fishText != null && vm != null) fishText.text = vm.GetStock(cm.food).ToString();
            if (rumText != null && vm != null) rumText.text = vm.GetStock(cm.rum).ToString();
            crewText.text = $"{1 + sea.CrewFighters.Count}/{Mathf.Max(1, sea.Stats.MaxCrew)}";
            if (cm != null)
            {
                dayText.text = $"Dia {cm.Day}";
                ((RectTransform)dayBar.transform).anchorMax = new Vector2(cm.DayProgress, 1);
            }

            // Viagem
            bool sailing = sea.Mode == SeaMode.Sailing && sea.Destination != null;
            legRow.SetActive(sailing);
            if (sailing) ((RectTransform)legBar.transform).anchorMax = new Vector2(sea.LegProgress, 1);

            UpdateShip();
        }

        void UpdateShip()
        {
            var s = sea.Stats;
            SetBar(hullBar, hullText, sea.ShipHp, s.hp);
            var cap = sea.captainCombatant;
            captainRow.SetActive(cap != null);
            if (cap != null) SetBar(captainBar, captainText, cap.hp, cap.maxHp);
            shipLine.text = s.speedKnots > 0 ? $"{s.speedKnots:0.0} nós · {s.cannons} canhões"
                : UIKit.Colored("Parado: o navio não tem velas", new Color(1f, 0.5f, 0.45f));
        }

        string ShipDetails()
        {
            var s = sea.Stats;
            string speed = s.speedKnots > 0 ? $"{s.speedKnots:0.0} nós" : "parado (sem velas)";
            return $"Navio\nCasco: {sea.ShipHp}/{s.hp}\nVelocidade: {speed}\nCanhões: {s.cannons}\n" +
                   $"Tripulação máx.: {s.MaxCrew} (você + {Mathf.Max(0, s.MaxCrew - 1)})\nBlocos: {s.blocks} · Valor: {s.value} ouro";
        }

        static void SetBar(Image fill, Text label, int cur, int max)
        {
            float f = max > 0 ? Mathf.Clamp01((float)cur / max) : 0f;
            ((RectTransform)fill.transform).anchorMax = new Vector2(f, 1);
            fill.color = f > 0.6f ? new Color(0.45f, 0.85f, 0.4f) : f > 0.3f ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.4f, 0.35f);
            label.text = $"{cur}/{max}";
        }

        /// <summary>Linha "Nome [barra] 70/70".</summary>
        (Image, Text) Bar(Transform parent, string name, out GameObject row)
        {
            var r = UIKit.Rect(name, parent);
            row = r.gameObject;
            UIKit.Horizontal(r.gameObject, 0, 6).childAlignment = TextAnchor.MiddleLeft;
            UIKit.Size(r, height: 18);
            var n = UIKit.Text(r, name, 13, TextAnchor.MiddleLeft);
            UIKit.Size(n, 58, 18);
            var bg = UIKit.Rect("Fundo", r);
            bg.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.55f);
            UIKit.Size(bg, 96, 10);
            var fill = UIKit.Rect("Preenchimento", bg);
            UIKit.Stretch(fill);
            var img = fill.gameObject.AddComponent<Image>();
            var t = UIKit.Text(r, "", 13, TextAnchor.MiddleLeft);
            UIKit.Size(t, 60, 18);
            return (img, t);
        }

        /// <summary>Barra fina que ocupa a largura do painel (dia, viagem).</summary>
        static Image BarFill(Transform parent, Color color)
        {
            var bg = UIKit.Rect("Barra", parent);
            bg.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);
            UIKit.Size(bg, height: 6);
            var fill = UIKit.Rect("Preenchimento", bg);
            UIKit.Stretch(fill);
            var img = fill.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        Text Resource(Transform parent, Sprite icon, Color color, System.Func<string> tip)
        {
            var row = UIKit.Rect("Recurso", parent);
            UIKit.Horizontal(row.gameObject, 0, 5).childAlignment = TextAnchor.MiddleLeft;
            row.gameObject.AddComponent<Image>().color = Color.clear;   // para a dica do mouse
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

        void Refresh()
        {
            if (actionBar == null) return;
            builtFor = sea.Mode;
            var director = EncounterDirector.Instance;
            builtEncounter = director != null ? director.State : EncounterState.None;
            UIKit.Clear(actionBar);
            buttonsBuilt = 0;
            builtButtons.Clear();
            BuildShipyard();

            switch (sea.Mode)
            {
                case SeaMode.Docked:
                    titleText.text = "Atracado na Enseada";
                    helpText.text = "A/D ou setas: andar · Espaço: pular";
                    bool canEdit = sea.CanEdit(out string editWhy);
                    ActionButton(canEdit ? "Editar navio\n(Estaleiro)" : "Editar navio\n(precisa de Estaleiro)", sea.StartEditing, canEdit);
                    if (sea.RepairCost > 0)
                        ActionButton($"Consertar casco\n({sea.RepairCost} ouro)", sea.Repair, sea.Gold >= sea.RepairCost);
                    ActionButton("Zarpar!\n(abrir mapa)", sea.OpenMap, true);
                    ActionButton("Voltar à vila", sea.GoHome, true);
                    break;

                case SeaMode.Editing:
                    titleText.text = "Estaleiro — editando o navio";
                    helpText.text = "Clique ou arraste: colocar\nBotão direito (ou arrastar): remover\nR ou rodinha do mouse: girar a peça\nM: espelho (constrói dos dois lados)\nEsc: soltar a peça / sair";

                    break;

                case SeaMode.Sailing:
                    bool fighting = builtEncounter != EncounterState.None;
                    titleText.text = fighting ? "Combate!" : sea.Destination != null ? $"Rumo a {(sea.Destination.revealed ? sea.Destination.name : "???")}" : "Navegando";
                    helpText.text = "A/D: andar · Espaço: pular · F/J: golpear\nClique num inimigo: o capitão vai lá e ataca\nArraste um tripulante até um inimigo ou um lugar: ordem\nP: pausa tática (dar ordens com o jogo parado)";
                    if (fighting) PauseButton();
                    if (director != null && director.State == EncounterState.Engaged)
                    {
                        // Ordens do capitão (como no Pixel Piracy): nada acontece até você mandar
                        if (director.HasCannons) ActionButton(director.PlayerFiring ? "Cessar fogo" : "Atirar!\n(canhões)", director.ToggleFire, true);
                        ActionButton("Abordar!\n(ganchos)", director.Board, director.CanBoard);
                    }
                    if (director != null && director.CanFlee) ActionButton(director.Hostile ? "Fugir" : "Seguir viagem", director.Flee, true);
                    if (director != null && director.Deciding)
                    {
                        // Navio rendido: o capitão decide (como no Pixel Piracy)
                        ActionButton("Capturar\no navio", director.Capture, true);
                        if (director.CaptainAway) ActionButton("Voltar ao\nnavio", director.ReturnAll, true);
                        ActionButton(director.CanPlunder ? "Saquear\n(e explodir)" : "Saquear\n(todos a bordo)", director.Plunder, director.CanPlunder);
                        ActionButton("Deixar\nir", director.LetGo, true);
                    }
                    if (!fighting) ActionButton("Ver mapa", map.Show, true);
                    break;

                case SeaMode.Landed:
                    titleText.text = sea.Location != null ? sea.Location.name : "Em terra";
                    helpText.text = "A/D: andar · Espaço: pular · F/J: golpear · E: abrir baú / negociar\nClique num inimigo: o capitão vai lá e ataca\nArraste um tripulante até um inimigo ou um lugar: ordem\nP: pausa tática";
                    PauseButton();
                    ActionButton("Zarpar\n(abrir mapa)", sea.OpenMap, true);
                    break;
            }
            // Sem botões (ex.: no meio da abordagem), a barra some em vez de ficar uma caixinha vazia
            if (sea.Mode != SeaMode.Editing)
            {
                actionBar.gameObject.SetActive(buttonsBuilt > 0);
                // Muitos botões: ficam mais estreitos para não cobrir o painel do personagem nem a barra de comandos
                float w = buttonsBuilt >= 5 ? 126f : buttonsBuilt == 4 ? 150f : 170f;
                foreach (var b in builtButtons) if (b != null) UIKit.Size(b, w, 64);
            }
        }

        void PauseButton()
        {
            if (sea.CrewFighters.Count == 0 && !CrewCommander.Paused) return;
            ActionButton(CrewCommander.Paused ? "Continuar\n(P)" : "Pausa tática\n(P)", () => commander.Toggle(), true);
        }

        void BuildTrade(Transform root)
        {
            RectTransform win = null;
            var (w, content) = UIKit.Window("Comerciante", root, "Comerciante do Porto", new Vector2(460, 300), () => win.gameObject.SetActive(false));
            win = w;
            trade = w;
            UIKit.Vertical(content.gameObject, 4, 8);
            UIKit.Info(w, () => "Peixe para a viagem e bom preço no seu rum.\nAs compras vão direto para o estoque da vila.");
            void Btn(string label, UnityEngine.Events.UnityAction a)
            {
                var (b, _, _) = UIKit.Button(content, label, a, 16);
                UIKit.Size(b, height: 46);
            }
            Btn($"Comprar {SeaController.FishPackSize} peixes ({SeaController.FishPackPrice} ouro)", sea.BuyFish);
            Btn($"Vender 1 rum ({SeaController.PortRumPrice} ouro)", () => sea.SellRum(false));
            Btn($"Vender todo o rum ({SeaController.PortRumPrice} ouro cada)", () => sea.SellRum(true));
            w.gameObject.SetActive(false);
        }

        // ---------- Estaleiro: abas, peças, cores, girar e espelho ----------
        static readonly (BlockCategory cat, string name)[] ShipTabs =
        {
            (BlockCategory.Casco, "Casco"), (BlockCategory.Estrutura, "Estrutura"), (BlockCategory.Velas, "Velas"),
            (BlockCategory.Armas, "Armas"), (BlockCategory.Enfeites, "Enfeites"),
        };

        void BuildShipyard()
        {
            bool editing = sea.Mode == SeaMode.Editing;
            shipyard.gameObject.SetActive(editing);
            actionBar.gameObject.SetActive(!editing);
            if (!editing) return;
            UIKit.Clear(shipyard);
            var ed = sea.editor;

            // Abas + girar + espelho
            var tabs = UIKit.Rect("Abas", shipyard);
            UIKit.Horizontal(tabs.gameObject, 0, 6);
            foreach (var (cat, name) in ShipTabs)
            {
                var c = cat;
                var (tb, _, tbg) = UIKit.Button(tabs, name, () => { shipCategory = c; BuildShipyard(); }, 15);
                UIKit.SetSelected(tbg, c == shipCategory);
                UIKit.Size(tb, 104, 34);
            }
            var gap = UIKit.Rect("Espaco", tabs);
            UIKit.Size(gap, 16, 10);
            var (rb, rl, rbg) = UIKit.Button(tabs, "Girar (R)", () => ed.Rotate(), 14);
            UIKit.Size(rb, 96, 34);
            rb.interactable = ed.Selected != null && ShipShapes.Orientations(ed.Selected.shape) > 1;
            UIKit.Tip(rb, "Gira a peça (rampa, curvo, meio bloco). A rodinha do mouse também gira.");
            var (mb, _, mbg) = UIKit.Button(tabs, "Espelho (M)", ed.ToggleMirror, 14);
            UIKit.SetSelected(mbg, ed.Mirror);
            UIKit.Size(mb, 110, 34);
            UIKit.Tip(mb, "Espelho: tudo o que você coloca ou tira aparece também do outro lado do eixo azul (o meio do navio).");
            var gap2 = UIKit.Rect("Espaco", tabs);
            UIKit.Size(gap2, 16, 10);
            var (done, _, doneBg) = UIKit.Button(tabs, "Concluir", sea.StopEditing, 15);
            UIKit.SetSelected(doneBg, true);
            UIKit.Size(done, 110, 34);
            UIKit.Tip(done, "Sair do estaleiro (Esc).");

            // Peças da aba (uma por família; a cor escolhida fica lembrada)
            var row = UIKit.Rect("Pecas", shipyard);
            UIKit.Horizontal(row.gameObject, 0, 6).childAlignment = TextAnchor.MiddleLeft;
            var families = new System.Collections.Generic.List<string>();
            var members = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ShipBlockDef>>();
            foreach (var d in sea.Palette)
            {
                if (d == null || d.category != shipCategory) continue;
                if (!members.TryGetValue(d.Family, out var l)) { l = new System.Collections.Generic.List<ShipBlockDef>(); members[d.Family] = l; families.Add(d.Family); }
                l.Add(d);
            }
            System.Collections.Generic.List<ShipBlockDef> selectedFamily = null;
            foreach (var fam in families)
            {
                var l = members[fam];
                if (!familyChoice.TryGetValue(fam, out var def) || !l.Contains(def)) def = l[0];
                if (ed.Selected != null && l.Contains(ed.Selected)) { def = ed.Selected; selectedFamily = l; }
                PieceButton(row, def, ed.Selected == def);
            }

            // Cores da peça escolhida
            if (selectedFamily != null && selectedFamily.Count > 1)
            {
                var sep = UIKit.Rect("Espaco", row);
                UIKit.Size(sep, 12, 10);
                foreach (var d in selectedFamily) Swatch(row, d, ed.Selected == d);
            }
        }

        void PieceButton(Transform parent, ShipBlockDef def, bool selected)
        {
            var ed = sea.editor;
            var rt = UIKit.Rect("Peca", parent);
            var (btn, bg) = UIKit.ButtonBase(rt);
            UIKit.SetSelected(bg, selected);
            btn.onClick.AddListener(() =>
            {
                ed.Selected = ed.Selected == def ? null : def;
                familyChoice[def.Family] = def;
                BuildShipyard();
            });
            UIKit.Size(rt, 64, 70);
            var iconRt = UIKit.Rect("Icone", rt);
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 1f);
            iconRt.pivot = new Vector2(0.5f, 1f);
            iconRt.anchoredPosition = new Vector2(0, -6);
            iconRt.sizeDelta = new Vector2(40, 40);
            var img = iconRt.gameObject.AddComponent<Image>();
            img.sprite = def.Preview;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var cost = UIKit.Text(rt, def.cost.ToString(), 12, TextAnchor.LowerCenter, UIKit.ButtonText);
            UIKit.Stretch(cost.rectTransform);
            cost.rectTransform.offsetMin = new Vector2(0, 8);
            string stats = (def.hp > 0 && def.solid ? $"\nVida do casco: +{def.hp}" : "") + (def.sailPower > 0 ? $"\nVelocidade: +{def.sailPower} vela" : "")
                         + (def.bunks > 0 ? $"\n+{def.bunks} vaga de tripulante" : "") + (def.cannons > 0 ? $"\n+{def.cannons} canhão" : "");
            UIKit.Tip(rt, $"{def.displayName} · {def.cost} ouro\n{def.description}{stats}");
        }

        void Swatch(Transform parent, ShipBlockDef def, bool selected)
        {
            var ed = sea.editor;
            var rt = UIKit.Rect("Cor", parent);
            var frame = rt.gameObject.AddComponent<Image>();
            frame.color = selected ? new Color(0.45f, 0.9f, 0.45f) : new Color(0.08f, 0.06f, 0.06f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            btn.onClick.AddListener(() => { ed.Selected = def; familyChoice[def.Family] = def; BuildShipyard(); });
            UIKit.Size(rt, 34, 34);
            var inner = UIKit.Rect("Amostra", rt);
            UIKit.Stretch(inner, 4);
            var img = inner.gameObject.AddComponent<Image>();
            img.color = def.swatch;
            img.raycastTarget = false;
            UIKit.Tip(rt, def.displayName);
        }

        void ActionButton(string label, UnityEngine.Events.UnityAction onClick, bool interactable)
        {
            buttonsBuilt++;
            var (btn, text, _) = UIKit.Button(actionBar, label, onClick, 16);
            btn.interactable = interactable;
            UIKit.Size(btn, 170, 64);
            UIKit.Fit(text, 11);
            builtButtons.Add(btn);
        }
    }
}
