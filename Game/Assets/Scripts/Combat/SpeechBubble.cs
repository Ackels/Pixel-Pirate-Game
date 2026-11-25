using System.Collections.Generic;
using Pixel.Village;
using UnityEngine;
using UnityEngine.UI;

namespace Pixel.Combat
{
    /// <summary>
    /// Balãozinho de fala em cima da cabeça de alguém (visão lateral). Um por pessoa: uma fala nova troca a anterior.
    /// SpeechBubble.Say(transform, "Iô-hô-hô!");
    /// </summary>
    public class SpeechBubble : MonoBehaviour
    {
        static readonly Dictionary<Transform, SpeechBubble> open = new Dictionary<Transform, SpeechBubble>();

        Transform follow;
        float height, timeLeft, total;
        CanvasGroup group;
        Text label;

        /// <summary>Mostra a fala por alguns segundos. height = altura acima dos pés, em blocos.</summary>
        public static void Say(Transform who, string text, float seconds = 2.8f, float height = 1.45f)
        {
            if (who == null || string.IsNullOrEmpty(text)) return;
            if (!open.TryGetValue(who, out var b) || b == null)
            {
                b = Create();
                open[who] = b;
            }
            b.follow = who;
            b.height = height;
            b.timeLeft = b.total = seconds;
            b.label.text = text;
            b.gameObject.SetActive(true);
            b.LateUpdate();
        }

        public static bool IsTalking(Transform who) => who != null && open.TryGetValue(who, out var b) && b != null && b.timeLeft > 0f;

        static SpeechBubble Create()
        {
            var go = new GameObject("Fala", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30;
            var rt = (RectTransform)go.transform;
            rt.localScale = Vector3.one / 32f;               // 32 pontos da fala = 1 bloco
            rt.sizeDelta = new Vector2(10, 10);
            rt.pivot = new Vector2(0.5f, 0f);

            var b = go.AddComponent<SpeechBubble>();
            b.group = go.GetComponent<CanvasGroup>();

            // Papel que cresce com o texto
            var box = UIKit.Rect("Papel", rt);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0f);
            box.pivot = new Vector2(0.5f, 0f);
            var img = box.gameObject.AddComponent<Image>();
            if (UIKit.Skin.Paper != null) UIKit.UseSkin(img, UIKit.Skin.Paper, Image.Type.Sliced);
            else img.color = new Color(0.95f, 0.9f, 0.78f);
            img.pixelsPerUnitMultiplier = 1f;
            var h = UIKit.Horizontal(box.gameObject, 0, 0);
            h.padding = new RectOffset(7, 7, 4, 5);
            h.childAlignment = TextAnchor.MiddleCenter;
            var fit = box.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            b.label = UIKit.Text(box, "", 11, TextAnchor.MiddleCenter, UIKit.ButtonText);
            b.label.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Rabinho do balão
            var tail = UIKit.Rect("Rabinho", rt);
            tail.anchorMin = tail.anchorMax = new Vector2(0.5f, 0f);
            tail.pivot = new Vector2(0.5f, 1f);
            tail.anchoredPosition = new Vector2(0, 1);
            tail.sizeDelta = new Vector2(4, 3);
            tail.gameObject.AddComponent<Image>().color = new Color32(0xee, 0xbd, 0x8a, 255);
            return b;
        }

        void LateUpdate()
        {
            if (follow == null) { Destroy(gameObject); return; }
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f) { gameObject.SetActive(false); return; }
            transform.position = follow.position + Vector3.up * height * Mathf.Max(1f, follow.lossyScale.y);
            group.alpha = Mathf.Clamp01(timeLeft / 0.35f) * Mathf.Clamp01((total - timeLeft) / 0.12f);
        }

        void OnDestroy()
        {
            if (follow != null && open.TryGetValue(follow, out var b) && b == this) open.Remove(follow);
        }
    }
}
