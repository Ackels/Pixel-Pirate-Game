using System;
using Pixel.Village;
using UnityEngine;
using UnityEngine.UI;

namespace Pixel.Ship
{
    /// <summary>
    /// Tela de decisão (inspirada no Pixel Piracy): a tela escurece, o jogo para e aparece a pergunta com as
    /// opções ("Encontro hostil! O que vamos fazer, capitão?" → Enfrentar / Fugir). Usada nos encontros com
    /// navios e nos eventos do mar. Sem a tela (cena de teste), a primeira opção é escolhida na hora.
    /// </summary>
    public class SeaDialog : MonoBehaviour
    {
        public struct Option
        {
            public string label;
            public Action onChoose;
            public Option(string label, Action onChoose) { this.label = label; this.onChoose = onChoose; }
        }

        public static SeaDialog Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        RectTransform root, box;

        public static SeaDialog Create(Transform canvas)
        {
            var go = new GameObject("TelaDeDecisao", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var d = go.AddComponent<SeaDialog>();
            d.root = (RectTransform)go.transform;
            UIKit.Stretch(d.root);
            var dim = go.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.03f, 0.06f, 0.86f);   // escurece o mar e bloqueia os cliques

            d.box = UIKit.Panel("Caixa", d.root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 0));
            var v = UIKit.Vertical(d.box.gameObject, 22, 10);
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = true;
            v.childForceExpandWidth = true;
            d.box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            go.SetActive(false);
            Instance = d;
            return d;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (IsOpen) { IsOpen = false; Time.timeScale = 1f; }
        }

        /// <summary>Mostra a pergunta e para o jogo até o jogador escolher.</summary>
        public static void Ask(string title, string body, params Option[] options)
        {
            if (Instance == null || options.Length == 0) { if (options.Length > 0) options[0].onChoose?.Invoke(); return; }
            Instance.Open(title, body, options);
        }

        void Open(string title, string body, Option[] options)
        {
            UIKit.Clear(box);
            var t = UIKit.Text(box, title, 24, TextAnchor.MiddleCenter, UIKit.GoldColor);
            UIKit.Size(t, height: 40);
            UIKit.Fit(t, 14);
            if (!string.IsNullOrEmpty(body))
            {
                var b = UIKit.Text(box, body, 15, TextAnchor.MiddleCenter);
                b.horizontalOverflow = HorizontalWrapMode.Wrap;
                b.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            var gap = UIKit.Rect("Espaco", box);
            UIKit.Size(gap, height: 4);
            foreach (var o in options)
            {
                var opt = o;
                var (btn, _, _) = UIKit.Button(box, opt.label, () => Choose(opt), 17);
                UIKit.Size((RectTransform)btn.transform, height: 46);
            }
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            IsOpen = true;
            Time.timeScale = 0f;
            AudioManager.Play(Sfx.Quest);
        }

        void Choose(Option o)
        {
            root.gameObject.SetActive(false);
            IsOpen = false;
            Time.timeScale = CrewCommander.Paused ? 0f : SandboxMode.Active ? SandboxMode.Speed : 1f;
            AudioManager.Play(Sfx.Click);
            o.onChoose?.Invoke();
        }
    }
}
