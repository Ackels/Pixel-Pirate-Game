using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>Peças de interface montadas por código (painéis, textos, botões) com o tema de madeira.</summary>
    public static class UIKit
    {
        public static readonly Color PanelColor = new Color(0.22f, 0.14f, 0.09f, 0.94f);
        public static readonly Color CardColor = new Color(0.32f, 0.21f, 0.13f, 1f);
        public static readonly Color ButtonColor = new Color(0.45f, 0.30f, 0.18f, 1f);
        public static readonly Color ButtonSelected = new Color(0.85f, 0.62f, 0.25f, 1f);
        public static readonly Color TextColor = new Color(0.98f, 0.93f, 0.82f);
        public static readonly Color MutedText = new Color(0.8f, 0.72f, 0.6f);
        public static readonly Color GoldColor = new Color(1f, 0.85f, 0.3f);

        static Font font;
        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
        public static string Colored(string s, Color c) => $"<color={Hex(c)}>{s}</color>";

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color? color = null)
        {
            var rt = Rect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            rt.gameObject.AddComponent<Image>().color = color ?? PanelColor;
            return rt;
        }

        public static Text Text(Transform parent, string content, int size, TextAnchor align, Color? color = null)
        {
            var rt = Rect("Texto", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.color = color ?? TextColor;
            t.alignment = align;
            t.text = content;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static VerticalLayoutGroup Vertical(GameObject go, int padding, int spacing)
        {
            var l = go.AddComponent<VerticalLayoutGroup>();
            l.padding = new RectOffset(padding, padding, padding, padding);
            l.spacing = spacing;
            l.childControlWidth = l.childControlHeight = true;
            l.childForceExpandWidth = true;
            l.childForceExpandHeight = false;
            return l;
        }

        public static HorizontalLayoutGroup Horizontal(GameObject go, int padding, int spacing)
        {
            var l = go.AddComponent<HorizontalLayoutGroup>();
            l.padding = new RectOffset(padding, padding, padding, padding);
            l.spacing = spacing;
            l.childControlWidth = l.childControlHeight = true;
            l.childForceExpandWidth = false;
            l.childForceExpandHeight = false;
            l.childAlignment = TextAnchor.MiddleLeft;
            return l;
        }

        public static LayoutElement Size(Component c, float width = -1, float height = -1, float flexWidth = -1)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0) le.preferredWidth = width;
            if (height >= 0) le.preferredHeight = height;
            if (flexWidth >= 0) le.flexibleWidth = flexWidth;
            return le;
        }

        /// <summary>Botão de texto simples. Retorna o botão e o texto (para mudar o rótulo depois).</summary>
        public static (Button button, Text label, Image bg) Button(Transform parent, string label, UnityAction onClick, int fontSize = 15)
        {
            var rt = Rect("Botao", parent);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = ButtonColor;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);
            var t = Text(rt, label, fontSize, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform, 4);
            return (btn, t, bg);
        }

        /// <summary>Janela com título e botão de fechar. Retorna o painel e a área de conteúdo.</summary>
        public static (RectTransform window, RectTransform content) Window(string name, Transform parent, string title, Vector2 size, UnityAction onClose)
        {
            var win = Panel(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), size,
                new Color(PanelColor.r, PanelColor.g, PanelColor.b, 1f));
            var header = Rect("Cabecalho", win);
            header.anchorMin = new Vector2(0, 1);
            header.anchorMax = new Vector2(1, 1);
            header.pivot = new Vector2(0.5f, 1);
            header.sizeDelta = new Vector2(0, 40);
            header.anchoredPosition = Vector2.zero;
            var titleText = Text(header, title, 22, TextAnchor.MiddleLeft, GoldColor);
            Stretch(titleText.rectTransform, 12);
            var (close, _, _) = Button(header, "X", onClose, 18);
            var crt = (RectTransform)close.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1, 0.5f);
            crt.pivot = new Vector2(1, 0.5f);
            crt.sizeDelta = new Vector2(32, 28);
            crt.anchoredPosition = new Vector2(-8, 0);

            var content = Rect("Conteudo", win);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(12, 12);
            content.offsetMax = new Vector2(-12, -46);
            return (win, content);
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}
