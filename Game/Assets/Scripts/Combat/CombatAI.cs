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
        [Tooltip("Vida a bordo (CrewLife) está mexendo no boneco (subindo escada, dormindo): a IA não anda.")]
        public bool externalControl;
        /// <summary>Tem alguém pra brigar agora (aí a vida a bordo para).</summary>
        public bool HasTarget => target != null && !target.IsDown;
        /// <summary>Chegou (na horizontal) no lugar para onde está indo.</summary>
        public bool AtHome => Mathf.Abs(transform.position.x - homeX) <= 0.6f;

        // ---------- Ordens do jogador (arrastar a espada do tripulante, como no Pixel Piracy) ----------
        /// <summary>Atacar este inimigo, onde ele estiver.</summary>
        public Combatant orderTarget;
        /// <summary>Ir até um lugar (e ficar lá brigando com quem aparecer).</summary>
        public bool moveOrder;
        public Vector2 orderPoint;
        public bool HasOrder => (orderTarget != null && !orderTarget.IsDown) || moveOrder;

        public void OrderAttack(Combatant t) { orderTarget = t; moveOrder = false; target = t; crossCheck = 0f; }
        public void OrderMove(Vector2 p) { orderTarget = null; moveOrder = true; orderPoint = p; target = null; crossCheck = 0f; }
        public void ClearOrders() { orderTarget = null; moveOrder = false; }
        float crossCheck;

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
            if (me.IsDown || externalControl) return;
            if (orderTarget != null && orderTarget.IsDown) orderTarget = null;

            // Ordem para o outro navio: atravessa na corda (se a abordagem já começou ou pode começar agora)
            crossCheck -= Time.deltaTime;
            if (HasOrder && crossCheck <= 0f)
            {
                crossCheck = 0.3f;
                Vector2 dest = orderTarget != null ? (Vector2)orderTarget.transform.position : orderPoint;
                var dir = EncounterDirector.Instance;
                if (dir != null && dir.NeedsCrossing(transform.position, dest))
                {
                    if (!dir.TryCross(this, dest)) ClearOrders();
                    moveX = 0f;
                    return;
                }
            }

            think -= Time.deltaTime;
            if (orderTarget != null) target = orderTarget;
            else if (think <= 0f || target == null || target.IsDown)
            {
                think = 0.25f;
                target = moveOrder ? null : PickTarget();
            }

            bool grounded = Grounded();
            moveX = 0f;
            if (moveOrder)
            {
                // Indo para o lugar mandado; chegando, fica lá (vira o "canto" dele)
                float dx = orderPoint.x - transform.position.x;
                if (Mathf.Abs(dx) > 0.3f) { moveX = Mathf.Sign(dx); me.FacingRight = moveX > 0; }
                else { moveOrder = false; homeX = orderPoint.x; }
            }
            else if (target != null)
            {
                Vector2 d = target.transform.position - transform.position;
                me.FacingRight = d.x >= 0;
                if (Mathf.Abs(d.x) <= me.attackRange * 0.85f && Mathf.Abs(d.y) < 1.2f) me.TryAttack();
                else moveX = Mathf.Sign(d.x);
            }
            else if (Mathf.Abs(transform.position.x - homeX) > 0.25f)
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
            if (me.IsDown || externalControl) return;
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
