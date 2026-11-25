using Pixel.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Pixel.Ship
{
    /// <summary>
    /// Capitão na visão lateral (no convés): A/D ou setas para andar, Espaço/W para pular,
    /// F/J ou clique para golpear com a espada. Clique num inimigo: o capitão vai sozinho até ele
    /// (na corda, se estiver no outro navio) e ataca até derrubá-lo; andar com A/D cancela. Na escada, W/S sobem e descem; em cima de um piso vazado,
    /// S desce para o andar de baixo.
    /// Usa os mesmos desenhos de perfil do capitão da vila.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class SideCaptainController : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public float jumpSpeed = 10f;
        public Sprite[] left, right;
        public Sprite attackLeft, attackRight;

        Rigidbody2D body;
        SpriteRenderer sr;
        Collider2D col;
        InputAction move, jump, attack;
        Combatant combatant;
        float inputX, inputY, anim, attackShow, gravity;
        bool facingRight = true, climbing;
        readonly Collider2D[] hits = new Collider2D[8];
        readonly System.Collections.Generic.List<(Collider2D col, float until)> ignored = new System.Collections.Generic.List<(Collider2D, float)>();

        /// <summary>Inimigo que o capitão está perseguindo (clicado com o mouse).</summary>
        public Combatant AutoTarget { get; private set; }
        float autoMoveX, stuck;

        public void ClearAutoTarget() { AutoTarget = null; autoMoveX = 0f; }

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
            gravity = body.gravityScale;
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
            // Pendurado na corda: o balanço controla o corpo
            if (RopeSwing.IsBusy(gameObject)) { inputX = 0f; autoMoveX = 0f; return; }
            inputX = Mathf.Clamp(move.ReadValue<float>(), -1f, 1f);
            var kb = Keyboard.current;
            bool upKey = kb != null && (kb.wKey.isPressed || kb.upArrowKey.isPressed);
            bool downKey = kb != null && (kb.sKey.isPressed || kb.downArrowKey.isPressed);
            inputY = upKey ? 1f : downKey ? -1f : 0f;
            bool onLadder = Overlapping<ShipLadder>((Vector2)transform.position + Vector2.up * 0.45f, new Vector2(0.3f, 0.8f));

            // Escada: W/S agarram; Espaço solta pulando
            if (!climbing && onLadder && (upKey || (downKey && !OnPlatformOnly()))) climbing = true;
            if (climbing && (!onLadder || (kb != null && kb.spaceKey.wasPressedThisFrame))) climbing = false;
            body.gravityScale = climbing ? 0f : gravity;
            if (climbing && downKey) DropThroughPlatforms();

            // Piso vazado: S desce para o andar de baixo
            if (!climbing && downKey && kb != null && (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)) DropThroughPlatforms();
            for (int i = ignored.Count - 1; i >= 0; i--)
                if (Time.time > ignored[i].until) { if (ignored[i].col != null) Physics2D.IgnoreCollision(col, ignored[i].col, false); ignored.RemoveAt(i); }

            bool jumpPressed = kb != null && kb.spaceKey.wasPressedThisFrame || (!onLadder && jump.WasPressedThisFrame());
            if (AutoTarget != null && (Mathf.Abs(inputX) > 0.01f || jumpPressed || climbing)) ClearAutoTarget();   // o jogador assumiu
            if (!climbing && jumpPressed && Grounded())
                body.linearVelocity = new Vector2(body.linearVelocity.x, jumpSpeed);

            if (Mathf.Abs(inputX) > 0.01f) facingRight = inputX > 0;
            if (combatant != null)
            {
                bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                bool editing = SeaController.Instance != null && SeaController.Instance.Mode == SeaMode.Editing;
                var mouse = Mouse.current;
                bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
                if (attack.WasPressedThisFrame() && !overUI && !editing && !CrewCommander.Dragging)
                {
                    Combatant enemy = null, ally = null;
                    if (clicked && Camera.main != null)
                    {
                        Vector2 w = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
                        enemy = Orders.At(w, null, true);
                        ally = enemy == null ? Orders.At(w, Team.Player, false) : null;
                    }
                    if (enemy != null)
                    {
                        AutoTarget = enemy;
                        Orders.Flash((Vector2)transform.position + Vector2.up * 0.8f, (Vector2)enemy.transform.position + Vector2.up * 0.5f, Orders.AttackColor);
                    }
                    else if (ally == null && clicked && Camera.main != null && TryHookTo(Camera.main.ScreenToWorldPoint(mouse.position.ReadValue()))) { }
                    else if (ally == null && !CrewCommander.Paused && combatant.TryAttack()) attackShow = 0.18f;
                }
                if (AutoTarget != null) ChaseTarget();
                combatant.FacingRight = facingRight;
            }
            var set = facingRight ? right : left;
            if (attackShow > 0f) attackShow -= Time.deltaTime;
            var atk = facingRight ? attackRight : attackLeft;
            if (attackShow > 0f && atk != null && sr != null) sr.sprite = atk;
            else if (set != null && set.Length > 0 && sr != null)
            {
                if (!Grounded()) sr.sprite = set[Mathf.Min(1, set.Length - 1)];
                else if (Mathf.Abs(inputX) > 0.01f || Mathf.Abs(autoMoveX) > 0.01f) { anim += Time.deltaTime * 9f; sr.sprite = set[(int)anim % set.Length]; }
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
            if (RopeSwing.IsBusy(gameObject)) return;
            float x = AutoTarget != null ? autoMoveX : inputX;
            if (climbing) body.linearVelocity = new Vector2(inputX * moveSpeed * 0.5f, inputY * 4f);
            else body.linearVelocity = new Vector2(x * moveSpeed, body.linearVelocity.y);
        }

        /// <summary>Clicou no outro navio (o inimigo ou, estando nele, o seu): o capitão lança o gancho até lá.</summary>
        bool TryHookTo(Vector2 w)
        {
            var dir = EncounterDirector.Instance;
            if (dir == null || !dir.NeedsCrossing(transform.position, w)) return false;
            ClearAutoTarget();
            dir.TryCross(this, w);
            return true;
        }

        /// <summary>Vai atrás do inimigo clicado: anda, pula degraus, atravessa na corda e golpeia quando alcança.</summary>
        void ChaseTarget()
        {
            if (AutoTarget == null || AutoTarget.IsDown || combatant.IsDown) { ClearAutoTarget(); return; }
            Vector2 me = transform.position, d = (Vector2)AutoTarget.transform.position - me;
            facingRight = d.x >= 0;
            var dir = EncounterDirector.Instance;
            if (dir != null && dir.NeedsCrossing(me, AutoTarget.transform.position))
            {
                autoMoveX = 0f;
                if (!dir.TryCross(this, AutoTarget.transform.position)) ClearAutoTarget();
                return;
            }
            if (Mathf.Abs(d.x) <= combatant.attackRange * 0.85f && Mathf.Abs(d.y) < 1.2f)
            {
                autoMoveX = 0f;
                if (!CrewCommander.Paused && combatant.TryAttack()) attackShow = 0.18f;
                return;
            }
            autoMoveX = Mathf.Sign(d.x);
            bool grounded = Grounded();
            if (grounded && Mathf.Abs(body.linearVelocity.x) < 0.3f) stuck += Time.deltaTime; else stuck = 0f;
            if (stuck > 0.25f && grounded) { body.linearVelocity = new Vector2(body.linearVelocity.x, jumpSpeed); stuck = 0f; }
        }

        bool Overlapping<T>(Vector2 center, Vector2 size) where T : Component
        {
            var filter = new ContactFilter2D { useTriggers = true };
            filter = ContactFilter2D.noFilter;
            int n = Physics2D.OverlapBox(center, size, 0f, filter, hits);
            for (int i = 0; i < n; i++) if (hits[i] != null && hits[i].GetComponent<T>() != null) return true;
            return false;
        }

        /// <summary>Em pé só em piso vazado (sem casco embaixo): aí S desce em vez de agarrar a escada.</summary>
        bool OnPlatformOnly()
        {
            var filter = new ContactFilter2D();
            filter = ContactFilter2D.noFilter;
            int n = Physics2D.OverlapBox((Vector2)transform.position + Vector2.down * 0.05f, new Vector2(0.45f, 0.1f), 0f, filter, hits);
            bool platform = false;
            for (int i = 0; i < n; i++)
            {
                if (hits[i] == col || hits[i].isTrigger) continue;
                if (hits[i].GetComponent<ShipPlatform>() != null) platform = true; else return false;
            }
            return platform;
        }

        /// <summary>Atravessa os pisos vazados logo abaixo dos pés por um instante.</summary>
        void DropThroughPlatforms()
        {
            var filter = new ContactFilter2D();
            filter = ContactFilter2D.noFilter;
            int n = Physics2D.OverlapBox((Vector2)transform.position + Vector2.down * 0.1f, new Vector2(0.6f, 0.3f), 0f, filter, hits);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h == null || h.GetComponent<ShipPlatform>() == null) continue;
                Physics2D.IgnoreCollision(col, h, true);
                ignored.Add((h, Time.time + 0.45f));
            }
        }
    }
}
