using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>
    /// Dica que aparece perto do mouse quando ele para em cima de um botão ou ícone (UIKit.Tip).
    /// Uma caixa só por tela, criada na primeira vez.
    /// </summary>
    /// <summary>Botão "i" de uma janela (UIKit.Info): abre e fecha o quadro com as explicações.</summary>
    public class InfoButton : MonoBehaviour
    {
        RectTransform box;
        Text label;
        Image bg;
        Func<string> text;
        float refresh;

        public void Setup(RectTransform box, Text label, Image bg, Func<string> text)
        {
            this.box = box; this.label = label; this.bg = bg; this.text = text;
        }

        public void Toggle()
        {
            bool open = !box.gameObject.activeSelf;
            box.gameObject.SetActive(open);
            UIKit.SetSelected(bg, open);
            if (open) { box.SetAsLastSibling(); label.text = text(); }
        }

        void Update()
        {
            if (!box.gameObject.activeSelf || (refresh -= Time.unscaledDeltaTime) > 0) return;
            refresh = 0.5f;
            label.text = text();
        }

        void OnDisable()
        {
            if (box != null && box.gameObject.activeSelf) { box.gameObject.SetActive(false); UIKit.SetSelected(bg, false); }
        }
    }

    public class Tooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Func<string> text;

        static Tooltip current;
        static RectTransform box;
        static Text label;
        float delay;

        public void OnPointerEnter(PointerEventData e) { current = this; delay = 0.25f; }
        public void OnPointerExit(PointerEventData e) { if (current == this) Hide(); }
        void OnDisable() { if (current == this) Hide(); }

        static void Hide()
        {
            current = null;
            if (box != null) box.gameObject.SetActive(false);
        }

        void Update()
        {
            if (current != this) return;
            if ((delay -= Time.unscaledDeltaTime) > 0) return;
            string s = text?.Invoke();
            if (string.IsNullOrEmpty(s)) { if (box != null) box.gameObject.SetActive(false); return; }

            var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null) return;
            if (box == null || box.parent != canvas.transform) Build(canvas.transform);
            box.gameObject.SetActive(true);
            box.SetAsLastSibling();
            if (label.text != s)
            {
                label.text = s;
                float w = Mathf.Min(340f, label.preferredWidth + 2f);
                label.rectTransform.sizeDelta = new Vector2(w, 0);
                float h = label.cachedTextGeneratorForLayout.GetPreferredHeight(s, label.GetGenerationSettings(new Vector2(w, 0))) / label.pixelsPerUnit;
                box.sizeDelta = new Vector2(w + 24, h + 20);
            }

            // Perto do mouse, sem sair da tela
            var root = (RectTransform)canvas.transform;
            var mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, mouse, null, out var local);
            var half = root.rect.size * 0.5f;
            var size = box.sizeDelta;
            var pos = local + new Vector2(16, 20);
            if (pos.x + size.x > half.x - 6) pos.x = local.x - 16 - size.x;
            if (pos.y + size.y > half.y - 6) pos.y = local.y - 20 - size.y;
            pos.x = Mathf.Max(pos.x, -half.x + 6);
            pos.y = Mathf.Max(pos.y, -half.y + 6);
            box.anchoredPosition = pos;
        }

        static void Build(Transform canvas)
        {
            if (box != null) Destroy(box.gameObject);
            box = UIKit.Panel("Dica", canvas, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Vector2(200, 40));
            foreach (var g in box.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            label = UIKit.Text(box, "", 14, TextAnchor.UpperLeft);
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(12, -10);
        }
    }
}
