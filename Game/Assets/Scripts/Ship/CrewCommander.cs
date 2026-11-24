using System.Collections.Generic;
using Pixel.Combat;
using Pixel.Village;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Pixel.Ship
{
    /// <summary>
    /// Comando da tripulação (inspirado no Pixel Piracy):
    /// - P pausa o jogo ("pausa tática"); com o jogo parado aparece uma espada em cima de cada tripulante.
    /// - Arraste a espada (ou o próprio tripulante, mesmo sem pausar) até um inimigo para mandar atacá-lo,
    ///   ou até um lugar para mandá-lo ir para lá. A linha em arco mostra a ordem (vermelha = atacar, amarela = ir).
    /// - Se o destino estiver no outro navio, o tripulante atravessa pendurado na corda.
    /// - Barra de comandos (canto de baixo, à direita): arrastar MOVER / ATACAR manda a tripulação inteira;
    ///   clicar INTERAGIR (E), REUNIR e CAVAR (G). A explicação de cada um aparece no mouse.
    /// - Painel do selecionado (canto de baixo, à esquerda): rosto, nome, nível, vida, fome e moral.
    ///   Clique num tripulante (ou no capitão) para selecionar.
    /// </summary>
    public class CrewCommander : MonoBehaviour
    {
        public static bool Paused { get; private set; }
        public static bool Dragging { get; private set; }
        public static CrewCommander Instance { get; private set; }

        SeaController sea;
        RectTransform canvas;
        GameObject banner;
        Sprite sword;
        readonly Dictionary<Combatant, RectTransform> icons = new Dictionary<Combatant, RectTransform>();
        readonly Dictionary<Combatant, LineRenderer> orderLines = new Dictionary<Combatant, LineRenderer>();
        Combatant dragged;
        LineRenderer dragLine;
        enum DragKind { One, AllMove, AllAttack }
        DragKind dragKind;
        bool dragging;
        RectTransform bar, moveBtn, attackBtn, digBtn;
        Image digIcon;

        // Painel do selecionado
        Combatant selected;
        RectTransform selPanel;
        Image selPortrait;
        Text selName;
        (Image fill, Text value, GameObject row) hpBar, hungerBar, moraleBar;

        public void Init(RectTransform canvasRoot, SeaController seaController)
        {
            Instance = this;
            canvas = canvasRoot;
            sea = seaController;
            sword = UIKit.Skin.Icon("espada");

            var b = UIKit.Panel("PausaTatica", canvas, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(0, 40));
            UIKit.Horizontal(b.gameObject, 8, 0).padding = new RectOffset(18, 18, 6, 6);
            b.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var t = UIKit.Text(b, UIKit.Colored("PAUSA", UIKit.GoldColor) + "  arraste a espada de um tripulante até um inimigo (atacar) ou um lugar (ir) · P: continuar",
                15, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            foreach (var g in b.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            banner = b.gameObject;
            banner.SetActive(false);

            BuildCommandBar();
            BuildSelectionPanel();
        }

        // ---------- Barra de comandos ----------
        void BuildCommandBar()
        {
            bar = UIKit.Panel("Comandos", canvas, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-12, 54), new Vector2(0, 64));
            UIKit.Horizontal(bar.gameObject, 6, 0).padding = new RectOffset(8, 8, 7, 7);
            bar.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            moveBtn = CommandButton("mover", null, () =>
                "ARRASTAR: " + UIKit.Colored("MOVER", UIKit.GoldColor) + "\nArraste até um lugar: a tripulação inteira vai para lá.");
            attackBtn = CommandButton("espada", null, () =>
                "ARRASTAR: " + UIKit.Colored("ATACAR", UIKit.GoldColor) + "\nArraste até um inimigo: todos atacam esse alvo.");
            CommandButton("mao", () => sea.InteractNearby(), () =>
                "CLIQUE: " + UIKit.Colored("INTERAGIR", UIKit.GoldColor) + "\nO capitão usa o que estiver perto dele\n(baús, comerciante). Atalho: E.");
            CommandButton("bandeira", Rally, () =>
                "CLIQUE: " + UIKit.Colored("REUNIR", UIKit.GoldColor) + "\nToda a tripulação volta para perto do capitão\n(e esquece as ordens anteriores).");
            digBtn = CommandButton("pa", () => sea.Dig(), () =>
                "CLIQUE: " + UIKit.Colored("CAVAR", UIKit.GoldColor) + "\nO capitão cava onde está. Nas ilhas do tesouro,\nprocure o X vermelho na areia! Atalho: G." +
                (sea.Mode == SeaMode.Landed ? "" : "\n" + UIKit.Colored("Só em terra firme.", new Color(1f, 0.5f, 0.45f))));
            digIcon = digBtn.Find("Icone").GetComponent<Image>();
        }

        RectTransform CommandButton(string icon, UnityEngine.Events.UnityAction onClick, System.Func<string> tip)
        {
            var rt = UIKit.Rect("Comando_" + icon, bar);
            UIKit.Size(rt, 50, 50);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = UIKit.Skin.Button;
            bg.type = Image.Type.Sliced;
            if (UIKit.Skin.Button != null) bg.pixelsPerUnitMultiplier = 0.5f * 100f / UIKit.Skin.Button.pixelsPerUnit;
            if (onClick != null)
            {
                var b = rt.gameObject.AddComponent<Button>();
                b.targetGraphic = bg;
                b.onClick.AddListener(() => { AudioManager.Play(Sfx.Click); onClick(); });
            }
            var ic = UIKit.Rect("Icone", rt);
            UIKit.Stretch(ic, 9);
            var img = ic.gameObject.AddComponent<Image>();
            img.sprite = UIKit.Skin.Icon(icon);
            img.preserveAspect = true;
            img.raycastTarget = false;
            UIKit.Tip(rt, tip);
            return rt;
        }

        /// <summary>Todos de volta para perto do capitão.</summary>
        void Rally()
        {
            if (sea.captain == null) return;
            int i = 0;
            foreach (var c in sea.CrewFighters)
            {
                if (c == null || c.IsDown) continue;
                var ai = c.GetComponent<CombatAI>();
                if (ai == null) continue;
                ai.OrderMove((Vector2)sea.captain.transform.position + new Vector2(Spread(i++), 0f));
            }
            sea.Say(i > 0 ? "Todos comigo!" : "Não há tripulantes de pé para reunir.");
        }

        static float Spread(int i) => (i % 2 == 0 ? 0.7f : -0.7f) * (1 + i / 2);

        // ---------- Painel do selecionado ----------
        void BuildSelectionPanel()
        {
            selPanel = UIKit.Panel("Selecionado", canvas, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12, 12), new Vector2(290, 84));
            var pr = UIKit.Rect("Rosto", selPanel);
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0f, 0.5f);
            pr.anchoredPosition = new Vector2(12, 0);
            pr.sizeDelta = new Vector2(56, 56);
            selPortrait = pr.gameObject.AddComponent<Image>();
            selPortrait.preserveAspect = true;
            selPortrait.raycastTarget = false;
            selName = UIKit.Text(selPanel, "", 15, TextAnchor.MiddleLeft, UIKit.GoldColor);
            var nrt = selName.rectTransform;
            nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(0f, 1f);
            nrt.anchoredPosition = new Vector2(78, -6);
            nrt.sizeDelta = new Vector2(200, 20);
            UIKit.Fit(selName, 10);
            hpBar = StatBar("Vida", 0, new Color(0.45f, 0.85f, 0.4f));
            hungerBar = StatBar("Fome", 1, new Color(0.95f, 0.45f, 0.4f));
            moraleBar = StatBar("Moral", 2, new Color(0.95f, 0.8f, 0.35f));
            UIKit.Tip(selPanel, () => "Personagem selecionado.\nClique num tripulante (ou no capitão) para ver os dados dele.");
        }

        (Image, Text, GameObject) StatBar(string label, int row, Color color)
        {
            var r = UIKit.Rect(label, selPanel);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(78, -28 - row * 17);
            r.sizeDelta = new Vector2(200, 15);
            var l = UIKit.Text(r, label, 12, TextAnchor.MiddleLeft);
            l.rectTransform.anchorMin = l.rectTransform.anchorMax = l.rectTransform.pivot = new Vector2(0f, 0.5f);
            l.rectTransform.sizeDelta = new Vector2(48, 15);
            l.raycastTarget = false;
            var bg = UIKit.Rect("Fundo", r);
            bg.anchorMin = bg.anchorMax = bg.pivot = new Vector2(0f, 0.5f);
            bg.anchoredPosition = new Vector2(50, 0);
            bg.sizeDelta = new Vector2(100, 8);
            var bgi = bg.gameObject.AddComponent<Image>();
            bgi.color = new Color(0, 0, 0, 0.55f);
            bgi.raycastTarget = false;
            var fill = UIKit.Rect("Preenchimento", bg);
            UIKit.Stretch(fill);
            var fi = fill.gameObject.AddComponent<Image>();
            fi.color = color;
            fi.raycastTarget = false;
            var v = UIKit.Text(r, "", 12, TextAnchor.MiddleLeft);
            v.rectTransform.anchorMin = v.rectTransform.anchorMax = v.rectTransform.pivot = new Vector2(0f, 0.5f);
            v.rectTransform.anchoredPosition = new Vector2(156, 0);
            v.rectTransform.sizeDelta = new Vector2(48, 15);
            v.raycastTarget = false;
            return (fi, v, r.gameObject);
        }

        static void SetBar((Image fill, Text value, GameObject row) b, float frac, string text)
        {
            ((RectTransform)b.fill.transform).anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);
            b.value.text = text;
        }

        void UpdatePanels()
        {
            bool show = sea.Mode != SeaMode.Editing;
            bar.gameObject.SetActive(show);
            selPanel.gameObject.SetActive(show);
            if (!show) return;
            digIcon.color = sea.Mode == SeaMode.Landed ? Color.white : new Color(1, 1, 1, 0.35f);

            if (selected == null || (selected != sea.captainCombatant && !sea.CrewFighters.Contains(selected))) selected = sea.captainCombatant;
            var c = selected;
            if (c == null) { selPanel.gameObject.SetActive(false); return; }
            var cm = CrewManager.Instance;
            var m = c.link as CrewMember;
            bool captain = c == sea.captainCombatant;
            CharacterLook look = null;
            if (cm != null) look = captain ? (cm.looks != null ? cm.looks.captain : null) : m != null ? cm.LookOf(m) : null;
            selPortrait.sprite = look != null ? look.portrait : null;
            selPortrait.enabled = selPortrait.sprite != null;
            selName.text = captain ? "Capitão" : m != null ? $"{m.name}  <size=12>Nv {m.level} · {CrewText.Name(m.profession)}</size>" : c.displayName;
            SetBar(hpBar, c.maxHp > 0 ? (float)c.hp / c.maxHp : 0f, $"{c.hp}/{c.maxHp}");
            var life = c.GetComponent<CrewLife>();
            hungerBar.row.SetActive(life != null);
            if (life != null) SetBar(hungerBar, life.Hunger / 100f, $"{Mathf.RoundToInt(life.Hunger)}%");
            moraleBar.row.SetActive(m != null);
            if (m != null) SetBar(moraleBar, m.morale / 100f, $"{m.morale}%");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Paused) { Paused = false; Time.timeScale = 1f; }
            Dragging = false;
            if (dragLine != null) Destroy(dragLine.gameObject);
            foreach (var l in orderLines.Values) if (l != null) Destroy(l.gameObject);
        }

        bool CanPause => sea != null && (sea.Mode == SeaMode.Sailing || sea.Mode == SeaMode.Landed) && !GameMenu.IsOpen;

        public void Toggle()
        {
            if (Paused) Resume();
            else if (CanPause) Pause();
        }

        void Pause()
        {
            Paused = true;
            Time.timeScale = 0f;
            banner.SetActive(true);
            banner.transform.SetAsLastSibling();
            AudioManager.Play(Sfx.Click);
        }

        void Resume()
        {
            Paused = false;
            Time.timeScale = SandboxMode.Active ? SandboxMode.Speed : 1f;
            banner.SetActive(false);
            AudioManager.Play(Sfx.Click);
        }

        void Update()
        {
            if (sea == null) return;
            var kb = Keyboard.current;
            if (kb != null && kb.pKey.wasPressedThisFrame && !GameMenu.IsOpen && !SeaDialog.IsOpen) Toggle();
            if (SeaDialog.IsOpen && !GameMenu.IsOpen && Time.timeScale != 0f) Time.timeScale = 0f;   // o menu (Esc) devolveu o tempo
            if (Paused)
            {
                if (!CanPause && !GameMenu.IsOpen && !SeaDialog.IsOpen) Resume();                       // atracou / foi editar o navio
                else if (!GameMenu.IsOpen && Time.timeScale != 0f) Time.timeScale = 0f;   // o menu de pausa (Esc) devolveu o tempo
            }
            UpdateIcons();
            UpdateOrderLines();
            UpdatePanels();
            HandleDrag();
        }

        // ---------- Espadas em cima dos tripulantes (só na pausa) ----------
        void UpdateIcons()
        {
            var cam = Camera.main;
            var alive = new HashSet<Combatant>();
            if (Paused && cam != null)
                foreach (var c in sea.CrewFighters)
                {
                    if (c == null || c.IsDown) continue;
                    alive.Add(c);
                    if (!icons.TryGetValue(c, out var rt) || rt == null) icons[c] = rt = MakeIcon();
                    Vector2 head = (Vector2)c.transform.position + Vector2.up * (c.figureHeight + 0.6f);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, cam.WorldToScreenPoint(head), null, out var local);
                    rt.anchoredPosition = local;
                    var hasOrder = c.GetComponent<CombatAI>() is CombatAI ai && ai.HasOrder;
                    rt.GetComponent<Image>().color = hasOrder ? new Color(1f, 0.82f, 0.82f) : Color.white;
                }
            foreach (var kv in icons) if (kv.Value != null) kv.Value.gameObject.SetActive(alive.Contains(kv.Key));
        }

        RectTransform MakeIcon()
        {
            var rt = UIKit.Rect("Ordem", canvas);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(34, 34);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = UIKit.Skin.Button;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = UIKit.Skin.Button != null ? 0.5f * 100f / UIKit.Skin.Button.pixelsPerUnit : 1f;
            var icon = UIKit.Rect("Espada", rt);
            UIKit.Stretch(icon, 5);
            var img = icon.gameObject.AddComponent<Image>();
            img.sprite = sword;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return rt;
        }

        // ---------- Linhas das ordens em andamento (só na pausa) ----------
        void UpdateOrderLines()
        {
            foreach (var c in sea.CrewFighters)
            {
                if (c == null) continue;
                var ai = c.GetComponent<CombatAI>();
                bool show = Paused && !c.IsDown && ai != null && ai.HasOrder;
                orderLines.TryGetValue(c, out var lr);
                if (!show) { if (lr != null) lr.gameObject.SetActive(false); continue; }
                bool attack = ai.orderTarget != null;
                if (lr == null) orderLines[c] = lr = Orders.MakeLine("OrdemAtual", Orders.AttackColor, 0.06f);
                lr.gameObject.SetActive(true);
                lr.startColor = lr.endColor = attack ? Orders.AttackColor : Orders.MoveColor;
                Vector2 to = attack ? (Vector2)ai.orderTarget.transform.position + Vector2.up * 0.5f : ai.orderPoint;
                Orders.Arc(lr, (Vector2)c.transform.position + Vector2.up * (c.figureHeight + 0.2f), to);
            }
        }

        // ---------- Arrastar uma ordem ----------
        /// <summary>Onde a linha da ordem começa: a cabeça do tripulante, ou o meio da tripulação (ordem para todos).</summary>
        bool DragOrigin(out Vector2 from)
        {
            if (dragKind == DragKind.One) { from = (Vector2)dragged.transform.position + Vector2.up * (dragged.figureHeight + 0.2f); return true; }
            Vector2 sum = Vector2.zero;
            int n = 0;
            foreach (var c in sea.CrewFighters) if (c != null && !c.IsDown) { sum += (Vector2)c.transform.position; n++; }
            from = n > 0 ? sum / n + Vector2.up * 1.2f : Vector2.zero;
            return n > 0;
        }

        void HandleDrag()
        {
            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || cam == null || sea.Mode == SeaMode.Editing || GameMenu.IsOpen) { CancelDrag(); return; }
            Vector2 screen = mouse.position.ReadValue();
            Vector2 world = cam.ScreenToWorldPoint(screen);

            if (mouse.leftButton.wasPressedThisFrame && !dragging)
            {
                Combatant pick = null;
                DragKind kind = DragKind.One;
                if (bar.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(moveBtn, screen, null)) kind = DragKind.AllMove;
                else if (bar.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(attackBtn, screen, null)) kind = DragKind.AllAttack;
                else if (Paused)
                    foreach (var kv in icons)
                        if (kv.Value != null && kv.Value.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(kv.Value, screen, null)) { pick = kv.Key; break; }
                bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (kind == DragKind.One && pick == null && !overUI)
                {
                    var c = Orders.At(world, Team.Player, false);
                    if (c != null && c == sea.captainCombatant) selected = c;   // clicou no capitão: só seleciona
                    else if (c != null && sea.CrewFighters.Contains(c)) pick = c;
                }
                if (kind != DragKind.One || pick != null)
                {
                    dragKind = kind;
                    dragged = pick;
                    dragging = true;
                    Dragging = true;
                    dragLine = Orders.MakeLine("OrdemArrastando", Orders.MoveColor);
                }
            }
            if (!dragging) return;
            if (dragKind == DragKind.One && (dragged == null || dragged.IsDown)) { CancelDrag(); return; }

            var enemy = dragKind == DragKind.AllMove ? null : Orders.At(world, null, true);
            bool hasFrom = DragOrigin(out var from);
            Vector2 end = enemy != null ? (Vector2)enemy.transform.position + Vector2.up * 0.5f : world;
            bool attackLook = enemy != null || dragKind == DragKind.AllAttack;
            dragLine.startColor = dragLine.endColor = attackLook ? Orders.AttackColor : Orders.MoveColor;
            dragLine.enabled = hasFrom;
            if (hasFrom) Orders.Arc(dragLine, from, end);

            if (!mouse.leftButton.wasReleasedThisFrame) return;
            var who = dragged;
            var kindNow = dragKind;
            CancelDrag();
            if (!hasFrom) { sea.Say("Não há tripulantes de pé para receber ordens."); return; }
            bool releasedOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (kindNow == DragKind.One && Vector2.Distance(from, end) < 0.6f + who.figureHeight) { selected = who; return; }   // só clicou: seleciona
            if (releasedOverUI)
            {
                if (kindNow != DragKind.One) sea.Say(kindNow == DragKind.AllMove ? "Segure e ARRASTE a seta até um lugar do navio ou da ilha." : "Segure e ARRASTE a espada até um inimigo.");
                return;
            }

            if (kindNow == DragKind.AllAttack && enemy == null) { sea.Say("Solte a espada em cima de um inimigo para atacar."); return; }
            int i = 0;
            foreach (var c in kindNow == DragKind.One ? new List<Combatant> { who } : new List<Combatant>(sea.CrewFighters))
            {
                if (c == null || c.IsDown) continue;
                var ai = c.GetComponent<CombatAI>();
                if (ai == null) continue;
                if (enemy != null) ai.OrderAttack(enemy);
                else ai.OrderMove(MovePoint(world, kindNow == DragKind.One ? 0f : Spread(i)));
                i++;
            }
            Orders.Flash(from, enemy != null ? end : MovePoint(world, 0f), enemy != null ? Orders.AttackColor : Orders.MoveColor);
            string subject = kindNow == DragKind.One ? who.displayName : "Tripulação";
            sea.Say(enemy != null ? $"{subject}: atacar {enemy.displayName}!" : $"{subject}: indo para lá!");
            AudioManager.Play(Sfx.Click);
        }

        /// <summary>Lugar de uma ordem de mover: no seu navio, no inimigo ou na ilha (fora disso, a beirada do seu navio).</summary>
        Vector2 MovePoint(Vector2 world, float offset)
        {
            float x = world.x + offset;
            var dir = EncounterDirector.Instance;
            bool onEnemy = dir != null && dir.EnemyShipContains(world.x);
            if (!onEnemy && sea.Mode != SeaMode.Landed)
                x = Mathf.Clamp(x, sea.shipView.LeftEdge + 0.5f, sea.shipView.RightEdge - 0.5f);
            return new Vector2(x, world.y);
        }

        void CancelDrag()
        {
            dragged = null;
            dragging = false;
            Dragging = false;
            if (dragLine != null) Destroy(dragLine.gameObject);
            dragLine = null;
        }
    }
}
