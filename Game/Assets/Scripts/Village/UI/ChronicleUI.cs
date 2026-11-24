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
        Text trackerTitle, trackerGoal, trackerReward;
        GameObject rewardRow;
        float refresh;

        public static ChronicleUI Create(Transform canvas)
        {
            var holder = UIKit.Rect("Cronicas", canvas);
            UIKit.Stretch(holder);
            var ui = holder.gameObject.AddComponent<ChronicleUI>();

            // Painel da missão atual (clicável)
            // Só o essencial: nome, objetivo com progresso e recompensa. A dica de como fazer fica no mouse.
            ui.tracker = UIKit.Panel("MissaoAtual", holder, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -76), new Vector2(270, 0));
            var v = UIKit.Vertical(ui.tracker.gameObject, 0, 2);
            v.padding = new RectOffset(12, 12, 8, 10);
            ui.tracker.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var btn = ui.tracker.gameObject.AddComponent<Button>();
            btn.targetGraphic = ui.tracker.GetComponent<Image>();
            btn.onClick.AddListener(ui.ToggleWindow);
            ui.trackerTitle = UIKit.Text(ui.tracker, "", 15, TextAnchor.MiddleLeft, UIKit.GoldColor);
            ui.trackerGoal = UIKit.Text(ui.tracker, "", 13, TextAnchor.UpperLeft);
            var row = UIKit.Rect("Recompensa", ui.tracker);
            ui.rewardRow = row.gameObject;
            UIKit.Horizontal(row.gameObject, 0, 4);
            var coin = UIKit.Rect("Moeda", row);
            var ci = coin.gameObject.AddComponent<Image>();
            ci.sprite = VillageHUD.Instance != null ? VillageHUD.Instance.goldIcon : null;
            ci.enabled = ci.sprite != null;
            ci.preserveAspect = true;
            ci.raycastTarget = false;
            UIKit.Size(coin, 18, 18);
            ui.trackerReward = UIKit.Text(row, "", 13, TextAnchor.MiddleLeft, UIKit.GoldColor);
            UIKit.Size(ui.trackerReward, 200, 18);
            UIKit.Tip(ui.tracker, () =>
            {
                var cm = ChronicleManager.Instance;
                var a = cm != null ? cm.Active : null;
                string how = a != null ? $"{a.hint}\n\n" : "";
                return how + "Clique (ou J) para ver todas as crônicas.";
            });

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
            {
                trackerTitle.text = "Crônicas concluídas!";
                trackerGoal.text = "Você é a lenda dos mares.";
                rewardRow.SetActive(false);
            }
            else
            {
                trackerTitle.text = $"{c.title} {UIKit.Colored($"{cm.Current + 1}/{cm.Chronicles.Count}", UIKit.MutedText)}";
                trackerGoal.text = c.progress != null ? $"{c.goal} {UIKit.Colored($"({c.progress()})", UIKit.GoldColor)}" : c.goal;
                trackerReward.text = c.reward.ToString();
                rewardRow.SetActive(true);
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
