using UnityEngine;

namespace Pixel.Combat
{
    /// <summary>
    /// Peças comuns das ordens de combate (clique do capitão e ordens arrastadas aos tripulantes):
    /// achar quem está debaixo do mouse e desenhar a linha em arco da ordem.
    /// </summary>
    public static class Orders
    {
        public static readonly Color AttackColor = new Color(0.86f, 0.2f, 0.25f, 0.95f);
        public static readonly Color MoveColor = new Color(0.95f, 0.85f, 0.45f, 0.95f);

        /// <summary>Lutador (de pé) perto de um ponto do mundo. team = null aceita qualquer time.</summary>
        public static Combatant At(Vector2 p, Team? team, bool enemiesOfPlayer, float radius = 0.75f)
        {
            Combatant best = null;
            float bestD = radius;
            foreach (var c in Combatant.All)
            {
                if (c == null || c.IsDown) continue;
                if (team.HasValue && c.team != team.Value) continue;
                if (enemiesOfPlayer && c.team == Team.Player) continue;
                Vector2 body = (Vector2)c.transform.position + Vector2.up * (c.figureHeight * 0.5f);
                float d = Vector2.Distance(p, body);
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        /// <summary>Linha em arco (como uma trajetória) de a até b.</summary>
        public static void Arc(LineRenderer lr, Vector2 a, Vector2 b)
        {
            int n = lr.positionCount;
            float h = Mathf.Clamp(Vector2.Distance(a, b) * 0.3f, 0.6f, 4f);
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1);
                lr.SetPosition(i, Vector2.Lerp(a, b, u) + Vector2.up * (h * 4f * u * (1f - u)));
            }
        }

        public static LineRenderer MakeLine(string name, Color color, float width = 0.08f)
        {
            var lr = RopeSwing.MakeRope(null, name, 24, width);
            lr.startColor = lr.endColor = color;
            lr.sortingOrder = 80;
            return lr;
        }

        /// <summary>Mostra a ordem por um instante (arco que some sozinho, mesmo com o jogo pausado).</summary>
        public static void Flash(Vector2 from, Vector2 to, Color color)
        {
            var lr = MakeLine("OrdemDada", color);
            Arc(lr, from, to);
            lr.gameObject.AddComponent<FadeLine>();
        }

        class FadeLine : MonoBehaviour
        {
            LineRenderer lr;
            float t;
            Color c;
            void Start() { lr = GetComponent<LineRenderer>(); c = lr.startColor; }
            void Update()
            {
                t += Time.unscaledDeltaTime;
                var k = c;
                k.a = c.a * Mathf.Clamp01(1f - t / 0.8f);
                lr.startColor = lr.endColor = k;
                if (t > 0.8f) Destroy(gameObject);
            }
        }
    }
}
