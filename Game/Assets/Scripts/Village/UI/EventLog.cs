using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>Registro no canto inferior esquerdo com as últimas notícias da vila.</summary>
    public class EventLog : MonoBehaviour
    {
        const int MaxLines = 5;
        Text text;

        public static EventLog Create(Transform canvas)
        {
            var root = UIKit.Panel("Registro", canvas, new Vector2(0, 0), new Vector2(0, 0), new Vector2(12, 12), new Vector2(420, 128),
                new Color(0.1f, 0.06f, 0.04f, 0.6f));
            Destroy(root.Find("Moldura").gameObject);   // o registro fica discreto, sem moldura
            var log = root.gameObject.AddComponent<EventLog>();
            log.text = UIKit.Text(root, "", 14, TextAnchor.LowerLeft);
            UIKit.Stretch(log.text.rectTransform, 8);
            UIKit.Fit(log.text, 11);   // muitas linhas longas: a letra encolhe um pouco em vez de vazar do painel
            return log;
        }

        void Start()
        {
            if (CrewManager.Instance != null) CrewManager.Instance.Logged += (_, __) => Refresh();
            Refresh();
        }

        void Refresh()
        {
            var cm = CrewManager.Instance;
            if (cm == null) return;
            var sb = new StringBuilder();
            int start = Mathf.Max(0, cm.History.Count - MaxLines);
            for (int i = start; i < cm.History.Count; i++)
                sb.AppendLine(UIKit.Colored(cm.History[i].msg, cm.History[i].color));
            text.text = sb.ToString();
        }
    }
}
