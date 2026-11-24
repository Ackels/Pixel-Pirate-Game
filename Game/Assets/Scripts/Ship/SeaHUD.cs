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
        RectTransform actionBar, helpPanel;
        SeaMode builtFor = (SeaMode)(-1);
        EncounterState builtEncounter = (EncounterState)(-1);
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

            var hintPanel = UIKit.Panel("Dica", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(760, 36));
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
            if (builtFor != sea.Mode || builtEncounter != est) Refresh();

            enemyText.transform.parent.gameObject.SetActive(est != EncounterState.None);
            if (director != null && est != EncounterState.None)
            {
                string crewInfo = est == EncounterState.Approaching ? "" : $" · Tripulação: {director.EnemyCrewAlive}";
                enemyText.text = UIKit.Colored(director.EnemyName, UIKit.GoldColor) + $"\nCasco: {director.EnemyHp}/{director.EnemyMaxHp}{crewInfo}";
            }

            var kb = Keyboard.current;
            if (sea.Mode == SeaMode.Editing && kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (sea.editor.Selected != null) { sea.editor.Selected = null; Refresh(); }
                else sea.StopEditing();
            }
            else if (kb != null && kb.escapeKey.wasPressedThisFrame && helpPanel.gameObject.activeSelf) helpPanel.gameObject.SetActive(false);

            messageTimer -= Time.unscaledDeltaTime;
            string hint = messageTimer > 0 ? message
                : sea.Mode == SeaMode.Editing ? sea.editor.Hint
                : sea.NearbyInteractable != null ? sea.NearbyInteractable.prompt : null;
            hintText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(hint));
            hintText.text = hint;

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
                    helpText.text = "Clique: colocar · Botão direito: remover\nEsc: soltar bloco / sair";
                    foreach (var def in sea.Palette)
                    {
                        var d = def;
                        bool selected = sea.editor.Selected == d;
                        PaletteButton(d, selected, () =>
                        {
                            sea.editor.Selected = sea.editor.Selected == d ? null : d;
                            Refresh();
                        });
                    }
                    ActionButton("Concluir", sea.StopEditing, true);
                    break;

                case SeaMode.Sailing:
                    bool fighting = builtEncounter != EncounterState.None;
                    titleText.text = fighting ? "Combate!" : sea.Destination != null ? $"Rumo a {sea.Destination.name}" : "Navegando";
                    helpText.text = "A/D: andar · Espaço: pular\nF / J / clique: golpear com a espada";
                    if (director != null && director.CanBoard) ActionButton("Abordar!", director.Board, true);
                    if (director != null && director.CanFlee) ActionButton("Fugir", director.Flee, true);
                    if (!fighting) ActionButton("Ver mapa", map.Show, true);
                    break;

                case SeaMode.Landed:
                    titleText.text = sea.Location != null ? sea.Location.name : "Em terra";
                    helpText.text = "A/D: andar · Espaço: pular · F/J: golpear\nE: abrir baú / negociar";
                    ActionButton("Zarpar\n(abrir mapa)", sea.OpenMap, true);
                    break;
            }
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

        void ActionButton(string label, UnityEngine.Events.UnityAction onClick, bool interactable)
        {
            var (btn, _, _) = UIKit.Button(actionBar, label, onClick, 16);
            btn.interactable = interactable;
            UIKit.Size(btn, 170, 64);
        }

        void PaletteButton(ShipBlockDef def, bool selected, UnityEngine.Events.UnityAction onClick)
        {
            var rt = UIKit.Rect("Bloco", actionBar);
            var (btn, bg) = UIKit.ButtonBase(rt);
            UIKit.SetSelected(bg, selected);
            btn.onClick.AddListener(onClick);
            UIKit.Size(rt, 118, 64);
            var l = UIKit.Horizontal(rt.gameObject, 5, 4);
            l.childAlignment = TextAnchor.MiddleCenter;
            var iconRt = UIKit.Rect("Icone", rt);
            var img = iconRt.gameObject.AddComponent<Image>();
            img.sprite = def.sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            UIKit.Size(iconRt, 36, 36);
            var t = UIKit.Text(rt, $"{def.displayName}\n{def.cost} ouro", 14, TextAnchor.MiddleCenter, UIKit.ButtonText);
            UIKit.Size(t, flexWidth: 1);
        }
    }
}
