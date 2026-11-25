using UnityEngine;

namespace Pixel.Combat
{
    /// <summary>
    /// Arma na mão do boneco na visão lateral: fica inclinada para a frente e gira num golpe quando ele ataca.
    /// Some quando o boneco está caído, deitado ou fazendo outra coisa (comendo, dormindo...).
    /// O desenho é o ícone da arma (cabo embaixo, pivô no cabo).
    /// </summary>
    public class HandWeapon : MonoBehaviour
    {
        const float SwingTime = 0.22f;
        static readonly Vector2 Hand = new Vector2(0.24f, 0.34f);   // em "bonecos" (1 = altura do desenho)

        SpriteRenderer weapon, body;
        Combatant combatant;
        IdleActivity idle;
        float swing;

        /// <summary>Põe (ou troca, ou tira com null) a arma na mão.</summary>
        public static HandWeapon Set(GameObject go, Sprite sprite)
        {
            var h = go.GetComponent<HandWeapon>();
            if (h == null)
            {
                if (sprite == null) return null;
                h = go.AddComponent<HandWeapon>();
            }
            h.weapon.sprite = sprite;
            return h;
        }

        void Awake()
        {
            weapon = new GameObject("Arma").AddComponent<SpriteRenderer>();
            weapon.transform.SetParent(transform, false);
            weapon.transform.localScale = Vector3.one * 0.62f;
            combatant = GetComponent<Combatant>();
            if (combatant != null) combatant.Attacked += OnAttack;
        }

        void OnDestroy()
        {
            if (combatant != null) combatant.Attacked -= OnAttack;
        }

        void OnAttack() => swing = 1f;

        void LateUpdate()
        {
            if (body == null) body = GetComponent<SpriteRenderer>();
            if (idle == null) idle = GetComponent<IdleActivity>();
            bool lying = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.z, 0f)) > 1f;
            bool show = weapon.sprite != null && !lying && (combatant == null || !combatant.IsDown)
                        && (idle == null || !idle.enabled) && (body == null || body.enabled);
            if (weapon.enabled != show) weapon.enabled = show;
            if (!show) return;

            bool right = combatant == null || combatant.FacingRight;
            float dir = right ? 1f : -1f;
            if (swing > 0f) swing = Mathf.Max(0f, swing - Time.deltaTime / SwingTime);
            // Parada: inclinada para a frente. Golpe: vem de trás (por cima da cabeça) e desce na frente.
            float t = 1f - swing;
            float angle = swing > 0f ? Mathf.Lerp(80f, -115f, 1f - (1f - t) * (1f - t)) : -28f;
            weapon.transform.localPosition = new Vector3(Hand.x * dir, Hand.y, 0f);
            weapon.transform.localRotation = Quaternion.Euler(0, 0, angle * dir);
            weapon.flipX = !right;
            if (body != null) weapon.sortingOrder = body.sortingOrder + 1;
        }
    }
}
