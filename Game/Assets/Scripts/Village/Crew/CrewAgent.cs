using UnityEngine;

namespace Pixel.Village
{
    /// <summary>
    /// O boneco de um tripulante andando pela vila, com o visual (CharacterLook) dele.
    /// Vai até o local de trabalho; sem trabalho, fica perto de casa ou da praça.
    /// </summary>
    public class CrewAgent : MonoBehaviour
    {
        [System.NonSerialized] public CrewMember member;
        public float speed = 2.2f;

        /// <summary>Chegou no trabalho e está produzindo.</summary>
        public bool AtWork { get; private set; }

        SpriteRenderer sr;
        CharacterLook look;
        Vector2 wanderTarget;
        float wanderTimer, anim;
        bool leaving;
        Vector2 exitPoint;

        public void Init(CrewMember m, CharacterLook characterLook)
        {
            member = m;
            look = characterLook;
            m.agent = this;
            sr = CharacterSprite.Create(gameObject);   // num filho, com desempate da ordem de desenho
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            wanderTarget = transform.position;
            if (look != null) sr.sprite = look.Idle(Vector2.down);
        }

        /// <summary>Sai da vila andando até as docas e some.</summary>
        public void Leave(Vector2 dock)
        {
            leaving = true;
            exitPoint = dock;
            AtWork = false;
        }

        void Update()
        {
            if (member == null) return;
            Vector2 target;
            if (leaving) target = exitPoint;
            else if (member.workplace != null) target = WorkSpot();
            else target = Wander();

            Vector2 pos = transform.position;
            Vector2 delta = target - pos;
            float step = speed * Time.deltaTime;
            if (delta.magnitude <= step)
            {
                transform.position = target;
                if (leaving) { Destroy(gameObject); return; }
                AtWork = member.workplace != null;
                if (look != null) sr.sprite = look.Idle(AtWork ? Vector2.up : Vector2.down);
                anim = 0f;
                return;
            }

            AtWork = false;
            transform.position = pos + delta.normalized * step;
            anim += Time.deltaTime * 7f;
            if (look != null) sr.sprite = CharacterLook.Frame(look.Walk(delta), anim);
        }

        Vector2 WorkSpot()
        {
            var b = member.workplace;
            int slot = b.Workers.IndexOf(member);
            float offset = (slot - (b.Workers.Count - 1) / 2f) * 0.8f;
            return b.Door + new Vector2(offset, 0);
        }

        Vector2 Wander()
        {
            wanderTimer -= Time.deltaTime;
            if (wanderTimer <= 0f || ((Vector2)transform.position - wanderTarget).sqrMagnitude < 0.01f && wanderTimer <= 0f)
            {
                wanderTimer = Random.Range(3f, 7f);
                Vector2 center = member.home != null ? member.home.Door : CrewManager.Instance.idleArea;
                wanderTarget = center + Random.insideUnitCircle * 2.5f;
            }
            return wanderTarget;
        }
    }
}
