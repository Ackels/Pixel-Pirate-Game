using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Pixel.Village
{
    /// <summary>
    /// Modo construção: escolha um prédio no HUD, uma "sombra" segue o mouse
    /// (verde = pode, vermelho = não pode). Clique esquerdo constrói,
    /// clique direito ou Esc cancela. Segure Shift para construir vários seguidos.
    /// </summary>
    public class BuildMode : MonoBehaviour
    {
        public BuildingDef Selected { get; private set; }
        public bool Demolishing { get; private set; }
        /// <summary>Mensagem de ajuda/erro para o HUD mostrar.</summary>
        public string Hint { get; private set; }
        public event Action SelectionChanged;

        static readonly Color ValidColor = new Color(0.4f, 1f, 0.4f, 0.35f);
        static readonly Color InvalidColor = new Color(1f, 0.3f, 0.3f, 0.35f);

        Camera cam;
        GameObject ghost;
        SpriteRenderer ghostSprite;
        SpriteRenderer ghostFootprint;
        Building hovered;

        void Awake()
        {
            cam = Camera.main;
            ghost = new GameObject("FantasmaDeConstrucao");
            ghostSprite = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            ghostSprite.transform.SetParent(ghost.transform, false);
            ghostSprite.sortingOrder = 1000;
            ghostFootprint = new GameObject("Area").AddComponent<SpriteRenderer>();
            ghostFootprint.transform.SetParent(ghost.transform, false);
            ghostFootprint.sprite = WhiteSprite();
            ghostFootprint.sortingOrder = 999;
            ghost.SetActive(false);
        }

        public void Select(BuildingDef def)
        {
            ClearHover();
            Demolishing = false;
            Selected = def;
            ghost.SetActive(def != null);
            if (def != null)
            {
                ghostSprite.sprite = def.sprite;
                ghostFootprint.transform.localScale = new Vector3(def.footprint.x, def.footprint.y, 1);
                ghostFootprint.transform.localPosition = new Vector3(0, def.footprint.y / 2f, 0);
                Hint = $"{def.displayName}: {def.description}";
            }
            else Hint = null;
            SelectionChanged?.Invoke();
        }

        public void StartDemolish()
        {
            Select(null);
            Demolishing = true;
            Hint = "Clique numa construção para demolir (devolve metade do custo).";
            SelectionChanged?.Invoke();
        }

        public void Cancel()
        {
            Select(null);
            Demolishing = false;
            SelectionChanged?.Invoke();
        }

        void Update()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (Selected == null && !Demolishing) return;

            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame))
            {
                Cancel();
                return;
            }
            if (mouse == null || cam == null) return;

            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            var vm = VillageManager.Instance;

            if (Demolishing)
            {
                var b = overUI ? null : vm.BuildingAt(world);
                if (b != hovered) { ClearHover(); hovered = b; if (b) b.GetComponent<SpriteRenderer>().color = new Color(1f, 0.5f, 0.5f); }
                if (b != null && mouse.leftButton.wasPressedThisFrame)
                {
                    int refund = Mathf.RoundToInt(b.def.cost * vm.demolishRefund);
                    FloatingText.Spawn(b.transform.position + Vector3.up, $"+{refund} ouro", new Color(1f, 0.85f, 0.3f));
                    hovered = null;
                    vm.Demolish(b);
                }
                return;
            }

            var def = Selected;
            var origin = new Vector2Int(
                Mathf.FloorToInt(world.x - def.footprint.x / 2f + 0.5f),
                Mathf.FloorToInt(world.y - def.footprint.y / 2f + 0.5f));
            ghost.transform.position = VillageManager.WorldPosition(def, origin);

            bool valid = vm.CanPlace(def, origin, out string reason);
            if (valid && vm.Gold < def.cost) { valid = false; reason = $"Ouro insuficiente (precisa de {def.cost})."; }
            ghostFootprint.color = valid ? ValidColor : InvalidColor;
            ghostSprite.color = valid ? new Color(1, 1, 1, 0.75f) : new Color(1, 0.6f, 0.6f, 0.6f);
            Hint = valid ? $"{def.displayName} ({def.cost} ouro): {def.description}" : reason;

            if (!overUI && valid && mouse.leftButton.wasPressedThisFrame && vm.TrySpendGold(def.cost))
            {
                vm.Place(def, origin);
                FloatingText.Spawn(ghost.transform.position + Vector3.up * def.footprint.y, $"-{def.cost} ouro", new Color(1f, 0.85f, 0.3f));
                bool keep = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
                if (!keep) Cancel();
            }
        }

        void ClearHover()
        {
            if (hovered != null) hovered.GetComponent<SpriteRenderer>().color = Color.white;
            hovered = null;
        }

        static Sprite whiteSprite;
        static Sprite WhiteSprite()
        {
            if (whiteSprite != null) return whiteSprite;
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            return whiteSprite;
        }
    }
}
