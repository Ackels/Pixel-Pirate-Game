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
        /// <summary>Fazendo caminho: clique ou arraste para pintar.</summary>
        public bool Paving { get; private set; }
        /// <summary>Mensagem de ajuda/erro para o HUD mostrar.</summary>
        public string Hint { get; private set; }
        public event Action SelectionChanged;
        /// <summary>Último frame em que o modo construção usou um clique/tecla (para o HUD não reagir ao mesmo clique).</summary>
        public int LastActionFrame { get; private set; } = -1;
        public bool Active => Selected != null || Demolishing || Paving;

        static readonly Color ValidColor = new Color(0.4f, 1f, 0.4f, 0.35f);
        static readonly Color InvalidColor = new Color(1f, 0.3f, 0.3f, 0.35f);

        Camera cam;
        GameObject ghost;
        SpriteRenderer ghostSprite;
        SpriteRenderer ghostFootprint;
        Building hovered;
        NatureObject hoveredNature;

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
            Paving = false;
            Selected = def;
            ghost.SetActive(def != null);
            if (def != null)
            {
                ghostSprite.sprite = def.sprite;
                ghostFootprint.transform.localScale = new Vector3(def.footprint.x, def.footprint.y, 1);
                ghostFootprint.transform.localPosition = new Vector3(0, def.footprint.y / 2f, 0);
                Hint = $"{def.displayName} ({VillageManager.CostText(def)}): {def.description}";
            }
            else Hint = null;
            SelectionChanged?.Invoke();
        }

        public void StartDemolish()
        {
            Select(null);
            Demolishing = true;
            Hint = "Clique numa construção para demolir (devolve metade do custo) ou numa árvore, pedra ou arbusto para limpar o terreno.";
            SelectionChanged?.Invoke();
        }

        /// <summary>Ferramenta de caminho: um bloco por vez (clique ou arraste).</summary>
        public void StartPaving()
        {
            Select(null);
            Paving = true;
            ghost.SetActive(true);
            ghostSprite.sprite = null;
            ghostFootprint.transform.localScale = new Vector3(2f, 2f, 1f);   // pincel 2x2: as peças de borda do caminho precisam de 2 de largura
            ghostFootprint.transform.localPosition = new Vector3(0, 1f, 0);
            SelectionChanged?.Invoke();
        }

        public void Cancel()
        {
            Select(null);
            Demolishing = false;
            Paving = false;
            SelectionChanged?.Invoke();
        }

        void Update()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (Selected == null && !Demolishing && !Paving) return;

            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame))
            {
                LastActionFrame = Time.frameCount;
                Cancel();
                return;
            }
            if (mouse == null || cam == null) return;

            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            var vm = VillageManager.Instance;

            if (Paving)
            {
                // Pincel 2x2 com o mouse no meio
                var cell = new Vector2Int(Mathf.FloorToInt(world.x - 0.5f), Mathf.FloorToInt(world.y - 0.5f));
                ghost.transform.position = new Vector3(cell.x + 1f, cell.y, 0);
                string why = null;
                int can = 0;
                for (int dx = 0; dx < 2; dx++)
                    for (int dy = 0; dy < 2; dy++)
                        if (vm.CanPlacePath(cell + new Vector2Int(dx, dy), out string w)) can++; else if (why == null) why = w;
                bool ok = can > 0;
                ghostFootprint.color = ok ? ValidColor : InvalidColor;
                Hint = ok ? $"Caminho ({vm.pathCost} ouro por bloco): clique ou arraste. Em caminhos todo mundo anda mais rápido."
                          : why;
                if (!overUI && ok && mouse.leftButton.isPressed)
                {
                    bool placed = false;
                    for (int dx = 0; dx < 2; dx++)
                        for (int dy = 0; dy < 2; dy++)
                        {
                            var c = cell + new Vector2Int(dx, dy);
                            if (vm.CanPlacePath(c, out _) && vm.PlacePath(c)) placed = true;
                        }
                    if (placed) AudioManager.Play(Sfx.Click, 0.5f);
                }
                if (mouse.leftButton.isPressed) LastActionFrame = Time.frameCount;
                return;
            }

            if (Demolishing)
            {
                var b = overUI ? null : vm.BuildingAt(world);
                var nat = overUI || b != null ? null : vm.NatureAt(world);
                if (b != hovered || nat != hoveredNature)
                {
                    ClearHover();
                    hovered = b;
                    hoveredNature = nat;
                    if (b) b.GetComponent<SpriteRenderer>().color = new Color(1f, 0.5f, 0.5f);
                    if (nat) nat.GetComponent<SpriteRenderer>().color = new Color(1f, 0.55f, 0.55f);
                }
                var pathCell = new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
                bool onPath = !overUI && b == null && nat == null && vm.IsPlayerPath(pathCell);
                var brush = new Vector2Int(Mathf.FloorToInt(world.x - 0.5f), Mathf.FloorToInt(world.y - 0.5f));
                Hint = nat != null ? $"Clique para limpar: {nat.DisplayName} (rende material)." : b != null ? $"Demolir {b.def.displayName} (devolve {VillageManager.CostText(b.def, vm.demolishRefund)})."
                    : onPath ? "Clique para tirar o caminho."
                    : "Clique numa construção para demolir (devolve metade do custo), numa árvore ou pedra para limpar o terreno (rende madeira ou pedra), ou num caminho seu para tirar.";
                if (onPath && mouse.leftButton.wasPressedThisFrame)
                {
                    LastActionFrame = Time.frameCount;
                    // Tira um bloco 2x2 (o mesmo tamanho do pincel), para não sobrar faixa fina
                    for (int dx = 0; dx < 2; dx++)
                        for (int dy = 0; dy < 2; dy++) vm.RemovePath(brush + new Vector2Int(dx, dy));
                    vm.RemovePath(pathCell);
                    AudioManager.Play(Sfx.Swing, 0.5f);
                    return;
                }
                if (nat != null && mouse.leftButton.wasPressedThisFrame)
                {
                    FloatingText.Spawn(nat.transform.position + Vector3.up, "Terreno limpo!", new Color(0.85f, 1f, 0.7f));
                    hoveredNature = null;
                    LastActionFrame = Time.frameCount;
                    vm.ClearNature(nat);
                    return;
                }
                if (b != null && mouse.leftButton.wasPressedThisFrame)
                {
                    FloatingText.Spawn(b.transform.position + Vector3.up, "+" + VillageManager.CostText(b.def, vm.demolishRefund), new Color(1f, 0.85f, 0.3f));
                    hovered = null;
                    LastActionFrame = Time.frameCount;
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
            if (valid && !vm.CanAfford(def, out string missing)) { valid = false; reason = missing; }
            ghostFootprint.color = valid ? ValidColor : InvalidColor;
            ghostSprite.color = valid ? new Color(1, 1, 1, 0.75f) : new Color(1, 0.6f, 0.6f, 0.6f);
            Hint = valid ? $"{def.displayName} ({VillageManager.CostText(def)}): {def.description}" : reason;

            if (!overUI && valid && mouse.leftButton.wasPressedThisFrame && vm.TryPay(def))
            {
                LastActionFrame = Time.frameCount;
                vm.Place(def, origin);
                AudioManager.Play(Sfx.Build);
                FloatingText.Spawn(ghost.transform.position + Vector3.up * def.footprint.y, "-" + VillageManager.CostText(def), new Color(1f, 0.85f, 0.3f));
                bool keep = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
                if (!keep) Cancel();
            }
        }

        void ClearHover()
        {
            if (hovered != null) hovered.GetComponent<SpriteRenderer>().color = Color.white;
            hovered = null;
            if (hoveredNature != null) { var sr = hoveredNature.GetComponent<SpriteRenderer>(); if (sr) sr.color = hoveredNature.baseColor; }
            hoveredNature = null;
        }

        static readonly Color natureTint = Color.white;
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
