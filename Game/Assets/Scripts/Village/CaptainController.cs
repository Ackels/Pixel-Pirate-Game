using UnityEngine;
using UnityEngine.InputSystem;

namespace Pixel.Village
{
    /// <summary>
    /// Movimento do capitão na vila (visão de cima). WASD / setas / analógico esquerdo.
    /// Anima trocando sprites por direção (baixo, cima, esquerda, direita).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class CaptainController : MonoBehaviour
    {
        [Header("Movimento")]
        [Tooltip("Velocidade em unidades por segundo (1 unidade = 16 px).")]
        public float moveSpeed = 4.5f;

        [Header("Animação (quadro 0 = parado; todos os quadros se alternam ao andar)")]
        public Sprite[] down;
        public Sprite[] up;
        public Sprite[] left;
        public Sprite[] right;
        public float framesPerSecond = 8f;

        Rigidbody2D body;
        SpriteRenderer spriteRenderer;
        InputAction moveAction;
        Vector2 input;
        Sprite[] currentSet;
        float animTimer;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            currentSet = down;

            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick");
        }

        void OnEnable() => moveAction.Enable();
        void OnDisable() => moveAction.Disable();
        void OnDestroy() => moveAction.Dispose();

        void Update()
        {
            input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            Animate();
        }

        void FixedUpdate()
        {
            body.linearVelocity = input * moveSpeed;
        }

        void Animate()
        {
            if (input.sqrMagnitude > 0.01f)
            {
                // Escolhe a direção dominante para o sprite.
                if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
                    currentSet = input.x < 0 ? left : right;
                else
                    currentSet = input.y < 0 ? down : up;

                animTimer += Time.deltaTime * framesPerSecond;
                SetFrame(currentSet != null && currentSet.Length > 0 ? (int)animTimer % currentSet.Length : 0);
            }
            else
            {
                animTimer = 0f;
                SetFrame(0);
            }
        }

        void SetFrame(int i)
        {
            if (currentSet == null || currentSet.Length == 0 || spriteRenderer == null) return;
            spriteRenderer.sprite = currentSet[Mathf.Min(i, currentSet.Length - 1)];
        }
    }
}
