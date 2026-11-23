using UnityEngine;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>Janela da Tripulação: todos os tripulantes com ofício, trabalho, moral, comida e salário.</summary>
    public class CrewPanel : MonoBehaviour
    {
        RectTransform list;
        Text summary;
        float refreshTimer;

        public static CrewPanel Create(Transform canvas)
        {
            CrewPanel panel = null;
            var (win, content) = UIKit.Window("JanelaTripulacao", canvas, "Tripulação", new Vector2(1000, 480), () => panel.Hide());
            panel = win.gameObject.AddComponent<CrewPanel>();
            UIKit.Vertical(content.gameObject, 0, 6);

            var header = Row(content, UIKit.Colored("Nome", UIKit.GoldColor), UIKit.Colored("Ofício", UIKit.GoldColor),
                UIKit.Colored("Trabalho", UIKit.GoldColor), UIKit.Colored("Moral", UIKit.GoldColor),
                UIKit.Colored("Hoje", UIKit.GoldColor), UIKit.Colored("Traços", UIKit.GoldColor));
            UIKit.Size(header, height: 24);

            panel.list = UIKit.Rect("Lista", content);
            UIKit.Vertical(panel.list.gameObject, 0, 4);
            UIKit.Size(panel.list, height: 340, flexWidth: 1);

            panel.summary = UIKit.Text(content, "", 15, TextAnchor.MiddleLeft, UIKit.MutedText);
            UIKit.Size(panel.summary, height: 30);

            win.gameObject.SetActive(false);
            return panel;
        }

        static readonly float[] Columns = { 190, 130, 200, 120, 110, 160 };

        static RectTransform Row(Transform parent, params string[] cells)
        {
            var row = UIKit.Rect("Linha", parent);
            UIKit.Horizontal(row.gameObject, 0, 6);
            for (int i = 0; i < cells.Length; i++)
            {
                var t = UIKit.Text(row, cells[i], 15, TextAnchor.MiddleLeft);
                UIKit.Size(t, width: Columns[i], height: 26);
            }
            return row;
        }

        public void Show() { gameObject.SetActive(true); transform.SetAsLastSibling(); Rebuild(); }
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

        void Rebuild()
        {
            var cm = CrewManager.Instance;
            if (cm == null || list == null) return;
            UIKit.Clear(list);
            int wages = 0;
            foreach (var c in cm.Crew)
            {
                var member = c;
                wages += c.Wage;
                string work = c.atSea ? UIKit.Colored("No mar", new Color(0.55f, 0.85f, 1f))
                    : c.workplace != null ? c.workplace.def.displayName : UIKit.Colored("Sem trabalho (vai pro mar)", UIKit.MutedText);
                string home = c.home != null ? "" : UIKit.Colored(" · sem casa!", new Color(1f, 0.5f, 0.45f));
                string morale = UIKit.Colored($"{c.morale} {CrewText.MoraleLabel(c.morale)}", CrewText.MoraleColor(c.morale));
                string today = (c.fedToday ? "comeu" : UIKit.Colored("fome", new Color(1f, 0.5f, 0.45f))) + " · " +
                               (c.paidToday ? "pago" : UIKit.Colored("sem pagar", new Color(1f, 0.5f, 0.45f)));
                var row = Row(list, c.name, $"{CrewText.Name(c.profession)} nv {c.level}", work + home, morale, today, c.TraitsText());
                var (btn, _, _) = UIKit.Button(row, "Dispensar", () => cm.Fire(member), 13);
                UIKit.Size(btn, width: 80, height: 24);
            }
            if (cm.Crew.Count == 0)
                UIKit.Size(UIKit.Text(list, "Ninguém no bando. Contrate na Taverna!", 16, TextAnchor.MiddleCenter, UIKit.MutedText), height: 40);

            summary.text = $"{cm.Crew.Count} tripulantes · Salários: {wages} ouro/dia · Camas livres: {cm.FreeBeds}. " +
                           "Moral baixa = trabalho lento. Abaixo de 15, o tripulante abandona o bando.";
        }
    }
}
