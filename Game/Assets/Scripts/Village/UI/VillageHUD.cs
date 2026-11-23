using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>
    /// Interface da vila, montada por código: ouro e estoque (topo), barra de construção (baixo),
    /// dica/erro do modo construção e textos flutuantes ("+15 ouro").
    /// </summary>
    public class VillageHUD : MonoBehaviour
    {
        public static VillageHUD Instance { get; private set; }

        public BuildMode buildMode;
        public Sprite goldIcon;
        public Sprite houseIcon;

        static readonly Color PanelColor = new Color(0.22f, 0.14f, 0.09f, 0.92f);
        static readonly Color ButtonColor = new Color(0.45f, 0.30f, 0.18f, 1f);
        static readonly Color ButtonSelected = new Color(0.85f, 0.62f, 0.25f, 1f);
        static readonly Color TextColor = new Color(0.98f, 0.93f, 0.82f);
        static readonly Color GoldColor = new Color(1f, 0.85f, 0.3f);

        public RectTransform FloatingRoot { get; private set; }
        public Font UIFont { get; private set; }

        Text goldText, housingText, hintText;
        readonly List<(ItemDef item, Text text)> itemTexts = new List<(ItemDef, Text)>();
        readonly List<(BuildingDef def, Button button, Image bg)> buildButtons = new List<(BuildingDef, Button, Image)>();
        Image demolishBg;

        void Awake()
        {
            Instance = this;
            UIFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            EnsureEventSystem();
            var vm = VillageManager.Instance;

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

            FloatingRoot = NewRect("TextosFlutuantes", root);
            Stretch(FloatingRoot);

            // --- Topo esquerdo: recursos ---
            var top = NewPanel("Recursos", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -12), new Vector2(230, 0));
            var topLayout = top.gameObject.AddComponent<VerticalLayoutGroup>();
            topLayout.padding = new RectOffset(10, 10, 8, 8);
            topLayout.spacing = 4;
            topLayout.childControlHeight = topLayout.childControlWidth = true;
            topLayout.childForceExpandHeight = false;
            top.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            goldText = ResourceRow(top, goldIcon, GoldColor);
            foreach (var item in vm.items)
                if (item != null) itemTexts.Add((item, ResourceRow(top, item.icon, item.textColor)));
            housingText = ResourceRow(top, houseIcon, TextColor);

            // --- Topo direito: ajuda ---
            var help = NewPanel("Ajuda", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -12), new Vector2(330, 92));
            var helpText = NewText(help, "WASD / setas: andar\nClique num prédio lá embaixo para construir\nShift: construir vários seguidos\nBotão direito ou Esc: cancelar", 15, TextAnchor.MiddleLeft);
            Stretch(helpText.rectTransform, 10);

            // --- Baixo: barra de construção ---
            var bar = NewPanel("BarraDeConstrucao", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(0, 84));
            var barLayout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            barLayout.padding = new RectOffset(8, 8, 8, 8);
            barLayout.spacing = 8;
            barLayout.childControlWidth = barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = barLayout.childForceExpandHeight = false;
            bar.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (var def in vm.buildable)
            {
                if (def == null) continue;
                var d = def;
                var (btn, bg) = BuildButton(bar, def.icon != null ? def.icon : def.sprite, $"{def.displayName}\n{def.cost} ouro", () =>
                {
                    if (buildMode.Selected == d) buildMode.Cancel(); else buildMode.Select(d);
                });
                buildButtons.Add((def, btn, bg));
            }
            var (_, dbg) = BuildButton(bar, null, "Demolir", () =>
            {
                if (buildMode.Demolishing) buildMode.Cancel(); else buildMode.StartDemolish();
            });
            demolishBg = dbg;

            // --- Dica do modo construção ---
            var hintPanel = NewPanel("Dica", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 104), new Vector2(760, 34));
            hintText = NewText(hintPanel, "", 16, TextAnchor.MiddleCenter);
            Stretch(hintText.rectTransform, 6);

            vm.Changed += Refresh;
            buildMode.SelectionChanged += Refresh;
            Refresh();
        }

        void Update()
        {
            if (hintText == null) return;
            string hint = buildMode.Hint;
            hintText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(hint));
            hintText.text = hint;
        }

        void Refresh()
        {
            var vm = VillageManager.Instance;
            if (vm == null || goldText == null) return;
            goldText.text = $"Ouro: {vm.Gold}";
            foreach (var (item, text) in itemTexts) text.text = $"{item.displayName}: {vm.GetStock(item)}";
            housingText.text = $"Moradia: {vm.HousingCapacity} vagas";
            foreach (var (def, btn, bg) in buildButtons)
            {
                bool affordable = vm.Gold >= def.cost;
                bg.color = buildMode.Selected == def ? ButtonSelected : ButtonColor;
                btn.interactable = affordable || buildMode.Selected == def;
            }
            demolishBg.color = buildMode.Demolishing ? ButtonSelected : ButtonColor;
        }

        // ---------- Peças de interface ----------
        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        RectTransform NewPanel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = PanelColor;
            return rt;
        }

        Text NewText(Transform parent, string content, int size, TextAnchor align)
        {
            var rt = NewRect("Texto", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UIFont;
            t.fontSize = size;
            t.color = TextColor;
            t.alignment = align;
            t.text = content;
            t.raycastTarget = false;
            return t;
        }

        Text ResourceRow(Transform parent, Sprite icon, Color color)
        {
            var row = NewRect("Linha", parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 26;

            var iconRt = NewRect("Icone", row);
            var img = iconRt.gameObject.AddComponent<Image>();
            img.sprite = icon;
            img.enabled = icon != null;
            img.preserveAspect = true;
            var ile = iconRt.gameObject.AddComponent<LayoutElement>();
            ile.preferredWidth = ile.preferredHeight = 24;

            var t = NewText(row, "", 18, TextAnchor.MiddleLeft);
            t.color = color;
            t.gameObject.AddComponent<LayoutElement>().preferredWidth = 170;
            return t;
        }

        (Button, Image) BuildButton(Transform parent, Sprite icon, string label, UnityEngine.Events.UnityAction onClick)
        {
            var rt = NewRect("Botao", parent);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = ButtonColor;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = icon != null ? 150 : 96;
            le.preferredHeight = 68;

            var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 4, 4);
            layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleCenter;

            if (icon != null)
            {
                var iconRt = NewRect("Icone", rt);
                var img = iconRt.gameObject.AddComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
                var ile = iconRt.gameObject.AddComponent<LayoutElement>();
                ile.preferredWidth = ile.preferredHeight = 48;
            }
            var t = NewText(rt, label, 15, TextAnchor.MiddleCenter);
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            return (btn, bg);
        }
    }
}
