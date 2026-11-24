using UnityEngine;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>
    /// Janela da Taverna: os recrutas do dia em cartões (rosto, ofício, traços como etiquetas, força,
    /// moral, salário) e o botão Contratar. Explicações no botão "i" e nas dicas do mouse.
    /// </summary>
    public class TavernPanel : MonoBehaviour
    {
        RectTransform cards;
        Text bedsText;

        public static TavernPanel Create(Transform canvas)
        {
            var panel = (TavernPanel)null;
            var (win, content) = UIKit.Window("JanelaTaverna", canvas, "Taverna", new Vector2(960, 360), () => panel.Hide());
            panel = win.gameObject.AddComponent<TavernPanel>();
            UIKit.Vertical(content.gameObject, 0, 8);

            // Linha de cima: título curto e camas livres (o que limita contratar)
            var top = CrewCards.Row(content, 24, 10);
            var sub = CrewCards.Label(top.transform, "Recrutas de hoje", 17, UIKit.GoldColor);
            sub.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
            var gap = UIKit.Rect("Espaco", top.transform);
            UIKit.Size(gap, flexWidth: 1);
            panel.bedsText = CrewCards.Label(top.transform, "", 14, UIKit.TextColor);

            panel.cards = UIKit.Rect("Cartas", content);
            var h = UIKit.Horizontal(panel.cards.gameObject, 0, 12);
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            UIKit.Size(panel.cards, height: 250, flexWidth: 1);

            UIKit.Info(win, () =>
                "Cada tripulante precisa de uma cama (Casa), 1 peixe e o salário todo dia. Rum deixa todo mundo mais feliz.\n\n" +
                "Traços verdes ajudam e vermelhos atrapalham: passe o mouse para ver o que cada um faz.\n" +
                "Novos recrutas chegam a cada dia.");
            win.gameObject.SetActive(false);
            return panel;
        }

        void OnEnable()
        {
            if (CrewManager.Instance != null) CrewManager.Instance.Changed += Rebuild;
            if (VillageManager.Instance != null) VillageManager.Instance.Changed += Rebuild;
            Rebuild();
        }

        void OnDisable()
        {
            if (CrewManager.Instance != null) CrewManager.Instance.Changed -= Rebuild;
            if (VillageManager.Instance != null) VillageManager.Instance.Changed -= Rebuild;
        }

        public void Show() { gameObject.SetActive(true); transform.SetAsLastSibling(); }
        public void Hide() => gameObject.SetActive(false);

        void Rebuild()
        {
            var cm = CrewManager.Instance;
            if (cm == null || cards == null) return;
            UIKit.Clear(cards);

            int beds = cm.FreeBeds;
            bedsText.text = beds > 0 ? $"Camas livres: {beds}" : UIKit.Colored("Sem cama livre: construa uma Casa", CrewCards.Warn);

            if (cm.Recruits.Count == 0)
            {
                var empty = UIKit.Text(cards, "Ninguém na taverna hoje. Volte amanhã!", 18, TextAnchor.MiddleCenter, UIKit.MutedText);
                UIKit.Size(empty, flexWidth: 1);
            }
            foreach (var r in cm.Recruits) Card(cm, r);
        }

        void Card(CrewManager cm, CrewMember r)
        {
            var card = UIKit.Rect("Carta", cards);
            card.gameObject.AddComponent<Image>().color = UIKit.CardColor;
            var v = UIKit.Vertical(card.gameObject, 12, 8);
            v.padding = new RectOffset(12, 12, 12, 12);

            // Rosto + nome + ofício
            var head = CrewCards.Row(card, 72, 10);
            CrewCards.Face(head.transform, r, 72);
            var id = UIKit.Rect("Nome", head.transform);
            UIKit.Vertical(id.gameObject, 0, 4).childAlignment = TextAnchor.MiddleLeft;
            UIKit.Size(id, flexWidth: 1);
            var name = CrewCards.Label(id, r.name, 17, UIKit.GoldColor, height: 22);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIKit.Fit(name, 11);
            var prof = CrewCards.Row(id, 22, 5);
            CrewCards.Icon(prof.transform, CrewCards.ProfessionIcon(r.profession), 20, 20);
            CrewCards.Label(prof.transform, $"{CrewText.Name(r.profession)} · nível {r.level}", 14, UIKit.TextColor);
            prof.gameObject.AddComponent<Image>().color = Color.clear;
            UIKit.Tip(prof, CrewCards.ProfessionHint(r.profession));

            // Traços
            var traits = CrewCards.Row(card, 20, 5);
            if (r.traits.Count == 0) CrewCards.Label(traits.transform, "Sem traços especiais", 12, UIKit.MutedText);
            foreach (var t in r.traits) CrewCards.TraitChip(traits.transform, t);

            // Força e moral
            var dmg = r.CombatDamage;
            var power = CrewCards.Row(card, 18, 6);
            CrewCards.Label(power.transform, $"Vida {r.CombatHp} · Dano {dmg.x}-{dmg.y}", 13, UIKit.TextColor);
            power.gameObject.AddComponent<Image>().color = Color.clear;
            UIKit.Tip(power, "Força em combate (no mar e nas expedições). Sobe com o nível.");
            CrewCards.MoraleRow(card, r.morale, 90);

            // Salário
            var wage = CrewCards.Row(card, 22, 5);
            CrewCards.Icon(wage.transform, VillageHUD.Instance != null ? VillageHUD.Instance.goldIcon : null, 18, 18);
            CrewCards.Label(wage.transform, $"{r.Wage} por dia", 14, UIKit.GoldColor);
            wage.gameObject.AddComponent<Image>().color = Color.clear;
            UIKit.Tip(wage, "Salário pago no fim de cada dia.");

            var spacer = UIKit.Rect("Espaco", card);
            UIKit.Size(spacer, height: 0).flexibleHeight = 1;

            bool can = cm.CanHire(r, out string reason);
            var recruit = r;
            var (btn, label, bg) = UIKit.Button(card, can ? $"Contratar · {r.HireCost} ouro" : reason, () => cm.Hire(recruit), 16);
            btn.interactable = can;
            if (can) UIKit.SetSelected(bg, true);
            UIKit.Fit(label, 11);
            UIKit.Size(btn, height: 42);
        }
    }
}
