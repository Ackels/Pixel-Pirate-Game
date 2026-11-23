using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Pixel.Ship
{
    /// <summary>
    /// Editor do navio (estaleiro): mostra a grade, uma prévia do bloco escolhido
    /// (verde = pode, vermelho = não pode). Clique esquerdo coloca, direito remove (devolve metade).
    /// </summary>
    public class ShipEditor : MonoBehaviour
    {
        public ShipView view;
        public Sprite gridSprite;

        public ShipBlockDef Selected { get; set; }
        public string Hint { get; private set; }

        SpriteRenderer ghost, grid;
        Camera cam;

        void Awake()
        {
            cam = Camera.main;
            ghost = new GameObject("PreviaDoBloco").AddComponent<SpriteRenderer>();
            ghost.transform.SetParent(transform, false);
            ghost.sortingOrder = 40;
            grid = new GameObject("Grade").AddComponent<SpriteRenderer>();
            grid.transform.SetParent(transform, false);
            grid.sprite = gridSprite;
            grid.drawMode = SpriteDrawMode.Tiled;
            grid.size = new Vector2(ShipLayout.Width, ShipLayout.Height);
            grid.transform.position = view.origin + new Vector2(ShipLayout.Width, ShipLayout.Height) / 2f;
            grid.sortingOrder = 39;
        }

        void OnEnable()
        {
            if (grid) grid.gameObject.SetActive(true);
        }

        void OnDisable()
        {
            if (grid) grid.gameObject.SetActive(false);
            if (ghost) ghost.gameObject.SetActive(false);
            Hint = null;
        }

        void Update()
        {
            var sea = SeaController.Instance;
            var mouse = Mouse.current;
            if (sea == null || mouse == null || cam == null) return;
            var layout = sea.Layout;

            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            var cell = view.WorldToCell(world);
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool inGrid = ShipLayout.InBounds(cell) && !overUI;

            // Remover com o botão direito
            if (inGrid && mouse.rightButton.wasPressedThisFrame && layout.Get(cell) != null)
            {
                if (layout.CanRemove(cell, out string why))
                {
                    var removed = layout.Remove(cell);
                    sea.Refund(removed.cost / 2);
                }
                else Hint = why;
                return;
            }

            ghost.gameObject.SetActive(Selected != null && inGrid);
            if (Selected == null)
            {
                var hovered = inGrid ? layout.Get(cell) : null;
                Hint = hovered != null
                    ? $"{hovered.displayName}: botão direito remove (devolve {hovered.cost / 2} ouro)."
                    : "Escolha um bloco na barra de baixo. Botão direito remove um bloco.";
                return;
            }
            if (!inGrid) { Hint = $"{Selected.displayName} ({Selected.cost} ouro): {Selected.description}"; return; }

            ghost.sprite = Selected.sprite;
            ghost.transform.position = view.CellCenter(cell);
            bool valid = layout.CanPlace(Selected, cell, out string reason);
            if (valid && sea.Gold < Selected.cost) { valid = false; reason = $"Ouro insuficiente (precisa de {Selected.cost})."; }
            ghost.color = valid ? new Color(0.6f, 1f, 0.6f, 0.8f) : new Color(1f, 0.4f, 0.4f, 0.6f);
            Hint = valid ? $"{Selected.displayName} ({Selected.cost} ouro): {Selected.description}" : reason;

            if (valid && mouse.leftButton.wasPressedThisFrame && sea.Spend(Selected.cost))
                layout.Set(cell, Selected);
        }
    }
}
