using Pixel.Village;
using UnityEngine;

namespace Pixel.Combat
{
    /// <summary>
    /// Gancho de abordagem (como no Pixel Piracy): o personagem lança o gancho até o outro navio, a corda
    /// fica esticada e ele atravessa andando por cima dela. Enquanto isso, a IA e a física ficam em pausa.
    /// (O nome da classe ficou do tempo em que eles balançavam num cipó.)
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

        /// <summary>Faz alguém lançar o gancho e atravessar até <paramref name="target"/> (anchorPoint não é mais usado).</summary>
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
        float hookTime;
        Vector2 hand;
        SpriteRenderer hook;
        IPose pose;
        Combatant fighter;
        const float HookFlight = 0.3f, WalkSpeed = 5.5f;

        void Start()
        {
            body = GetComponent<Rigidbody2D>();
            ai = GetComponent<CombatAI>();
            pose = GetComponent<IPose>();
            fighter = GetComponent<Combatant>();
            line = MakeRope(null, "Gancho", 3, 0.08f);
            line.startColor = line.endColor = new Color32(0x5a, 0x3a, 0x22, 255);
            line.enabled = false;
        }

        void Update()
        {
            t += Time.deltaTime;
            if (t < 0f) return;
            if (!started)
            {
                // Para, mira e lança o gancho a partir de onde está agora
                started = true;
                if (ai != null) ai.externalControl = true;
                if (body != null)
                {
                    oldType = body.bodyType;
                    body.linearVelocity = Vector2.zero;
                    body.bodyType = RigidbodyType2D.Kinematic;
                }
                from = transform.position;
                hand = from + Vector2.up * 0.6f * Mathf.Max(1f, transform.lossyScale.y);
                duration = Mathf.Clamp(Vector2.Distance(from, to) / WalkSpeed, 0.5f, 3.5f);
                hook = new GameObject("PontaDoGancho").AddComponent<SpriteRenderer>();
                hook.sprite = Fx.White;
                hook.color = new Color(0.25f, 0.25f, 0.3f);
                hook.transform.localScale = Vector3.one * 0.16f;
                hook.sortingOrder = 8;
                if (fighter != null) fighter.FacingRight = to.x > from.x;
                AudioManager.PlayVaried(Sfx.Swing, 0.6f, 0.2f);
                t = 0f;
            }
            bool right = to.x > from.x;
            line.enabled = true;
            if (hookTime < HookFlight)
            {
                // 1) O gancho voa em arco até o convés do outro navio
                hookTime += Time.deltaTime;
                float k = Mathf.Clamp01(hookTime / HookFlight);
                var tip = Vector2.Lerp(hand, to, k) + Vector2.up * (Mathf.Sin(k * Mathf.PI) * 1.2f);
                line.SetPosition(0, hand);
                line.SetPosition(1, Vector2.Lerp(hand, tip, 0.5f) + Vector2.down * 0.2f * (1f - k));
                line.SetPosition(2, tip);
                hook.transform.position = tip;
                if (pose != null) pose.Pose(false, true, right);
                if (k >= 1f) { t = 0f; AudioManager.PlayVaried(Sfx.Wood, 0.4f, 0.2f); }
                return;
            }
            // 2) Corda esticada: anda por cima dela até o outro lado
            float u = Mathf.Clamp01(t / duration);
            var p = Vector2.Lerp(from, to, u);
            if (body != null) body.position = p;
            transform.position = p;
            line.SetPosition(0, from);
            line.SetPosition(1, p + Vector2.down * 0.06f);
            line.SetPosition(2, to);
            hook.transform.position = to;
            if (pose != null) pose.Pose(true, true, right);
            if (fighter != null) fighter.FacingRight = right;
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
            if (hook != null) Destroy(hook.gameObject);
            if (!Done && started)
            {
                if (body != null) body.bodyType = oldType;
                if (ai != null) ai.externalControl = false;
            }
        }
    }
}
