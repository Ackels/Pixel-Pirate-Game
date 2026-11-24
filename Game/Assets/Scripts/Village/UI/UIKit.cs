using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>Peças de interface montadas por código (painéis, textos, botões) com o tema de madeira.</summary>
    public static class UIKit
    {
        public static readonly Color PanelColor = new Color(0.22f, 0.14f, 0.09f, 0.94f);
        /// <summary>Cartão dentro de uma janela: sombra escura por cima das tábuas.</summary>
        public static readonly Color CardColor = new Color(0.12f, 0.07f, 0.05f, 0.5f);
        /// <summary>Texto escrito em cima dos botões claros.</summary>
        public static readonly Color ButtonText = new Color(0.24f, 0.14f, 0.11f);
        public static readonly Color ButtonMuted = new Color(0.45f, 0.32f, 0.26f);
        public static readonly Color ButtonGood = new Color(0.16f, 0.42f, 0.18f);
        public static readonly Color TextColor = new Color(0.98f, 0.93f, 0.82f);
        public static readonly Color MutedText = new Color(0.8f, 0.72f, 0.6f);
        public static readonly Color GoldColor = new Color(1f, 0.85f, 0.3f);

        static Font font;
        /// <summary>Fonte pixelada Pixelify Sans Bold (Resources/Fonts, licença OFL ao lado); se faltar, a padrão do Unity.</summary>
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.Load<Font>("Fonts/PixelifySans-Bold");
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        /// <summary>Ajuste geral do tamanho das letras (a Pixelify Sans pede letras um pouco maiores para ler bem).</summary>
        public const float FontScale = 1.1f;
        public static int Scaled(int size) => Mathf.Max(8, Mathf.RoundToInt(size * FontScale));

        /// <summary>Se o texto não couber, ele encolhe (até "min") em vez de vazar.</summary>
        public static void Fit(Text t, int min = 8)
        {
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Min(min, t.fontSize);
            t.resizeTextMaxSize = t.fontSize;
            t.verticalOverflow = VerticalWrapMode.Truncate;
        }

        /// <summary>Peças de madeira e papel (Resources/UI, geradas por tools/arte/ui_export.py).</summary>
        public static class Skin
        {
            static Sprite frame, planks, button, buttonOn, paper;
            static Sprite Load(ref Sprite s, string name) { if (s == null) s = Resources.Load<Sprite>("UI/" + name); return s; }
            public static Sprite Frame => Load(ref frame, "moldura");
            public static Sprite Planks => Load(ref planks, "tabuas");
            public static Sprite Button => Load(ref button, "botao");
            public static Sprite ButtonOn => Load(ref buttonOn, "botao_verde");
            public static Sprite Paper => Load(ref paper, "papel");
            public static Sprite Icon(string name) => Resources.Load<Sprite>("UI/icone_" + name);
        }

        /// <summary>1 pixel da arte da interface = 2 pontos da tela (na resolução de referência 1280x720).</summary>
        public const float ArtPixel = 2f;
        public static void UseSkin(Image img, Sprite sprite, Image.Type type)
        {
            if (sprite == null) return;
            img.sprite = sprite;
            img.type = type;
            img.color = Color.white;
            img.pixelsPerUnitMultiplier = 100f / sprite.pixelsPerUnit / ArtPixel;
        }

        const string FrameName = "Moldura";
        /// <summary>Moldura de madeira em volta do retângulo (um pouco para fora, sem entrar no layout).</summary>
        public static void AddFrame(RectTransform rt)
        {
            if (Skin.Frame == null) return;
            var f = Rect(FrameName, rt);
            f.SetAsFirstSibling();
            Stretch(f, -4);
            var img = f.gameObject.AddComponent<Image>();
            UseSkin(img, Skin.Frame, Image.Type.Sliced);
            img.fillCenter = false;
            img.raycastTarget = false;
            f.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        /// <summary>Botão claro (normal) ou verde (selecionado / aberto).</summary>
        public static void SetSelected(Image bg, bool on)
        {
            var s = on ? Skin.ButtonOn : Skin.Button;
            if (s != null) { UseSkin(bg, s, Image.Type.Sliced); }
            else bg.color = on ? new Color(0.85f, 0.62f, 0.25f, 1f) : new Color(0.45f, 0.30f, 0.18f, 1f);
        }

        /// <summary>Texto que aparece ao passar o mouse.</summary>
        public static Tooltip Tip(Component target, System.Func<string> text)
        {
            var t = target.GetComponent<Tooltip>();
            if (t == null) t = target.gameObject.AddComponent<Tooltip>();
            t.text = text;
            return t;
        }
        public static Tooltip Tip(Component target, string text) => Tip(target, () => text);

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
            var img = rt.gameObject.AddComponent<Image>();
            // Sem cor pedida: tábuas de madeira. Com cor: fundo liso daquela cor (avisos, selos), mas com a moldura.
            if (color == null && Skin.Planks != null) UseSkin(img, Skin.Planks, Image.Type.Tiled);
            else img.color = color ?? PanelColor;
            AddFrame(rt);
            return rt;
        }

        public static Text Text(Transform parent, string content, int size, TextAnchor align, Color? color = null)
        {
            var rt = Rect("Texto", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = Scaled(size);
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
            var (btn, bg) = ButtonBase(rt);
            if (onClick != null) btn.onClick.AddListener(onClick);
            var t = Text(rt, label, fontSize, TextAnchor.MiddleCenter, Skin.Button != null ? ButtonText : TextColor);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = new Vector2(6, 8);    // a base do botão tem a "sombra" da madeira
            t.rectTransform.offsetMax = new Vector2(-6, -4);
            Fit(t);
            return (btn, t, bg);
        }

        /// <summary>Fundo e comportamento de um botão de madeira (sem conteúdo).</summary>
        public static (Button, Image) ButtonBase(RectTransform rt)
        {
            var bg = rt.gameObject.AddComponent<Image>();
            SetSelected(bg, false);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.9f, 0.75f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.78f, 0.7f, 0.62f);
            colors.disabledColor = new Color(0.55f, 0.52f, 0.5f, 0.75f);
            btn.colors = colors;
            return (btn, bg);
        }

        /// <summary>Janela com título e botão de fechar. Retorna o painel e a área de conteúdo.</summary>
        public static (RectTransform window, RectTransform content) Window(string name, Transform parent, string title, Vector2 size, UnityAction onClose)
        {
            var win = Panel(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), size);
            var header = Rect("Cabecalho", win);
            header.anchorMin = new Vector2(0, 1);
            header.anchorMax = new Vector2(1, 1);
            header.pivot = new Vector2(0.5f, 1);
            header.sizeDelta = new Vector2(0, 40);
            header.anchoredPosition = Vector2.zero;
            header.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.3f);   // faixa do título
            var titleText = Text(header, title, 22, TextAnchor.MiddleLeft, GoldColor);
            Stretch(titleText.rectTransform, 12);
            titleText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);
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

        /// <summary>
        /// Botão "i" no cabeçalho da janela: explicações e números de apoio ficam guardados ali
        /// (aparecem ao passar o mouse; clicar deixa o quadro aberto).
        /// </summary>
        public static void Info(RectTransform window, System.Func<string> text)
        {
            var header = window.Find("Cabecalho");
            if (header == null) return;
            var rt = Rect("Info", header);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 0.5f);
            rt.sizeDelta = new Vector2(32, 28);
            rt.anchoredPosition = new Vector2(-46, 0);
            var (btn, bg) = ButtonBase(rt);
            var t = Text(rt, "i", 18, TextAnchor.MiddleCenter, Skin.Button != null ? ButtonText : TextColor);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = new Vector2(0, 4); t.rectTransform.offsetMax = Vector2.zero;

            var box = Panel("QuadroInfo", window, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, -46), new Vector2(380, 0));
            var v = Vertical(box.gameObject, 14, 0);
            v.padding = new RectOffset(14, 14, 12, 12);
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var label = Text(box, "", 14, TextAnchor.UpperLeft);
            box.gameObject.SetActive(false);

            var info = rt.gameObject.AddComponent<InfoButton>();
            info.Setup(box, label, bg, text);
            btn.onClick.AddListener(info.Toggle);
            Tip(rt, () => box.gameObject.activeSelf ? null : text());
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                if (t.GetChild(i).name != FrameName) Object.Destroy(t.GetChild(i).gameObject);   // a moldura fica
        }
    }
}
