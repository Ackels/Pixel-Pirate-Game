using Pixel.Village;
using UnityEngine;

namespace Pixel.Combat
{
    /// <summary>
    /// Boneco na visão lateral usando o visual (CharacterLook) do personagem — o mesmo da vila,
    /// só que com os quadros de perfil (esquerda/direita). Mostra o quadro de ataque ao golpear.
    /// </summary>
    public class LookFigure : MonoBehaviour, IPose
    {
        CharacterLook look;
        SpriteRenderer sr;
        Combatant combatant;
        float anim, attackTimer;
        bool facingRight = true;

        public void Init(CharacterLook l, int order)
        {
            look = l;
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            combatant = GetComponent<Combatant>();
            Pose(false, true, true);
        }

        void Start()
        {
            // O Combatant costuma ser adicionado depois do boneco
            if (combatant == null) combatant = GetComponent<Combatant>();
            if (combatant != null) combatant.Attacked += OnAttack;
        }

        void OnDestroy()
        {
            if (combatant != null) combatant.Attacked -= OnAttack;
        }

        void OnAttack() => attackTimer = 0.18f;

        void Update()
        {
            if (attackTimer > 0f) attackTimer -= Time.deltaTime;
        }

        public void Pose(bool moving, bool grounded, bool right)
        {
            if (look == null || sr == null) return;
            facingRight = right;
            var dir = right ? Vector2.right : Vector2.left;
            if (attackTimer > 0f) { sr.sprite = look.Attack(dir); return; }
            var set = look.Walk(dir);
            if (!grounded) sr.sprite = set[Mathf.Min(1, set.Length - 1)];
            else if (moving) { anim += Time.deltaTime * 8f; sr.sprite = CharacterLook.Frame(set, anim); }
            else { anim = 0f; sr.sprite = set[0]; }
        }
    }
}
