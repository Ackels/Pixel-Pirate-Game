using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>
    /// Janela da Tripulação (tecla C): cada tripulante num cartão com rosto, ofício, experiência,
    /// trabalho, moral, traços e o dia de hoje (comeu / foi pago). Números de apoio no botão "i".
    /// </summary>
    public class CrewPanel : MonoBehaviour
    {
        RectTransform list;
        ScrollRect scroll;
        Text countText;
        float refreshTimer;
        string lastSignature;

        public static CrewPanel Create(Transform canvas)
        {
            CrewPanel panel = null;
            var (win, content) = UIKit.Window("JanelaTripulacao", canvas, "Tripulação", new Vector2(1010, 500), () => panel.Hide());
            panel = win.gameObject.AddComponent<CrewPanel>();
            UIKit.Vertical(content.gameObject, 0, 8);

            var top = CrewCards.Row(content, 22, 10);
            panel.countText = CrewCards.Label(top.transform, "", 15, UIKit.TextColor);

            // Lista com rolagem: cartões em 3 colunas
            var view = UIKit.Rect("Lista", content);
            view.gameObject.AddComponent<Image>().color = Color.clear;
            view.gameObject.AddComponent<RectMask2D>();
            UIKit.Size(view, flexWidth: 1).flexibleHeight = 1;
            panel.list = UIKit.Rect("Cartoes", view);
            panel.list.anchorMin = new Vector2(0, 1); panel.list.anchorMax = new Vector2(1, 1); panel.list.pivot = new Vector2(0.5f, 1);
            panel.list.sizeDelta = Vector2.zero;
            var grid = panel.list.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(320, 144);
            grid.spacing = new Vector2(10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            panel.list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            panel.scroll = view.gameObject.AddComponent<ScrollRect>();
            panel.scroll.content = panel.list; panel.scroll.viewport = view;
            panel.scroll.horizontal = false; panel.scroll.movementType = ScrollRect.MovementType.Clamped; panel.scroll.scrollSensitivity = 24;

            UIKit.Info(win, Summary);
            win.gameObject.SetActive(false);
            return panel;
        }

        public void Show() { gameObject.SetActive(true); transform.SetAsLastSibling(); lastSignature = null; Rebuild(); }
        public void Hide() => gameObject.SetActive(false);

        void OnEnable()
        {
            if (CrewManager.Instance != null) CrewManager.Instance.Changed += Rebuild;
        }

        void OnDisable()
        {
            if (CrewManager.Instance != null) CrewManager.Instance.Changed -= Rebuild;
        }

        void Update()
        {
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f) { refreshTimer = 1f; Rebuild(); }
        }

        /// <summary>Resumo do que aparece nos cartões: só refaz a lista quando algo mudou (refazer à toa "come" cliques).</summary>
        static string Signature(CrewManager cm)
        {
            var sb = new StringBuilder();
            foreach (var c in cm.Crew)
                sb.Append(c.name).Append(c.level).Append('|').Append(c.xp).Append('|').Append(c.morale)
                  .Append(c.fedToday).Append(c.paidToday).Append(c.atSea).Append(c.onExpedition)
                  .Append(c.workplace != null ? c.workplace.GetHashCode() : 0).Append(c.home != null).Append(';');
            return sb.ToString();
        }

        void Rebuild()
        {
            var cm = CrewManager.Instance;
            if (cm == null || list == null) return;
            string sig = Signature(cm);
            if (sig == lastSignature) return;
            lastSignature = sig;

            float pos = scroll.verticalNormalizedPosition;
            UIKit.Clear(list);
            countText.text = $"{cm.Crew.Count} tripulantes  {UIKit.Colored($"· {cm.FreeBeds} camas livres", UIKit.MutedText)}";
            foreach (var c in cm.Crew) Card(cm, c);
            if (cm.Crew.Count == 0)
                UIKit.Text(list, "Ninguém no bando. Contrate na Taverna!", 16, TextAnchor.MiddleCenter, UIKit.MutedText);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = pos;
        }

        void Card(CrewManager cm, CrewMember c)
        {
            var member = c;
            var card = UIKit.Rect("Tripulante", list);
            card.gameObject.AddComponent<Image>().color = UIKit.CardColor;
            var v = UIKit.Vertical(card.gameObject, 8, 4);
            v.padding = new RectOffset(8, 8, 8, 8);

            // Rosto + nome + ofício + experiência
            var head = CrewCards.Row(card, 56, 8);
            CrewCards.Face(head.transform, c, 56);
            var id = UIKit.Rect("Nome", head.transform);
            UIKit.Vertical(id.gameObject, 0, 2).childAlignment = TextAnchor.MiddleLeft;
            UIKit.Size(id, flexWidth: 1);
            var name = CrewCards.Label(id, c.name, 15, UIKit.GoldColor, height: 18);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIKit.Fit(name, 10);
            var prof = CrewCards.Row(id, 18, 4);
            CrewCards.Icon(prof.transform, CrewCards.ProfessionIcon(c.profession), 16, 16);
            CrewCards.Label(prof.transform, $"{CrewText.Name(c.profession)} · nível {c.level}", 13, UIKit.TextColor);
            prof.gameObject.AddComponent<Image>().color = Color.clear;
            UIKit.Tip(prof, CrewCards.ProfessionHint(c.profession));
            var xp = CrewCards.Row(id, 12, 4);
            CrewCards.Label(xp.transform, "XP", 11, UIKit.MutedText, 22);
            CrewCards.Bar(xp.transform, (float)c.xp / Mathf.Max(1, c.XpToNextLevel), new Color(0.55f, 0.75f, 1f), 120, 6);
            xp.gameObject.AddComponent<Image>().color = Color.clear;
            UIKit.Tip(xp, $"Experiência: {c.xp}/{c.XpToNextLevel} para o nível {c.level + 1}");

            // Onde está / trabalha
            var work = CrewCards.Row(card, 20, 5);
            if (c.onExpedition) CrewCards.Label(work.transform, UIKit.Colored("Em expedição", new Color(0.75f, 0.65f, 1f)), 13, UIKit.TextColor);
            else if (c.atSea) CrewCards.Label(work.transform, UIKit.Colored("No mar", new Color(0.55f, 0.85f, 1f)), 13, UIKit.TextColor);
            else if (c.workplace != null)
            {
                CrewCards.Icon(work.transform, c.workplace.def.icon != null ? c.workplace.def.icon : c.workplace.def.sprite, 18, 18);
                CrewCards.Label(work.transform, c.workplace.def.displayName, 13, UIKit.TextColor);
            }
            else
            {
                CrewCards.Label(work.transform, UIKit.Colored("Sem posto", UIKit.MutedText), 13, UIKit.TextColor);
                work.gameObject.AddComponent<Image>().color = Color.clear;
                UIKit.Tip(work, "Sem posto de trabalho: embarca no navio quando você zarpar.");
            }
            if (c.home == null)
            {
                var warn = UIKit.Rect("SemCasa", work.transform);
                warn.gameObject.AddComponent<Image>().color = CrewCards.Bad;
                UIKit.Horizontal(warn.gameObject, 0, 0).padding = new RectOffset(6, 6, 1, 1);
                var wt = UIKit.Text(warn, "sem casa!", 12, TextAnchor.MiddleCenter, Color.white);
                wt.horizontalOverflow = HorizontalWrapMode.Overflow;
                UIKit.Size(warn, height: 18);
                UIKit.Tip(warn, "Dorme ao relento: a moral cai. Construa mais Casas.");
            }

            CrewCards.MoraleRow(card, c.morale, 110);

            // Traços + hoje (comeu / pago) + dispensar
            var bottom = CrewCards.Row(card, 22, 4);
            foreach (var t in c.traits) CrewCards.TraitChip(bottom.transform, t);
            var gap = UIKit.Rect("Espaco", bottom.transform);
            UIKit.Size(gap, flexWidth: 1);
            Today(bottom.transform, cm.food != null ? cm.food.icon : null, c.fedToday, "Comeu hoje", "Passou fome hoje (-moral)");
            Today(bottom.transform, VillageHUD.Instance != null ? VillageHUD.Instance.goldIcon : null, c.paidToday, "Recebeu o salário hoje", "Ficou sem salário hoje (-moral)");
            var (fire, fl, _) = UIKit.Button(bottom.transform, "Dispensar", () => cm.Fire(member), 11);
            UIKit.Size(fire, 74, 22);
            fl.rectTransform.offsetMin = new Vector2(3, 5); fl.rectTransform.offsetMax = new Vector2(-3, -1);
            UIKit.Tip(fire, $"Dispensar {c.name} do bando.");
        }

        static void Today(Transform parent, Sprite icon, bool ok, string yes, string no)
        {
            var img = CrewCards.Icon(parent, icon, 18, 18, ok ? Color.white : new Color(0.35f, 0.3f, 0.3f, 0.9f));
            img.raycastTarget = true;
            UIKit.Tip(img, ok ? yes : UIKit.Colored(no, CrewCards.Warn));
        }

        static string Summary()
        {
            var cm = CrewManager.Instance;
            if (cm == null) return "";
            int wages = 0;
            foreach (var c in cm.Crew) wages += c.Wage;
            return $"{cm.Crew.Count} tripulantes · salários: {wages} ouro por dia · camas livres: {cm.FreeBeds}\n\n" +
                   "Todo fim de dia cada um come 1 peixe e recebe o salário. Moral baixa = trabalho mais lento. Abaixo de 15, o tripulante abandona o bando.\n\n" +
                   "Passe o mouse nos traços, na moral e nos ícones para ver detalhes.";
        }
    }
}
