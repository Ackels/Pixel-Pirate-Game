using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Pixel.Ship
{
    /// <summary>
    /// Editor do navio (estaleiro): grade, prévia do bloco escolhido (verde = pode, vermelho = não pode).
    /// Clique (ou arraste) coloca; botão direito (ou arraste) remove e devolve metade.
    /// R ou rodinha do mouse gira a peça; M liga o espelho (constrói dos dois lados de um eixo).
    /// </summary>
    public class ShipEditor : MonoBehaviour
    {
        public ShipView view;
        public Sprite gridSprite;

        ShipBlockDef selected;
        public ShipBlockDef Selected
        {
            get => selected;
            set
            {
                selected = value;
                if (selected != null) Rotation %= ShipShapes.Orientations(selected.shape);
            }
        }
        public int Rotation { get; private set; }
        public bool Mirror { get; private set; }
        public string Hint { get; private set; }
        public event System.Action StateChanged;

        SpriteRenderer ghost, ghostMirror, grid, axisLine;
        Camera cam;
        int axis2;                       // eixo do espelho, em meias células (2*x)
        Vector2Int lastCell = new Vector2Int(-999, -999);

        void Awake()
        {
            cam = Camera.main;
            ghost = NewRenderer("PreviaDoBloco", 40);
            ghostMirror = NewRenderer("PreviaEspelho", 40);
            grid = NewRenderer("Grade", 39);
            grid.sprite = gridSprite;
            grid.drawMode = SpriteDrawMode.Tiled;
            grid.size = new Vector2(ShipLayout.Width, ShipLayout.Height);
            grid.transform.position = view.origin + new Vector2(ShipLayout.Width, ShipLayout.Height) / 2f;

            // Linha do eixo do espelho (textura de 1 pixel esticada)
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            axisLine = NewRenderer("EixoEspelho", 41);
            axisLine.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            axisLine.color = new Color(0.3f, 0.9f, 1f, 0.6f);
            axisLine.transform.localScale = new Vector3(0.08f, ShipLayout.Height, 1f);
            axisLine.gameObject.SetActive(false);
        }

        SpriteRenderer NewRenderer(string name, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sortingOrder = order;
            return sr;
        }

        void OnEnable()
        {
            if (grid) grid.gameObject.SetActive(true);
            if (axisLine) axisLine.gameObject.SetActive(Mirror);
        }

        void OnDisable()
        {
            if (grid) grid.gameObject.SetActive(false);
            if (ghost) ghost.gameObject.SetActive(false);
            if (ghostMirror) ghostMirror.gameObject.SetActive(false);
            if (axisLine) axisLine.gameObject.SetActive(false);
            Hint = null;
        }

        // ---------- Botões do HUD ----------
        public void Rotate(int dir = 1)
        {
            int n = selected != null ? ShipShapes.Orientations(selected.shape) : 1;
            Rotation = ((Rotation + dir) % n + n) % n;
            StateChanged?.Invoke();
        }

        public void ToggleMirror()
        {
            Mirror = !Mirror;
            if (Mirror)
            {
                // Eixo no meio do navio atual (ou da grade, se estiver vazio)
                int min = int.MaxValue, max = int.MinValue;
                var sea = SeaController.Instance;
                if (sea != null) foreach (var c in sea.Layout.Blocks.Keys) { min = Mathf.Min(min, c.x); max = Mathf.Max(max, c.x); }
                axis2 = min <= max ? min + max + 1 : ShipLayout.Width;
                axisLine.transform.position = view.origin + new Vector2(axis2 / 2f, ShipLayout.Height / 2f);
            }
            axisLine.gameObject.SetActive(Mirror && enabled);
            StateChanged?.Invoke();
        }

        void Update()
        {
            var sea = SeaController.Instance;
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (sea == null || mouse == null || cam == null) return;
            var layout = sea.Layout;

            if (kb != null && kb.rKey.wasPressedThisFrame) Rotate(kb.shiftKey.isPressed ? -1 : 1);
            if (kb != null && kb.mKey.wasPressedThisFrame) ToggleMirror();
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f && selected != null && ShipShapes.Orientations(selected.shape) > 1) Rotate(wheel > 0 ? 1 : -1);

            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            var cell = view.WorldToCell(world);
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool inGrid = ShipLayout.InBounds(cell) && !overUI;
            var mcell = ShipLayout.MirrorCell(cell, axis2);
            bool useMirror = Mirror && mcell != cell && ShipLayout.InBounds(mcell);

            bool newCell = cell != lastCell;
            if (!mouse.leftButton.isPressed && !mouse.rightButton.isPressed) lastCell = new Vector2Int(-999, -999);

            // Remover com o botão direito (segurar e arrastar remove vários)
            if (inGrid && mouse.rightButton.isPressed && (mouse.rightButton.wasPressedThisFrame || newCell))
            {
                lastCell = cell;
                if (layout.Get(cell) != null) TryRemove(sea, layout, cell);
                if (useMirror && layout.Get(mcell) != null) TryRemove(sea, layout, mcell);
                ghost.gameObject.SetActive(false);
                ghostMirror.gameObject.SetActive(false);
                return;
            }

            ghost.gameObject.SetActive(selected != null && inGrid);
            ghostMirror.gameObject.SetActive(selected != null && inGrid && useMirror);
            if (selected == null)
            {
                var hovered = inGrid ? layout.Get(cell) : null;
                Hint = hovered != null
                    ? $"{hovered.displayName}: botão direito remove (devolve {hovered.cost / 2} ouro)."
                    : "Escolha uma peça embaixo. Botão direito remove. M: espelho.";
                return;
            }
            if (!inGrid) { Hint = $"{selected.displayName} ({selected.cost} ouro): {selected.description}"; return; }

            int rot = Rotation;
            int mrot = ShipShapes.Mirror(selected.shape, rot);
            ShowGhost(ghost, cell, rot);
            bool valid = CanPlaceHere(sea, layout, cell, out string reason);
            ghost.color = valid ? new Color(0.6f, 1f, 0.6f, 0.8f) : new Color(1f, 0.4f, 0.4f, 0.6f);
            if (useMirror)
            {
                ShowGhost(ghostMirror, mcell, mrot);
                ghostMirror.color = new Color(0.6f, 0.9f, 1f, 0.6f);
            }
            string keys = ShipShapes.Orientations(selected.shape) > 1 ? " · R gira" : "";
            Hint = valid ? $"{selected.displayName} ({selected.cost} ouro): {selected.description}{keys}" : reason;

            // Colocar (segurar e arrastar coloca vários)
            if (mouse.leftButton.isPressed && (mouse.leftButton.wasPressedThisFrame || newCell))
            {
                lastCell = cell;
                if (valid && sea.Spend(selected.cost)) layout.Set(cell, selected, rot);
                if (useMirror && CanPlaceHere(sea, layout, mcell, out _) && sea.Spend(selected.cost)) layout.Set(mcell, selected, mrot);
            }
        }

        bool CanPlaceHere(SeaController sea, ShipLayout layout, Vector2Int c, out string reason)
        {
            bool valid = layout.CanPlace(selected, c, out reason);
            if (valid && sea.Gold < selected.cost) { valid = false; reason = $"Ouro insuficiente (precisa de {selected.cost})."; }
            return valid;
        }

        void TryRemove(SeaController sea, ShipLayout layout, Vector2Int c)
        {
            if (layout.CanRemove(c, out string why))
            {
                var removed = layout.Remove(c);
                sea.Refund(removed.cost / 2);
            }
            else Hint = why;
        }

        void ShowGhost(SpriteRenderer sr, Vector2Int c, int rot)
        {
            sr.sprite = selected.Preview;
            var (fx, fy) = ShipShapes.Flips(selected.shape, rot);
            sr.flipX = fx; sr.flipY = fy;
            sr.transform.position = view.CellCenter(c);
        }
    }
}
