using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>Painel lateral com informações do prédio clicado (status, trabalhadores, moradores).</summary>
    public class BuildingInfoPanel : MonoBehaviour
    {
        Building target;
        Text title, body;
        Button tavernButton;
        float refreshTimer;

        public static BuildingInfoPanel Create(Transform canvas, System.Action openTavern)
        {
            var root = UIKit.Panel("PainelPredio", canvas, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 30), new Vector2(330, 300),
                new Color(UIKit.PanelColor.r, UIKit.PanelColor.g, UIKit.PanelColor.b, 1f));
            var panel = root.gameObject.AddComponent<BuildingInfoPanel>();
            UIKit.Vertical(root.gameObject, 12, 8);

            var header = UIKit.Rect("Cabecalho", root);
            UIKit.Size(header, height: 30);
            panel.title = UIKit.Text(header, "", 22, TextAnchor.MiddleLeft, UIKit.GoldColor);
            UIKit.Stretch(panel.title.rectTransform);
            var (close, _, _) = UIKit.Button(header, "X", panel.Hide, 18);
            var crt = (RectTransform)close.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1, 0.5f);
            crt.pivot = new Vector2(1, 0.5f);
            crt.sizeDelta = new Vector2(30, 26);

            panel.body = UIKit.Text(root, "", 15, TextAnchor.UpperLeft);
            UIKit.Size(panel.body, height: 200, flexWidth: 1);

            var (tb, _, _) = UIKit.Button(root, "Ver recrutas da Taverna", () => openTavern(), 16);
            UIKit.Size(tb, height: 34);
            panel.tavernButton = tb;

            root.gameObject.SetActive(false);
            return panel;
        }

        public void Show(Building b)
        {
            target = b;
            gameObject.SetActive(true);
            Refresh();
        }

        public void Hide() => gameObject.SetActive(false);

        void Update()
        {
            if (target == null) { Hide(); return; }
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f) { refreshTimer = 0.25f; Refresh(); }
        }

        void Refresh()
        {
            if (target == null) return;
            var def = target.def;
            var cm = CrewManager.Instance;
            title.text = def.displayName;
            tavernButton.gameObject.SetActive(def.kind == BuildingKind.Tavern);

            var sb = new StringBuilder();
            sb.AppendLine(UIKit.Colored(def.description, UIKit.MutedText));
            sb.AppendLine();
            switch (def.kind)
            {
                case BuildingKind.Producer:
                    sb.AppendLine(target.Status);
                    int pct = Mathf.RoundToInt(target.Progress * 100f);
                    sb.AppendLine($"Próxima produção: {pct}%");
                    sb.AppendLine();
                    sb.AppendLine($"Trabalhadores ({target.Workers.Count}/{def.workerSlots}) · ideal: {CrewText.Name(def.profession)}");
                    foreach (var w in target.Workers)
                    {
                        string match = w.profession == def.profession ? "" : UIKit.Colored(" (fora do ofício: 50%)", new Color(1f, 0.6f, 0.5f));
                        sb.AppendLine($"• {w.name} — {CrewText.Name(w.profession)} nv {w.level}{match}");
                    }
                    if (target.Workers.Count == 0) sb.AppendLine(UIKit.Colored("Ninguém trabalhando aqui.", new Color(1f, 0.6f, 0.5f)));
                    break;

                case BuildingKind.House:
                    int residents = 0;
                    if (cm != null)
                        foreach (var c in cm.Crew)
                            if (c.home == target) { residents++; sb.AppendLine($"• {c.name}"); }
                    sb.Insert(0, "");
                    sb.AppendLine($"Moradores: {residents}/{def.housing}");
                    break;

                case BuildingKind.Market:
                    sb.AppendLine("Visitantes compram aqui o item mais caro do estoque.");
                    sb.AppendLine("Comida e rum da tripulação ficam guardados (não são vendidos).");
                    break;

                case BuildingKind.Shipyard:
                    sb.AppendLine("Libera a edição do navio.");
                    sb.AppendLine("Vá até o fim do píer e aperte E para embarcar;");
                    sb.AppendLine("lá, clique em \"Editar navio\".");
                    break;

                case BuildingKind.Tavern:
                    if (cm != null)
                    {
                        sb.AppendLine($"{cm.Recruits.Count} recruta(s) esperando. Novos chegam a cada dia.");
                        sb.AppendLine($"Camas livres: {cm.FreeBeds}");
                    }
                    break;
            }
            body.text = sb.ToString();
        }
    }
}
