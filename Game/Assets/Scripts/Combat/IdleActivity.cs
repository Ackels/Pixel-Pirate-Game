using UnityEngine;

namespace Pixel.Combat
{
    /// <summary>
    /// O que um inimigo (ou morador) da ilha faz enquanto ninguém chega perto: comer, beber, dormir ou conversar.
    /// Inimigos acordam quando alguém do bando chega perto ou quando apanham, avisam os vizinhos e vão lutar.
    /// Moradores amigos (friendly) nunca acordam: só ficam ali dando vida à construção.
    /// </summary>
    public class IdleActivity : MonoBehaviour
    {
        public enum Kind { Eat, Drink, Sleep, Chat }

        public Kind kind;
        public bool friendly;
        public bool facingRight;
        public Sprite prop;
        public float wakeRadius = 5f;

        Combatant me;
        CombatAI ai;
        Rigidbody2D body;
        IPose pose;
        SpriteRenderer propSr;
        float t, talk, startHp;
        bool awake;
        Vector2 restPos;

        static readonly string[] EatLines = { "Nham!", "Que peixe bom...", "Mais um pedaço!", "Hmm..." };
        static readonly string[] DrinkLines = { "Hic!", "Mais rum!", "Saúde!", "Iô-hô-hô..." };
        static readonly string[] ChatLines = { "Ouviu falar do tesouro?", "O mar está calmo hoje.", "Ha ha ha!", "Que história, hein?", "Mais uma rodada!" };

        void Start()
        {
            me = GetComponent<Combatant>();
            ai = GetComponent<CombatAI>();
            body = GetComponent<Rigidbody2D>();
            pose = GetComponent<IPose>();
            if (me != null) { startHp = me.hp; me.FacingRight = facingRight; }
            if (ai != null) ai.externalControl = true;
            restPos = transform.position;
            if (prop != null)
            {
                propSr = new GameObject("Objeto").AddComponent<SpriteRenderer>();
                propSr.transform.SetParent(transform, false);
                propSr.sprite = prop;
                propSr.sortingOrder = 11;
                propSr.transform.localPosition = new Vector2(facingRight ? 0.32f : -0.32f, 0.42f);
                propSr.flipX = !facingRight;
            }
            if (pose == null) pose = GetComponent<IPose>();
            if (kind == Kind.Sleep && body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.bodyType = RigidbodyType2D.Kinematic;
                body.position = restPos + new Vector2(-0.5f, 0.12f);
                transform.position = body.position;
                body.rotation = -90f;   // deitado no chão
            }
            t = Random.value * 3f;
            talk = Random.Range(2f, 6f);
        }

        void Update()
        {
            if (awake) return;
            if (me != null && me.IsDown) { Wake(false); return; }
            if (!friendly && me != null && (me.hp < startHp || PlayerNear())) { WakeAround(); return; }

            t += Time.deltaTime;
            talk -= Time.deltaTime;
            pose?.Pose(false, true, facingRight);
            if (me != null) me.FacingRight = facingRight;
            switch (kind)
            {
                case Kind.Eat:
                    if (propSr != null) propSr.transform.localPosition = new Vector2(facingRight ? 0.32f : -0.32f, 0.42f + Mathf.Abs(Mathf.Sin(t * 4f)) * 0.12f);
                    if (talk <= 0f) { talk = Random.Range(4f, 8f); SpeechBubble.Say(transform, EatLines[Random.Range(0, EatLines.Length)], 1.6f); }
                    break;
                case Kind.Drink:
                    if (propSr != null)
                    {
                        float tilt = Mathf.Max(0f, Mathf.Sin(t * 1.6f)) * 70f;
                        propSr.transform.localRotation = Quaternion.Euler(0, 0, facingRight ? tilt : -tilt);
                        propSr.transform.localPosition = new Vector2(facingRight ? 0.28f : -0.28f, 0.45f + tilt / 70f * 0.12f);
                    }
                    if (talk <= 0f) { talk = Random.Range(4f, 8f); SpeechBubble.Say(transform, DrinkLines[Random.Range(0, DrinkLines.Length)], 1.6f); }
                    break;
                case Kind.Sleep:
                    if (talk <= 0f) { talk = 3f; SpeechBubble.Say(transform, "Zzz...", 2f, 0.9f); }
                    break;
                case Kind.Chat:
                    if (talk <= 0f) { talk = Random.Range(3f, 7f); SpeechBubble.Say(transform, ChatLines[Random.Range(0, ChatLines.Length)], 2.2f); }
                    break;
            }
        }

        bool PlayerNear()
        {
            foreach (var c in Combatant.All)
            {
                if (c == null || c.team != Team.Player || c.IsDown) continue;
                Vector2 d = c.transform.position - transform.position;
                if (Mathf.Abs(d.x) < wakeRadius && Mathf.Abs(d.y) < 2.5f) return true;
            }
            return false;
        }

        /// <summary>Acorda este e quem estiver perto (um grito acorda a taverna inteira).</summary>
        void WakeAround()
        {
            foreach (var other in FindObjectsByType<IdleActivity>())
                if (other != null && !other.friendly && !other.awake && Vector2.Distance(other.transform.position, transform.position) < 7f)
                    other.Wake(other == this);
        }

        void Wake(bool shout)
        {
            awake = true;
            if (propSr != null) Destroy(propSr.gameObject);
            if (body != null)
            {
                body.rotation = 0f;
                if (kind == Kind.Sleep) { body.position = restPos + Vector2.up * 0.1f; body.bodyType = RigidbodyType2D.Dynamic; }
            }
            if (ai != null) ai.externalControl = false;
            if (shout && me != null && !me.IsDown)
                SpeechBubble.Say(transform, kind == Kind.Sleep ? "Hã?! Invasores!" : "Invasores! Peguem eles!", 2f);
            enabled = false;
        }
    }
}
