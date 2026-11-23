using UnityEngine;
using UnityEngine.Rendering;

namespace Pixel.Village
{
    /// <summary>
    /// O boneco de um tripulante andando pela vila. Montado em camadas
    /// (pele, roupa, cabelo coloridos + base) para cada pirata ter um visual diferente.
    /// Vai até o local de trabalho; sem trabalho, fica perto de casa ou da praça.
    /// </summary>
    public class CrewAgent : MonoBehaviour
    {
        [System.NonSerialized] public CrewMember member;
        public float speed = 2.2f;

        /// <summary>Chegou no trabalho e está produzindo.</summary>
        public bool AtWork { get; private set; }

        SpriteRenderer skin, outfit, hair, baseLayer;
        CrewManager.LayerSprites sprites;
        Vector2 wanderTarget;
        float wanderTimer, anim;
        bool leaving;
        Vector2 exitPoint;

        public void Init(CrewMember m, CrewManager.LayerSprites layerSprites)
        {
            member = m;
            sprites = layerSprites;
            m.agent = this;
            gameObject.AddComponent<SortingGroup>();
            skin = Layer("Pele", 0, m.skin);
            outfit = Layer("Roupa", 1, m.outfit);
            hair = Layer("Cabelo", 2, m.hair);
            baseLayer = Layer("Base", 3, Color.white);
            wanderTarget = transform.position;
            SetFrame(sprites.down, 0);
        }

        SpriteRenderer Layer(string n, int order, Color c)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            sr.color = c;
            return sr;
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
                SetFrame(AtWork ? sprites.up : sprites.down, 0);
                return;
            }

            AtWork = false;
            transform.position = pos + delta.normalized * step;
            var set = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? (delta.x < 0 ? sprites.left : sprites.right) : (delta.y < 0 ? sprites.down : sprites.up);
            anim += Time.deltaTime * 7f;
            SetFrame(set, 1 + (int)anim % 2);
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

        void SetFrame(CrewManager.DirSprites set, int frame)
        {
            if (set == null) return;
            skin.sprite = set.skin[frame];
            outfit.sprite = set.outfit[frame];
            hair.sprite = set.hair[frame];
            baseLayer.sprite = set.baseLayer[frame];
        }
    }
}
