using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>
    /// Interface da vila, montada por código: dia, ouro e estoque (topo), barra de construção (baixo),
    /// registro de eventos, janelas (Taverna, Tripulação, info do prédio) e textos flutuantes.
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

        Text dayText, goldText, crewText, hintText;
        Image dayBar;
        readonly List<(ItemDef item, Text text)> itemTexts = new List<(ItemDef, Text)>();
        readonly List<(BuildingDef def, Button button, Image bg)> buildButtons = new List<(BuildingDef, Button, Image)>();
        Image demolishBg;
        BuildingInfoPanel infoPanel;
        TavernPanel tavernPanel;
        CrewPanel crewPanel;
        GameMenu menu;
        ExpeditionPanel expeditionPanel;
        Camera cam;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

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

            // --- Topo esquerdo: dia + recursos ---
            var top = UIKit.Panel("Recursos", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -12), new Vector2(280, 0));
            UIKit.Vertical(top.gameObject, 10, 4);
            top.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            dayText = UIKit.Text(top, "", 19, TextAnchor.MiddleLeft, UIKit.GoldColor);
            UIKit.Size(dayText, height: 24);
            var barBg = UIKit.Rect("BarraDoDia", top);
            barBg.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);
            UIKit.Size(barBg, height: 6);
            var bar = UIKit.Rect("Preenchimento", barBg);
            UIKit.Stretch(bar);
            dayBar = bar.gameObject.AddComponent<Image>();
            dayBar.color = UIKit.GoldColor;

            goldText = ResourceRow(top, goldIcon, UIKit.GoldColor);
            foreach (var item in vm.items)
                if (item != null) itemTexts.Add((item, ResourceRow(top, item.icon, item.textColor)));
            crewText = ResourceRow(top, crewIcon, UIKit.TextColor);

            // --- Topo direito: ajuda ---
            var help = UIKit.Panel("Ajuda", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -12), new Vector2(340, 110));
            var helpText = UIKit.Text(help,
                "WASD / setas: andar\nBarra de baixo: construir (Shift = vários)\nClique num prédio: ver detalhes\nC: tripulação · X: expedições · Esc: menu", 15, TextAnchor.MiddleLeft);
            UIKit.Stretch(helpText.rectTransform, 10);

            // --- Baixo: barra de construção ---
            var barPanel = UIKit.Panel("BarraDeConstrucao", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(0, 84));
            var barLayout = UIKit.Horizontal(barPanel.gameObject, 8, 8);
            barLayout.childAlignment = TextAnchor.MiddleCenter;
            barPanel.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (var def in vm.buildable)
            {
                if (def == null) continue;
                var d = def;
                var (btn, bg) = IconButton(barPanel, def.icon != null ? def.icon : def.sprite, $"{def.displayName}\n{def.cost} ouro", () =>
                {
                    CloseWindows();
                    if (buildMode.Selected == d) buildMode.Cancel(); else buildMode.Select(d);
                });
                buildButtons.Add((def, btn, bg));
            }
            var (_, dbg) = IconButton(barPanel, null, "Demolir", () =>
            {
                CloseWindows();
                if (buildMode.Demolishing) buildMode.Cancel(); else buildMode.StartDemolish();
            });
            demolishBg = dbg;
            IconButton(barPanel, crewIcon, "Tripulação\n[C]", ToggleCrew);

            // --- Dica do modo construção ---
            var hintPanel = UIKit.Panel("Dica", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 104), new Vector2(760, 34));
            hintText = UIKit.Text(hintPanel, "", 16, TextAnchor.MiddleCenter);
            UIKit.Stretch(hintText.rectTransform, 6);

            // --- Registro e janelas ---
            EventLog.Create(root);
            tavernPanel = TavernPanel.Create(root);
            crewPanel = CrewPanel.Create(root);
            expeditionPanel = ExpeditionPanel.Create(root);
            ExpeditionStrip.Create(root);
            infoPanel = BuildingInfoPanel.Create(root, () => { infoPanel.Hide(); tavernPanel.Show(); }, () => { infoPanel.Hide(); expeditionPanel.Show(); });
            menu = GameMenu.Create(root);
            menu.OpenStart();

            vm.Changed += Refresh;
            buildMode.SelectionChanged += Refresh;
            if (cm != null) cm.Changed += Refresh;
            Refresh();
        }

        void Update()
        {
            if (hintText == null) return;
            toastTimer -= Time.unscaledDeltaTime;
            string hint = !string.IsNullOrEmpty(buildMode.Hint) ? buildMode.Hint
                : toastTimer > 0 ? toast : Prompt;
            hintText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(hint));
            hintText.text = hint;

            var cm = CrewManager.Instance;
            if (cm != null)
            {
                dayText.text = $"Dia {cm.Day}";
                ((RectTransform)dayBar.transform).anchorMax = new Vector2(cm.DayProgress, 1);
            }

            HandleInput();
        }

        void HandleInput()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (buildMode.Active || buildMode.LastActionFrame == Time.frameCount || GameMenu.IsOpen) return;

            if (kb != null && kb.cKey.wasPressedThisFrame) ToggleCrew();
            if (kb != null && kb.xKey.wasPressedThisFrame) ToggleExpeditions();
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                bool anyOpen = tavernPanel.gameObject.activeSelf || crewPanel.gameObject.activeSelf || infoPanel.gameObject.activeSelf || expeditionPanel.IsOpen;
                if (anyOpen) CloseWindows();
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

        void ToggleCrew()
        {
            bool open = crewPanel.gameObject.activeSelf;
            CloseWindows();
            if (!open) crewPanel.Show();
        }

        void ToggleExpeditions()
        {
            bool open = expeditionPanel.IsOpen;
            CloseWindows();
            if (open) return;
            if (ExpeditionManager.Instance == null || !ExpeditionManager.Instance.HasBoard) { Toast("Construa o Quadro de Expedições para mandar grupos às masmorras."); return; }
            expeditionPanel.Show();
        }

        void CloseWindows()
        {
            expeditionPanel.Hide();
            tavernPanel.Hide();
            crewPanel.Hide();
            infoPanel.Hide();
        }

        void Refresh()
        {
            var vm = VillageManager.Instance;
            if (vm == null || goldText == null) return;
            var cm = CrewManager.Instance;
            goldText.text = $"Ouro: {vm.Gold}";
            foreach (var (item, text) in itemTexts) text.text = $"{item.displayName}: {vm.GetStock(item)}";
            if (cm != null) crewText.text = $"Tripulação: {cm.Crew.Count} · camas: {vm.HousingCapacity}";
            foreach (var (def, btn, bg) in buildButtons)
            {
                bg.color = buildMode.Selected == def ? UIKit.ButtonSelected : UIKit.ButtonColor;
                btn.interactable = vm.Gold >= def.cost || buildMode.Selected == def;
            }
            demolishBg.color = buildMode.Demolishing ? UIKit.ButtonSelected : UIKit.ButtonColor;
        }

        Text ResourceRow(Transform parent, Sprite icon, Color color)
        {
            var row = UIKit.Rect("Linha", parent);
            UIKit.Horizontal(row.gameObject, 0, 6);
            UIKit.Size(row, height: 26);
            var iconRt = UIKit.Rect("Icone", row);
            var img = iconRt.gameObject.AddComponent<Image>();
            img.sprite = icon;
            img.enabled = icon != null;
            img.preserveAspect = true;
            UIKit.Size(iconRt, 24, 24);
            var t = UIKit.Text(row, "", 18, TextAnchor.MiddleLeft, color);
            UIKit.Size(t, 230, 26);
            return t;
        }

        (Button, Image) IconButton(Transform parent, Sprite icon, string label, UnityEngine.Events.UnityAction onClick)
        {
            var rt = UIKit.Rect("Botao", parent);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = UIKit.ButtonColor;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);
            UIKit.Size(rt, icon != null ? 124 : 84, 68);

            var layout = UIKit.Horizontal(rt.gameObject, 5, 5);
            layout.childAlignment = TextAnchor.MiddleCenter;
            if (icon != null)
            {
                var iconRt = UIKit.Rect("Icone", rt);
                var img = iconRt.gameObject.AddComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
                UIKit.Size(iconRt, 38, 38);
            }
            var t = UIKit.Text(rt, label, 15, TextAnchor.MiddleCenter);
            UIKit.Size(t, flexWidth: 1);
            return (btn, bg);
        }
    }
}
