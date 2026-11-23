using UnityEngine;

namespace Pixel.Combat
{
    /// <summary>Prancha de madeira entre dois pontos (navio↔navio ou navio↔praia), com colisão para andar.</summary>
    public static class Gangplank
    {
        public static GameObject Create(Vector2 a, Vector2 b, Sprite plank, string name = "Prancha")
        {
            var root = new GameObject(name);
            var edge = root.AddComponent<EdgeCollider2D>();
            edge.points = new[] { a + Vector2.left * 0.4f, b + Vector2.right * 0.4f };
            var sr = new GameObject("Tabuas").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root.transform, false);
            sr.sprite = plank;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(Vector2.Distance(a, b) + 0.8f, 1f);
            sr.sortingOrder = 2;
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            sr.transform.position = (a + b) / 2f + Vector2.down * 0.45f;
            sr.transform.rotation = Quaternion.Euler(0, 0, ang);
            return root;
        }
    }
}
