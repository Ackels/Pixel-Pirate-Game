using Pixel.Village;
using UnityEngine;
using UnityEngine.UI;

namespace Pixel.Ship
{
    /// <summary>
    /// Janela do mapa-múndi: ilhas reveladas com ícone do tipo, o navio onde está,
    /// e um painel com distância, tempo, perigo e provisões do destino escolhido.
    /// </summary>
    public class MapPanel : MonoBehaviour
    {
        public Sprite paper, homeIcon, portIcon, wildIcon, campIcon, unknownIcon, shipIcon;

        SeaController sea;
        RectTransform mapArea;
        Text info;
        Button goButton;
        Text goLabel;
        IslandNode selected;
        const float AreaW = 760f, AreaH = 380f;

        public static MapPanel Create(Transform canvas, SeaController sea, Sprite[] icons)
        {
            MapPanel panel = null;
            var (win, content) = UIKit.Window("JanelaMapa", canvas, "Mapa dos Mares", new Vector2(800, 560), () => panel.Hide());
            panel = win.gameObject.AddComponent<MapPanel>();
            panel.sea = sea;
            panel.paper = icons[0]; panel.homeIcon = icons[1]; panel.portIcon = icons[2]; panel.wildIcon = icons[3];
            panel.campIcon = icons[4]; panel.unknownIcon = icons[5]; panel.shipIcon = icons[6];

            panel.mapArea = UIKit.Rect("AreaDoMapa", content);
            panel.mapArea.anchorMin = panel.mapArea.anchorMax = new Vector2(0.5f, 1f);
            panel.mapArea.pivot = new Vector2(0.5f, 1f);
            panel.mapArea.sizeDelta = new Vector2(AreaW, AreaH);
            panel.mapArea.anchoredPosition = Vector2.zero;
            var bg = panel.mapArea.gameObject.AddComponent<Image>();
            bg.color = new Color(0.47f, 0.70f, 0.80f);

            var bottom = UIKit.Rect("Rodape", content);
            bottom.anchorMin = new Vector2(0, 0);
            bottom.anchorMax = new Vector2(1, 0);
            bottom.pivot = new Vector2(0.5f, 0);
            bottom.sizeDelta = new Vector2(0, 92);
            bottom.anchoredPosition = Vector2.zero;
            panel.info = UIKit.Text(bottom, "", 15, TextAnchor.UpperLeft);
            panel.info.rectTransform.anchorMin = Vector2.zero;
            panel.info.rectTransform.anchorMax = new Vector2(1, 1);
            panel.info.rectTransform.offsetMin = new Vector2(0, 0);
            panel.info.rectTransform.offsetMax = new Vector2(-210, 0);
            var (go, label, _) = UIKit.Button(bottom, "", () => panel.Go(), 17);
            var grt = (RectTransform)go.transform;
            grt.anchorMin = grt.anchorMax = new Vector2(1, 0.5f);
            grt.pivot = new Vector2(1, 0.5f);
            grt.sizeDelta = new Vector2(200, 60);
            grt.anchoredPosition = Vector2.zero;
            panel.goButton = go;
            panel.goLabel = label;

            win.gameObject.SetActive(false);
            return panel;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            selected = null;
            Rebuild();
        }

        public void Hide() => gameObject.SetActive(false);

        Vector2 ToUI(Vector2 miles) =>
            new Vector2(miles.x / WorldMap.Width * AreaW - AreaW / 2f, miles.y / WorldMap.Height * AreaH - AreaH);

        Sprite IconFor(IslandNode n) => n.type switch
        {
            IslandType.Home => homeIcon,
            IslandType.Port => portIcon,
            IslandType.Wild => wildIcon,
            _ => campIcon,
        };

        void Rebuild()
        {
            UIKit.Clear(mapArea);
            var world = sea.World;
            foreach (var n in world.Islands)
            {
                if (!n.revealed) continue;
                var node = n;
                var rt = UIKit.Rect(n.name, mapArea);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(34, 34);
                rt.anchoredPosition = ToUI(n.pos);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = IconFor(n);
                img.color = n == selected ? new Color(1f, 1f, 0.6f) : n.visited || n.type == IslandType.Home ? Color.white : new Color(1, 1, 1, 0.8f);
                var btn = rt.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => { selected = node; Rebuild(); });

                string label = n.name + (n.hasTreasureHint && !n.looted ? " (X)" : "") + (n.visited && n.type != IslandType.Home ? " (visitada)" : "");
                var t = UIKit.Text(rt, label, 13, TextAnchor.UpperCenter, new Color(0.15f, 0.1f, 0.05f));
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                t.rectTransform.pivot = new Vector2(0.5f, 1f);
                t.rectTransform.sizeDelta = new Vector2(130, 20);
                t.rectTransform.anchoredPosition = new Vector2(0, -1);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            // Navio na posição atual
            if (sea.Location != null)
            {
                var s = UIKit.Rect("Navio", mapArea);
                s.anchorMin = s.anchorMax = new Vector2(0.5f, 1f);
                s.sizeDelta = new Vector2(26, 26);
                s.anchoredPosition = ToUI(sea.Location.pos) + new Vector2(20, 18);
                var si = s.gameObject.AddComponent<Image>();
                si.sprite = shipIcon;
                si.raycastTarget = false;
            }

            var hint = UIKit.Text(mapArea, "Ilhas mais longe ficam escondidas: explore para revelar.", 13, TextAnchor.LowerRight, new Color(0.15f, 0.1f, 0.05f));
            UIKit.Stretch(hint.rectTransform, 6);

            UpdateInfo();
        }

        void UpdateInfo()
        {
            if (selected == null)
            {
                info.text = "Clique numa ilha para ver a rota.\n" +
                            $"Você está em: {sea.Location?.name}. A bordo: {sea.People} pessoa(s). Peixe na vila: {sea.FoodStock}.";
                goButton.interactable = false;
                goLabel.text = "Escolha um destino";
                return;
            }
            var n = selected;
            int danger = n.Danger(sea.World.Home.pos);
            float miles = sea.LegMiles(n);
            int rations = sea.RationsFor(n);
            string food = rations <= sea.FoodStock ? $"{rations} peixe (tem {sea.FoodStock})"
                : UIKit.Colored($"{rations} peixe (só tem {sea.FoodStock}! vão passar fome)", new Color(1f, 0.45f, 0.4f));
            string status = n.type == IslandType.Home ? "Voltar encerra a viagem." :
                (n.visited ? (n.looted ? "Já saqueada. " : "Visitada. ") + (n.cleared ? "Sem inimigos." : "") : "Nunca visitada.");
            info.text = $"{UIKit.Colored(n.name, UIKit.GoldColor)} — {IslandNode.TypeName(n.type)}   Perigo: {danger}/5\n" +
                        $"Distância: {miles:0} milhas · Tempo: ~{sea.LegSecondsTo(n):0} s · Provisões: {food}\n{status}" +
                        (n.hasTreasureHint && !n.looted ? "\nUm X de tesouro marca esta ilha!" : "");
            bool can = sea.CanSetCourse(n, out string why);
            goButton.interactable = can;
            goLabel.text = can ? (n.type == IslandType.Home ? "Voltar pra casa" : "Navegar até lá") : why;
        }

        void Go()
        {
            if (selected == null) return;
            var target = selected;
            Hide();
            sea.SetCourse(target);
        }
    }
}
