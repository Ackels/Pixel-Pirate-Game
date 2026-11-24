using UnityEngine;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>Janela da Taverna: recrutas do dia com ofício, traços (visíveis!), salário e botão Contratar.</summary>
    public class TavernPanel : MonoBehaviour
    {
        RectTransform cards;

        public static TavernPanel Create(Transform canvas)
        {
            var panel = (TavernPanel)null;
            var (win, content) = UIKit.Window("JanelaTaverna", canvas, "Taverna — Recrutas do dia", new Vector2(900, 360), () => panel.Hide());
            panel = win.gameObject.AddComponent<TavernPanel>();
            UIKit.Vertical(content.gameObject, 0, 8);

            panel.cards = UIKit.Rect("Cartas", content);
            var h = UIKit.Horizontal(panel.cards.gameObject, 0, 10);
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            UIKit.Size(panel.cards, height: 290, flexWidth: 1);

            UIKit.Info(win, () =>
            {
                var cm = CrewManager.Instance;
                string nums = cm == null ? "" : $"Camas livres: {cm.FreeBeds} · Tripulação: {cm.Crew.Count}\n\n";
                return nums + "Cada tripulante precisa de uma cama (Casa), 1 peixe e salário por dia. Rum deixa todo mundo mais feliz.\n" +
                       "Novos recrutas chegam a cada dia.";
            });

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

            if (cm.Recruits.Count == 0)
            {
                var empty = UIKit.Text(cards, "Ninguém na taverna hoje. Volte amanhã!", 18, TextAnchor.MiddleCenter, UIKit.MutedText);
                UIKit.Size(empty, flexWidth: 1);
            }

            foreach (var r in cm.Recruits)
            {
                var recruit = r;
                var card = UIKit.Rect("Carta", cards);
                card.gameObject.AddComponent<Image>().color = UIKit.CardColor;
                UIKit.Vertical(card.gameObject, 10, 6);

                var name = UIKit.Text(card, r.name, 19, TextAnchor.UpperLeft, UIKit.GoldColor);
                UIKit.Size(name, height: 26);
                var prof = UIKit.Text(card, $"{CrewText.Name(r.profession)} · nível {r.level}", 16, TextAnchor.UpperLeft);
                UIKit.Size(prof, height: 22);

                string traits = "";
                foreach (var t in r.traits)
                {
                    var c = CrewText.IsGood(t) ? new Color(0.55f, 0.95f, 0.5f) : new Color(1f, 0.6f, 0.5f);
                    traits += UIKit.Colored(CrewText.Name(t), c) + ": " + CrewText.Describe(t) + "\n";
                }
                var traitText = UIKit.Text(card, traits, 14, TextAnchor.UpperLeft, UIKit.MutedText);
                UIKit.Size(traitText, height: 70);

                var wage = UIKit.Text(card, $"Salário: {r.Wage} ouro/dia\nMoral: {CrewText.MoraleLabel(r.morale)}", 15, TextAnchor.UpperLeft);
                UIKit.Size(wage, height: 42);

                bool can = cm.CanHire(r, out string reason);
                var (btn, label, _) = UIKit.Button(card, can ? $"Contratar ({r.HireCost} ouro)" : reason, () => cm.Hire(recruit), 15);
                btn.interactable = can;
                UIKit.Size(btn, height: 36);
            }
        }
    }
}
