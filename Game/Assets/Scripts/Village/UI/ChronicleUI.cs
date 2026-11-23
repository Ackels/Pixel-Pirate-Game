using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>
    /// Painel da missão atual das Crônicas (canto esquerdo) e a janela com todas elas (tecla J).
    /// </summary>
    public class ChronicleUI : MonoBehaviour
    {
        RectTransform tracker, window, list;
        Text trackerText;
        float refresh;

        public static ChronicleUI Create(Transform canvas)
        {
            var holder = UIKit.Rect("Cronicas", canvas);
            UIKit.Stretch(holder);
            var ui = holder.gameObject.AddComponent<ChronicleUI>();

            // Painel da missão atual (clicável)
            ui.tracker = UIKit.Panel("MissaoAtual", holder, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -236), new Vector2(280, 96),
                new Color(0.28f, 0.2f, 0.08f, 0.92f));
            var btn = ui.tracker.gameObject.AddComponent<Button>();
            btn.targetGraphic = ui.tracker.GetComponent<Image>();
            btn.onClick.AddListener(ui.ToggleWindow);
            ui.trackerText = UIKit.Text(ui.tracker, "", 13, TextAnchor.UpperLeft);
            UIKit.Stretch(ui.trackerText.rectTransform, 8);

            // Janela com todas as crônicas
            var (win, content) = UIKit.Window("JanelaCronicas", holder, "Crônicas do Bando", new Vector2(640, 520), () => ui.window.gameObject.SetActive(false));
            ui.window = win;
            ui.list = content;
            UIKit.Vertical(content.gameObject, 0, 3);
            win.gameObject.SetActive(false);
            return ui;
        }

        public bool WindowOpen => window.gameObject.activeSelf;
        public void HideWindow() => window.gameObject.SetActive(false);

        public void ToggleWindow()
        {
            bool open = !window.gameObject.activeSelf;
            window.gameObject.SetActive(open);
            if (open) { window.SetAsLastSibling(); RebuildList(); }
        }

        void Update()
        {
            refresh -= Time.unscaledDeltaTime;
            if (refresh > 0) return;
            refresh = 0.5f;
            var cm = ChronicleManager.Instance;
            if (cm == null) { tracker.gameObject.SetActive(false); return; }

            var c = cm.Active;
            if (c == null)
                trackerText.text = UIKit.Colored("Crônicas concluídas!", UIKit.GoldColor) + "\nVocê é a lenda dos mares.\n" + UIKit.Colored("(J: ver crônicas)", UIKit.MutedText);
            else
            {
                string prog = c.progress != null ? $" ({c.progress()})" : "";
                trackerText.text =
                    UIKit.Colored($"Crônica {cm.Current + 1}/{cm.Chronicles.Count}: {c.title}", UIKit.GoldColor) + "\n" +
                    c.goal + prog + "\n" +
                    UIKit.Colored($"{c.hint}", UIKit.MutedText) + "\n" +
                    UIKit.Colored($"Recompensa: {c.reward} ouro · J: ver todas", new Color(1f, 0.85f, 0.4f));
            }
            if (window.gameObject.activeSelf) RebuildList();
        }

        void RebuildList()
        {
            var cm = ChronicleManager.Instance;
            if (cm == null) return;
            UIKit.Clear(list);
            for (int i = 0; i < cm.Chronicles.Count; i++)
            {
                var c = cm.Chronicles[i];
                bool done = i < cm.Current, current = i == cm.Current;
                string mark = done ? UIKit.Colored("[feito]", new Color(0.55f, 0.95f, 0.5f))
                    : current ? UIKit.Colored("[agora]", UIKit.GoldColor) : UIKit.Colored("[ - ]", UIKit.MutedText);
                string text = done || current ? $"{mark} {i + 1}. {c.title} — {c.goal}" : $"{mark} {i + 1}. ???";
                var t = UIKit.Text(list, text, 14, TextAnchor.MiddleLeft, done ? UIKit.MutedText : UIKit.TextColor);
                UIKit.Size(t, height: 26);
            }
        }
    }
}
