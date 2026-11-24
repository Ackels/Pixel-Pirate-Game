using UnityEngine;

namespace Pixel.Combat
{
    /// <summary>
    /// Abordagem no cipó: o lutador se pendura numa corda presa no alto e balança em arco até o outro navio.
    /// Enquanto balança, a IA e a física ficam em pausa; ao pousar, volta tudo ao normal.
    /// </summary>
    public class RopeSwing : MonoBehaviour
    {
        static Material ropeMat;
        public static readonly Color RopeColor = new Color32(0x8a, 0x62, 0x3a, 255);

        Vector2 from, to, anchor;
        float duration, t;
        Rigidbody2D body;
        CombatAI ai;
        LineRenderer line;
        RigidbodyType2D oldType;
        System.Action onLand;

        public bool Done { get; private set; }

        /// <summary>Esta pessoa está pendurada (ou prestes a se pendurar) numa corda.</summary>
        public static bool IsBusy(GameObject go) => go != null && go.GetComponent<RopeSwing>() != null;

        /// <summary>Faz alguém balançar de onde está até <paramref name="target"/>, com a corda presa em <paramref name="anchorPoint"/>.</summary>
        public static RopeSwing Begin(Component who, Vector2 target, Vector2 anchorPoint, float delay = 0f, System.Action landed = null)
        {
            if (who == null) return null;
            var old = who.GetComponent<RopeSwing>();
            if (old != null) Destroy(old);
            var s = who.gameObject.AddComponent<RopeSwing>();
            s.to = target;
            s.anchor = anchorPoint;
            s.t = -delay;
            s.onLand = landed;
            return s;
        }

        public static LineRenderer MakeRope(Transform parent, string name, int points, float width = 0.06f)
        {
            if (ropeMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                ropeMat = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
            }
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = ropeMat;
            lr.positionCount = points;
            lr.startWidth = lr.endWidth = width;
            lr.startColor = lr.endColor = RopeColor;
            lr.useWorldSpace = true;
            lr.sortingOrder = 7;
            lr.numCapVertices = 0;
            return lr;
        }

        /// <summary>Corda solta entre dois pontos, com barriga (catenária simples).</summary>
        public static void Sag(LineRenderer lr, Vector2 a, Vector2 b, float sag)
        {
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1);
                var p = Vector2.Lerp(a, b, u) + Vector2.down * (sag * 4f * u * (1f - u));
                lr.SetPosition(i, p);
            }
        }

        bool started;

        void Start()
        {
            body = GetComponent<Rigidbody2D>();
            ai = GetComponent<CombatAI>();
            line = MakeRope(null, "Corda", 2, 0.05f);
            line.enabled = false;
        }

        void Update()
        {
            t += Time.deltaTime;
            if (t < 0f) return;
            if (!started)
            {
                // Agarra a corda: congela a IA e a física e parte de onde estiver agora
                started = true;
                if (ai != null) ai.externalControl = true;
                if (body != null)
                {
                    oldType = body.bodyType;
                    body.linearVelocity = Vector2.zero;
                    body.bodyType = RigidbodyType2D.Kinematic;
                }
                from = transform.position;
                duration = Mathf.Clamp(Vector2.Distance(from, to) / 7f, 0.6f, 1.2f);
                t = 0f;
            }
            float u = Mathf.Clamp01(t / duration);
            // Arco: sai do ponto de partida, desce pela "barriga" do balanço e sobe até o outro convés
            var mid = new Vector2((from.x + to.x) / 2f, Mathf.Min(from.y, to.y) - 0.6f);
            var p = Bezier(from, mid, to, Smooth(u)) + Vector2.up * (Mathf.Sin(u * Mathf.PI) * 0.4f);
            if (body != null) body.position = p;
            transform.position = p;
            line.enabled = u < 0.98f;
            line.SetPosition(0, anchor);
            line.SetPosition(1, p + Vector2.up * 0.8f);
            if (u >= 1f) Land();
        }

        static float Smooth(float u) => u * u * (3f - 2f * u);
        static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float u) => Vector2.Lerp(Vector2.Lerp(a, b, u), Vector2.Lerp(b, c, u), u);

        void Land()
        {
            if (Done) return;
            Done = true;
            if (body != null) { body.bodyType = oldType; body.linearVelocity = Vector2.zero; }
            if (ai != null) ai.externalControl = false;
            onLand?.Invoke();
            Destroy(this);
        }

        void OnDestroy()
        {
            if (line != null) Destroy(line.gameObject);
            if (!Done && started)
            {
                if (body != null) body.bodyType = oldType;
                if (ai != null) ai.externalControl = false;
            }
        }
    }
}
