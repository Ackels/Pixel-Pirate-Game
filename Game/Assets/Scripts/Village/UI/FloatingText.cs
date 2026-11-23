using UnityEngine;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>Texto que sobe e some sobre um ponto do mundo ("+15 ouro", "+1 Cana").</summary>
    public class FloatingText : MonoBehaviour
    {
        const float Lifetime = 1.6f;
        const float RiseUnits = 1.2f;

        Vector3 worldStart;
        float age;
        Text text;
        Outline outline;
        Color baseColor;
        Camera cam;

        public static void Spawn(Vector3 worldPos, string message, Color color)
        {
            var hud = VillageHUD.Instance;
            if (hud == null || hud.FloatingRoot == null || !hud.isActiveAndEnabled) return;
            var go = new GameObject("TextoFlutuante", typeof(RectTransform));
            go.transform.SetParent(hud.FloatingRoot, false);
            var t = go.AddComponent<Text>();
            t.font = hud.UIFont;
            t.fontSize = 16;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = message;
            t.color = color;
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.85f);
            o.effectDistance = new Vector2(1, -1);
            var ft = go.AddComponent<FloatingText>();
            ft.worldStart = worldPos;
            ft.text = t;
            ft.outline = o;
            ft.baseColor = color;
            ft.cam = Camera.main;
            ft.Place();
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= Lifetime) { Destroy(gameObject); return; }
            float fade = 1f - Mathf.Clamp01((age - Lifetime * 0.6f) / (Lifetime * 0.4f));
            text.color = new Color(baseColor.r, baseColor.g, baseColor.b, fade);
            outline.effectColor = new Color(0, 0, 0, 0.85f * fade);
            Place();
        }

        void Place()
        {
            if (cam == null) return;
            var world = worldStart + Vector3.up * (RiseUnits * age / Lifetime);
            transform.position = cam.WorldToScreenPoint(world);
        }
    }
}
