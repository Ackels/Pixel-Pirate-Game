using UnityEngine;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>Peças visuais dos cartões de tripulante (Taverna, Tripulação): rosto, ícone do ofício, traços, barras.</summary>
    public static class CrewCards
    {
        public static readonly Color Good = new Color(0.35f, 0.62f, 0.3f);
        public static readonly Color Bad = new Color(0.72f, 0.3f, 0.25f);
        public static readonly Color Warn = new Color(1f, 0.5f, 0.45f);

        public static Sprite Portrait(CrewMember m)
        {
            var cm = CrewManager.Instance;
            var look = cm != null ? cm.LookOf(m) : null;
            if (look == null) return VillageHUD.Instance != null ? VillageHUD.Instance.crewIcon : null;
            return look.portrait != null ? look.portrait : look.down[0];
        }

        /// <summary>Marujo = âncora; os outros, o produto do ofício (cana, peixe, rum).</summary>
        public static Sprite ProfessionIcon(Profession p)
        {
            if (p == Profession.Marujo) return UIKit.Skin.Icon("ancora");
            string item = p switch { Profession.Agricultor => "Cana", Profession.Pescador => "Peixe", Profession.Destilador => "Rum", _ => null };
            var vm = VillageManager.Instance;
            if (vm == null || item == null) return null;
            foreach (var it in vm.items) if (it != null && it.displayName == item) return it.icon;
            return null;
        }

        public static string ProfessionHint(Profession p) => p switch
        {
            Profession.Marujo => "Marujo: luta melhor no mar e nas expedições.",
            Profession.Agricultor => "Agricultor: rende mais na Horta.",
            Profession.Pescador => "Pescador: rende mais na Cabana de Pesca.",
            Profession.Destilador => "Destilador: rende mais na Destilaria.",
            _ => "",
        };

        public static Image Icon(Transform parent, Sprite s, float w, float h, Color? tint = null)
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

        /// <summary>Rosto num quadradinho escuro.</summary>
        public static RectTransform Face(Transform parent, CrewMember m, float size)
        {
            var box = UIKit.Rect("Rosto", parent);
            box.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.3f);
            UIKit.Size(box, size, size);
            var img = UIKit.Rect("Imagem", box);
            UIKit.Stretch(img, 3);
            var i = img.gameObject.AddComponent<Image>();
            i.sprite = Portrait(m);
            i.preserveAspect = true;
            i.raycastTarget = false;
            return box;
        }

        public static HorizontalLayoutGroup Row(Transform parent, float height, int spacing = 5)
        {
            var r = UIKit.Rect("Linha", parent);
            var h = UIKit.Horizontal(r.gameObject, 0, spacing);
            h.childAlignment = TextAnchor.MiddleLeft;
            UIKit.Size(r, height: height);
            return h;
        }

        public static Text Label(Transform parent, string s, int size, Color color, float width = -1, float height = -1)
        {
            var t = UIKit.Text(parent, s, size, TextAnchor.MiddleLeft, color);
            t.horizontalOverflow = width < 0 ? HorizontalWrapMode.Overflow : HorizontalWrapMode.Wrap;
            UIKit.Size(t, width, height);
            return t;
        }

        /// <summary>Etiqueta colorida (verde = traço bom, vermelho = ruim); a explicação fica no mouse.</summary>
        public static void TraitChip(Transform parent, Trait t)
        {
            bool good = CrewText.IsGood(t);
            var chip = UIKit.Rect("Traco", parent);
            chip.gameObject.AddComponent<Image>().color = good ? Good : Bad;
            var h = UIKit.Horizontal(chip.gameObject, 0, 0);
            h.padding = new RectOffset(6, 6, 1, 1);
            h.childAlignment = TextAnchor.MiddleCenter;
            var txt = UIKit.Text(chip, CrewText.Name(t), 12, TextAnchor.MiddleCenter, Color.white);
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIKit.Size(chip, height: 18);
            UIKit.Tip(chip, $"{CrewText.Name(t)} ({(good ? "bom" : "ruim")})\n{CrewText.Describe(t)}");
        }

        /// <summary>Barra fina colorida (moral, experiência).</summary>
        public static Image Bar(Transform parent, float value, Color color, float width, float height = 8)
        {
            var bg = UIKit.Rect("Barra", parent);
            bg.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);
            UIKit.Size(bg, width, height);
            var fill = UIKit.Rect("Preenchimento", bg);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var img = fill.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Linha "Moral [barra] Animado".</summary>
        public static void MoraleRow(Transform parent, int morale, float barWidth)
        {
            var row = Row(parent, 16, 6);
            Label(row.transform, "Moral", 12, UIKit.MutedText, 44);
            Bar(row.transform, morale / 100f, CrewText.MoraleColor(morale), barWidth);
            Label(row.transform, CrewText.MoraleLabel(morale), 12, CrewText.MoraleColor(morale));
            row.gameObject.AddComponent<Image>().color = Color.clear;
            UIKit.Tip(row, $"Moral {morale}/100\nMoral baixa deixa o trabalho mais lento. Abaixo de 15 o tripulante abandona o bando.");
        }
    }
}
