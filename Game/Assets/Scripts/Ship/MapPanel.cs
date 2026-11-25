using Pixel.Village;
using UnityEngine;
using UnityEngine.UI;

namespace Pixel.Ship
{
    /// <summary>
    /// Janela do mapa-múndi: ilhas reveladas com ícone do tipo, ilhas "?" (desconhecidas, perto de onde você já
    /// esteve: dá pra navegar até elas para descobrir), o navio onde está, a rota pontilhada até o destino,
    /// dica no mouse (tipo, perigo, distância) e um painel com distância, tempo, perigo e provisões.
    /// </summary>
    public class MapPanel : MonoBehaviour
    {
        public Sprite paper, homeIcon, portIcon, wildIcon, campIcon, unknownIcon, shipIcon, fortIcon;

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
            panel.fortIcon = icons.Length > 7 ? icons[7] : icons[4];

            panel.mapArea = UIKit.Rect("AreaDoMapa", content);
            panel.mapArea.anchorMin = panel.mapArea.anchorMax = new Vector2(0.5f, 1f);
            panel.mapArea.pivot = new Vector2(0.5f, 1f);
            panel.mapArea.sizeDelta = new Vector2(AreaW, AreaH);
            panel.mapArea.anchoredPosition = Vector2.zero;
            var bg = panel.mapArea.gameObject.AddComponent<Image>();
            if (panel.paper != null && panel.paper.border != Vector4.zero)
            {
                // Pergaminho de mapa do tesouro (cantos fixos, bordas e meio esticados, pixel em dobro)
                bg.sprite = panel.paper;
                bg.type = Image.Type.Sliced;
                bg.pixelsPerUnitMultiplier = 0.5f * 100f / panel.paper.pixelsPerUnit;   // 1 pixel do papel = 2 pontos da tela (o Canvas usa 100 pontos por unidade)
                bg.color = Color.white;
            }
            else bg.color = new Color(0.47f, 0.70f, 0.80f);

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

        // Margens para as ilhas (e os nomes embaixo delas) ficarem dentro do pergaminho
        const float MarginX = 44f, MarginTop = 34f, MarginBottom = 46f;

        Vector2 ToUI(Vector2 miles) => new Vector2(
            MarginX + miles.x / WorldMap.Width * (AreaW - 2f * MarginX) - AreaW / 2f,
            -AreaH + MarginBottom + miles.y / WorldMap.Height * (AreaH - MarginTop - MarginBottom));

        /// <summary>Até onde, a partir das ilhas visitadas, aparecem as ilhas "?".</summary>
        const float RumorRadius = WorldMap.RevealRadius * 2f;

        bool Rumored(IslandNode n)
        {
            if (n.revealed) return false;
            foreach (var v in sea.World.Islands)
                if (v.visited && Vector2.Distance(v.pos, n.pos) <= RumorRadius) return true;
            return false;
        }

        static Color DangerColor(int d) => d <= 1 ? new Color(0.45f, 0.85f, 0.4f) : d <= 3 ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.42f, 0.38f);

        string Tooltip(IslandNode n)
        {
            int danger = n.Danger(sea.World.Home.pos);
            string title = n.revealed ? $"{n.name}\n{IslandNode.TypeName(n.type)}" : "Ilha desconhecida\nNinguém sabe o que tem lá";
            string dist = n == sea.Location ? "Você está aqui" : $"{sea.LegMiles(n):0} milhas · ~{sea.LegSecondsTo(n):0} s";
            return $"{title}\n{UIKit.Colored($"Perigo {danger}/5", DangerColor(danger))}\n{dist}";
        }

        Sprite IconFor(IslandNode n) => !n.revealed ? unknownIcon : n.type switch
        {
            IslandType.Home => homeIcon,
            IslandType.Port => portIcon,
            IslandType.Wild => wildIcon,
            IslandType.Fortress => fortIcon,
            _ => campIcon,
        };

        void Rebuild()
        {
            UIKit.Clear(mapArea);
            var world = sea.World;
            // Rota pontilhada até o destino escolhido (desenhada antes, fica embaixo das ilhas)
            if (selected != null && sea.Location != null)
            {
                Vector2 a = ToUI(sea.Location.pos), b = ToUI(selected.pos);
                int dots = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(a, b) / 12f));
                for (int i = 1; i < dots; i++)
                {
                    var d = UIKit.Rect("Rota", mapArea);
                    d.anchorMin = d.anchorMax = new Vector2(0.5f, 1f);
                    d.sizeDelta = new Vector2(5, 5);
                    d.anchoredPosition = Vector2.Lerp(a, b, i / (float)dots);
                    var di = d.gameObject.AddComponent<Image>();
                    di.color = new Color(0.55f, 0.12f, 0.1f, 0.85f);
                    di.raycastTarget = false;
                }
            }

            foreach (var n in world.Islands)
            {
                if (!n.revealed && !Rumored(n)) continue;
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
                UIKit.Tip(rt, () => Tooltip(node));

                // Selo de perigo no canto do ícone
                if (n.type != IslandType.Home)
                {
                    int dg = n.Danger(world.Home.pos);
                    var badge = UIKit.Rect("Perigo", rt);
                    badge.anchorMin = badge.anchorMax = new Vector2(1f, 1f);
                    badge.sizeDelta = new Vector2(16, 16);
                    badge.anchoredPosition = new Vector2(-2, -2);
                    var bi = badge.gameObject.AddComponent<Image>();
                    bi.color = new Color(0.12f, 0.08f, 0.05f, 0.85f);
                    bi.raycastTarget = false;
                    var bt = UIKit.Text(badge, dg.ToString(), 12, TextAnchor.MiddleCenter, DangerColor(dg));
                    UIKit.Stretch(bt.rectTransform);
                    bt.raycastTarget = false;
                }

                string label = !n.revealed ? "???" : n.name + (n.hasTreasureHint && !n.looted ? " (X)" : "") + (n.visited && n.type != IslandType.Home ? " (visitada)" : "");
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

            var hint = UIKit.Text(mapArea, "Ilhas \"?\" são desconhecidas: navegue até elas para descobrir. O número é o perigo.", 13, TextAnchor.LowerRight, new Color(0.15f, 0.1f, 0.05f));
            UIKit.Stretch(hint.rectTransform, 36);   // dentro do pergaminho (a borda do papel tem margem transparente)

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
            string who = n.revealed ? $"{UIKit.Colored(n.name, UIKit.GoldColor)} — {IslandNode.TypeName(n.type)}" : UIKit.Colored("Ilha desconhecida", UIKit.GoldColor) + " — vá descobrir!";
            if (!n.revealed) status = "Ninguém sabe o que tem lá: pode ser um porto, uma ilha selvagem ou um acampamento pirata.";
            info.text = $"{who}   {UIKit.Colored($"Perigo: {danger}/5", DangerColor(danger))}\n" +
                        $"Distância: {miles:0} milhas · Tempo: ~{sea.LegSecondsTo(n):0} s · Provisões: {food}\n{status}" +
                        (n.hasTreasureHint && !n.looted ? "\nUm X de tesouro marca esta ilha!" : "");
            bool can = sea.CanSetCourse(n, out string why);
            goButton.interactable = can;
            goLabel.text = can ? (n.type == IslandType.Home ? "Voltar pra casa" : n.revealed ? "Navegar até lá" : "Explorar!") : why;
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
