using Pixel.Ship;
using UnityEngine;

namespace Pixel.Combat
{
    /// <summary>
    /// Cérebro de quem não é o capitão: escolhe o adversário mais perto, anda até ele e golpeia.
    /// Em "defesa" só ataca quem invadiu o próprio navio; em "ataque" vai atrás de qualquer um.
    /// Se ficar travado num degrau, pula.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Combatant))]
    public class CombatAI : MonoBehaviour
    {
        public float moveSpeed = 3.6f;
        public float jumpSpeed = 10f;
        [Tooltip("Ataque = cruza para o outro navio. Defesa = fica no próprio.")]
        public bool charge;
        public ShipView homeShip;
        public float homeX;
        [Tooltip("Sem navio (ilha): área que este lutador defende e linha d'água.")]
        public float regionMin = -9999f, regionMax = 9999f, waterlineY = -9999f;

        Combatant me;
        Rigidbody2D body;
        IPose figure;
        Collider2D col;
        float moveX, stuck, think;
        Combatant target;
        readonly Collider2D[] hits = new Collider2D[4];

        void Awake()
        {
            me = GetComponent<Combatant>();
            body = GetComponent<Rigidbody2D>();
            figure = GetComponent<IPose>();
            col = GetComponent<Collider2D>();
        }

        bool Grounded()
        {
            var filter = new ContactFilter2D { useTriggers = false };
            int n = Physics2D.OverlapBox((Vector2)transform.position + Vector2.down * 0.05f, new Vector2(0.45f, 0.1f), 0f, filter, hits);
            for (int i = 0; i < n; i++) if (hits[i] != col && hits[i].attachedRigidbody != body && hits[i].GetComponent<Combatant>() == null) return true;
            return false;
        }

        void Update()
        {
            if (me.IsDown) return;
            think -= Time.deltaTime;
            if (think <= 0f || target == null || target.IsDown)
            {
                think = 0.25f;
                target = PickTarget();
            }

            bool grounded = Grounded();
            moveX = 0f;
            if (target != null)
            {
                Vector2 d = target.transform.position - transform.position;
                me.FacingRight = d.x >= 0;
                if (Mathf.Abs(d.x) <= me.attackRange * 0.85f && Mathf.Abs(d.y) < 1.2f) me.TryAttack();
                else moveX = Mathf.Sign(d.x);
            }
            else if (Mathf.Abs(transform.position.x - homeX) > 0.6f)
            {
                // Sem ninguém pra brigar: volta pro seu canto no próprio navio
                moveX = Mathf.Sign(homeX - transform.position.x);
                me.FacingRight = moveX > 0;
            }

            if (moveX != 0f && grounded && Mathf.Abs(body.linearVelocity.x) < 0.3f) stuck += Time.deltaTime;
            else stuck = 0f;
            if (stuck > 0.25f && grounded) { body.linearVelocity = new Vector2(body.linearVelocity.x, jumpSpeed); stuck = 0f; }

            if (figure != null) figure.Pose(moveX != 0f, grounded, me.FacingRight);

            // Caiu no mar
            float water = homeShip != null ? homeShip.WaterlineY : waterlineY;
            if (transform.position.y < water - 1.5f)
            {
                if (me.knockOutInsteadOfDeath && homeShip != null)
                {
                    var p = homeShip.SpawnPoint();
                    transform.position = p;
                    body.position = p;
                    body.linearVelocity = Vector2.zero;
                }
                else me.TakeDamage(9999, null);
            }
        }

        void FixedUpdate()
        {
            if (me.IsDown) return;
            body.linearVelocity = new Vector2(moveX * moveSpeed, body.linearVelocity.y);
        }

        bool Defends(float x) => homeShip != null ? homeShip.ContainsX(x) : x >= regionMin && x <= regionMax;

        Combatant PickTarget()
        {
            Combatant best = null;
            float bestD = float.MaxValue;
            foreach (var c in Combatant.All)
            {
                if (c.team == me.team || c.IsDown) continue;
                if (!charge && !Defends(c.transform.position.x)) continue;
                float dd = (c.transform.position - transform.position).sqrMagnitude;
                if (dd < bestD) { bestD = dd; best = c; }
            }
            return best;
        }
    }
}
