using System.Collections;
using System.Collections.Generic;
using Pixel.Village;
using UnityEngine;

namespace Pixel.Combat
{
    /// <summary>
    /// Capitão Lendário (chefe da fortaleza). Por cima da luta normal (CombatAI + Combatant) ele tem:
    /// - Pancada no chão: pula e cai, ferindo e empurrando todo mundo do bando em volta (avisa antes).
    /// - Reforços: com 2/3 e 1/3 da vida chama piratas de dentro da fortaleza.
    /// - Fúria: abaixo de 1/3 golpeia e anda mais rápido, e a pancada vem mais vezes.
    /// A barra de vida grande no topo da tela (SeaHUD) lê o <see cref="Active"/>.
    /// </summary>
    public class LegendBoss : MonoBehaviour
    {
        public static LegendBoss Active { get; private set; }

        public string title = "Capitão Lendário";
        /// <summary>Chamado com o número da fase (1 ou 2) para trazer reforços.</summary>
        public System.Action<int> reinforce;
        public float slamRadius = 2.7f;
        public Vector2Int slamDamage = new Vector2Int(12, 18);

        Combatant c;
        CombatAI ai;
        IdleActivity idle;
        Rigidbody2D body;
        float slamTimer = 5f, baseCooldown, baseSpeed;
        int phase;
        bool slamming, announced;

        public bool IsAwake => c != null && !c.IsDown && (idle == null || !idle.enabled);
        public bool Enraged => phase >= 2;
        public float HpFraction => c != null && c.maxHp > 0 ? (float)c.hp / c.maxHp : 0f;
        public Combatant Fighter => c;

        void Start()
        {
            c = GetComponent<Combatant>();
            ai = GetComponent<CombatAI>();
            idle = GetComponent<IdleActivity>();
            body = GetComponent<Rigidbody2D>();
            baseCooldown = c.attackCooldown;
            baseSpeed = ai != null ? ai.moveSpeed : 3f;
            Active = this;
        }

        void OnDestroy() { if (Active == this) Active = null; }

        void Update()
        {
            if (c == null || c.IsDown) return;
            if (!IsAwake)
            {
                // Os guardas caíram: ele levanta do trono
                if (idle != null && Time.frameCount % 15 == 0 && !AnyAlly()) idle.WakeUp();
                return;
            }
            if (!announced)
            {
                announced = true;
                SpeechBubble.Say(transform, "Quem ousa invadir a minha fortaleza?!", 3f);
                AudioManager.Play(Sfx.Thunder, 0.6f);
            }
            float f = HpFraction;
            if (phase == 0 && f < 0.66f) { phase = 1; Call("Rapazes! Acabem com eles!"); }
            if (phase == 1 && f < 0.33f)
            {
                phase = 2;
                Call("Chega de brincadeira!");
                c.attackCooldown = baseCooldown * 0.8f;
                if (ai != null) ai.moveSpeed = baseSpeed * 1.2f;
                foreach (var sr in GetComponentsInChildren<SpriteRenderer>()) if (sr.name != "BarraDeVida" && sr.name != "Preenchimento") sr.color = new Color(1f, 0.75f, 0.72f);
                c.CacheRenderers();
            }

            if (slamming) return;
            slamTimer -= Time.deltaTime;
            if (slamTimer > 0f || !AnyoneNear(slamRadius)) return;
            slamTimer = Enraged ? Random.Range(5f, 7f) : Random.Range(7f, 10f);
            StartCoroutine(Slam());
        }

        void Call(string line)
        {
            SpeechBubble.Say(transform, line, 2.5f);
            reinforce?.Invoke(phase);
        }

        bool AnyAlly()
        {
            foreach (var o in Combatant.All)
                if (o != null && o != c && o.team == c.team && !o.IsDown) return true;
            return false;
        }

        bool AnyoneNear(float r)
        {
            foreach (var o in Combatant.All)
                if (o != null && o.team != c.team && !o.IsDown && Vector2.Distance(o.transform.position, transform.position) < r) return true;
            return false;
        }

        /// <summary>Pula, fica um instante no ar (dá para fugir) e cai com tudo.</summary>
        IEnumerator Slam()
        {
            slamming = true;
            if (ai != null) ai.externalControl = true;
            SpeechBubble.Say(transform, Enraged ? "MORRAM!" : "Sintam o peso do mar!", 1.5f);
            if (body != null && body.bodyType == RigidbodyType2D.Dynamic) body.linearVelocity = new Vector2(0f, 9f);
            yield return new WaitForSeconds(0.25f);
            // Espera cair de volta no chão
            float t = 0f;
            while (t < 1.2f && body != null && (body.linearVelocity.y > 0.05f || t < 0.35f)) { t += Time.deltaTime; yield return null; }
            while (t < 1.4f && body != null && Mathf.Abs(body.linearVelocity.y) > 0.05f) { t += Time.deltaTime; yield return null; }
            if (c == null || c.IsDown) { slamming = false; yield break; }
            Vector2 p = transform.position;
            for (int i = -2; i <= 2; i++) Fx.Spawn(Fx.Puff, p + new Vector2(i * 0.7f, 0.15f), 0.6f, 2.4f);
            Blast(p + Vector2.up * 0.4f, c.team == Team.Enemy ? Team.Player : Team.Enemy, slamRadius, slamDamage, 7f);
            AudioManager.PlayVaried(Sfx.Boom, 0.9f, 0.1f);
            Camera.main?.GetComponent<SeaCamera>()?.Shake(0.4f);
            if (ai != null) ai.externalControl = false;
            slamming = false;
        }

        /// <summary>Explosão/pancada: fere e empurra quem do time "victims" estiver no raio.</summary>
        public static void Blast(Vector2 point, Team victims, float radius, Vector2Int damage, float knock)
        {
            foreach (var o in new List<Combatant>(Combatant.All))
            {
                if (o == null || o.team != victims || o.IsDown) continue;
                Vector2 d = (Vector2)o.transform.position + Vector2.up * 0.5f - point;
                if (d.magnitude > radius) continue;
                o.TakeDamage(Random.Range(damage.x, damage.y + 1), null);
                var rb = o.GetComponent<Rigidbody2D>();
                if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
                    rb.linearVelocity = new Vector2(Mathf.Sign(d.x == 0 ? 1 : d.x) * knock, knock * 0.9f);
            }
        }
    }

    /// <summary>
    /// Canhão da fortaleza (fica no chão, sem andar): atira em arco no bando que se aproxima pela esquerda.
    /// Para de atirar em quem já está colado nele. Dá para destruir no braço (é um Combatant inimigo).
    /// </summary>
    public class FortCannon : MonoBehaviour
    {
        public float range = 17f, minRange = 2.2f;
        public Vector2 muzzle = new Vector2(-0.55f, 0.45f);
        public Vector2Int damage = new Vector2Int(8, 12);
        Combatant c;
        float reload;

        void Start()
        {
            c = GetComponent<Combatant>();
            reload = Random.Range(1.5f, 3f);
            c.Downed += _ =>
            {
                Fx.Spawn(Fx.Boom, transform.position + Vector3.up * 0.4f, 0.5f, 2.5f);
                Fx.Splinters((Vector2)transform.position + Vector2.up * 0.4f, 6);
                AudioManager.PlayVaried(Sfx.Boom, 0.8f);
            };
        }

        void Update()
        {
            if (c == null || c.IsDown) return;
            reload -= Time.deltaTime;
            if (reload > 0f) return;
            Combatant target = null;
            float best = float.MaxValue;
            foreach (var o in Combatant.All)
            {
                if (o == null || o.team == c.team || o.IsDown) continue;
                float dx = transform.position.x - o.transform.position.x;   // só quem está à esquerda
                if (dx < minRange || dx > range || dx >= best) continue;
                best = dx;
                target = o;
            }
            if (target == null) { reload = 0.5f; return; }
            reload = Random.Range(4f, 5.5f);
            Vector2 from = (Vector2)transform.position + muzzle;
            bool hit = Random.value < 0.7f;
            Vector2 to = (Vector2)target.transform.position + new Vector2(hit ? 0f : Random.Range(-2.5f, 2.5f), 0.4f);
            var dmg = damage;
            Cannonball.Fire(from, to, true, () =>
            {
                Fx.Spawn(Fx.Boom, to, 0.45f, 2f);
                Blast(to, Team.Player, 1.3f, dmg);
                Camera.main?.GetComponent<SeaCamera>()?.Shake(0.25f);
            }, false);
        }

        static void Blast(Vector2 p, Team t, float r, Vector2Int d) => LegendBoss.Blast(p, t, r, d, 4.5f);
    }
}
