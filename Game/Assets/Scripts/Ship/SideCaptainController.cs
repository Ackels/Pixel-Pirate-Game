using Pixel.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Pixel.Ship
{
    /// <summary>
    /// Capitão na visão lateral (no convés): A/D ou setas para andar, Espaço/W para pular,
    /// F/J ou clique para golpear com a espada.
    /// Usa os mesmos desenhos de perfil do capitão da vila.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class SideCaptainController : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public float jumpSpeed = 10f;
        public Sprite[] left, right;

        Rigidbody2D body;
        SpriteRenderer sr;
        Collider2D col;
        InputAction move, jump, attack;
        Combatant combatant;
        float inputX, anim;
        bool facingRight = true;
        readonly Collider2D[] hits = new Collider2D[4];

        /// <summary>Disparado quando o capitão cai no mar (o HUD mostra um aviso).</summary>
        public event System.Action FellOverboard;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            sr = GetComponentInChildren<SpriteRenderer>();
            col = GetComponent<Collider2D>();
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick/x");
            jump = new InputAction("Jump", InputActionType.Button);
            jump.AddBinding("<Keyboard>/space");
            jump.AddBinding("<Keyboard>/w");
            jump.AddBinding("<Keyboard>/upArrow");
            jump.AddBinding("<Gamepad>/buttonSouth");
            attack = new InputAction("Attack", InputActionType.Button);
            attack.AddBinding("<Keyboard>/f");
            attack.AddBinding("<Keyboard>/j");
            attack.AddBinding("<Mouse>/leftButton");
            attack.AddBinding("<Gamepad>/buttonWest");
            combatant = GetComponent<Combatant>();
        }

        void OnEnable() { move.Enable(); jump.Enable(); attack.Enable(); }
        void OnDisable() { move.Disable(); jump.Disable(); attack.Disable(); }
        void OnDestroy() { move.Dispose(); jump.Dispose(); attack.Dispose(); }

        public void Teleport(Vector2 p)
        {
            transform.position = p;
            body.position = p;
            body.linearVelocity = Vector2.zero;
        }

        bool Grounded()
        {
            var filter = new ContactFilter2D { useTriggers = false };
            Vector2 feet = (Vector2)transform.position + Vector2.down * 0.05f;
            int n = Physics2D.OverlapBox(feet, new Vector2(0.45f, 0.1f), 0f, filter, hits);
            for (int i = 0; i < n; i++) if (hits[i] != col) return true;
            return false;
        }

        void Update()
        {
            inputX = Mathf.Clamp(move.ReadValue<float>(), -1f, 1f);
            if (jump.WasPressedThisFrame() && Grounded())
                body.linearVelocity = new Vector2(body.linearVelocity.x, jumpSpeed);

            if (Mathf.Abs(inputX) > 0.01f) facingRight = inputX > 0;
            if (combatant != null)
            {
                combatant.FacingRight = facingRight;
                bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                bool editing = SeaController.Instance != null && SeaController.Instance.Mode == SeaMode.Editing;
                if (attack.WasPressedThisFrame() && !overUI && !editing) combatant.TryAttack();
            }
            var set = facingRight ? right : left;
            if (set != null && set.Length >= 3 && sr != null)
            {
                if (!Grounded()) sr.sprite = set[1];
                else if (Mathf.Abs(inputX) > 0.01f) { anim += Time.deltaTime * 9f; sr.sprite = set[1 + (int)anim % 2]; }
                else { anim = 0f; sr.sprite = set[0]; }
            }

            var view = SeaController.Instance != null ? SeaController.Instance.shipView : null;
            if (view != null && transform.position.y < view.WaterlineY - 1.5f)
            {
                Teleport(view.SpawnPoint());
                FellOverboard?.Invoke();
            }
        }

        void FixedUpdate()
        {
            body.linearVelocity = new Vector2(inputX * moveSpeed, body.linearVelocity.y);
        }
    }
}
