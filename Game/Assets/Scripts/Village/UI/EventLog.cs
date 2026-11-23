using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>Registro no canto inferior esquerdo com as últimas notícias da vila.</summary>
    public class EventLog : MonoBehaviour
    {
        const int MaxLines = 6;
        Text text;

        public static EventLog Create(Transform canvas)
        {
            var root = UIKit.Panel("Registro", canvas, new Vector2(0, 0), new Vector2(0, 0), new Vector2(12, 150), new Vector2(430, 136),
                new Color(0.12f, 0.08f, 0.05f, 0.75f));
            var log = root.gameObject.AddComponent<EventLog>();
            log.text = UIKit.Text(root, "", 14, TextAnchor.LowerLeft);
            UIKit.Stretch(log.text.rectTransform, 8);
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
