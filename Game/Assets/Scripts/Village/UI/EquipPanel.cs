using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>
    /// Janela de Equipamento (tecla I, na vila e no mar): escolha uma pessoa à esquerda (capitão ou tripulante),
    /// veja os 3 espaços dela (arma, chapéu, amuleto) e clique numa peça do Arsenal para equipar.
    /// Clicar num espaço ocupado guarda a peça de volta. Num porto, ou na vila com Mercado, dá para vender.
    /// </summary>
    public class EquipPanel : MonoBehaviour
    {
        class Person
        {
            public string name;
            public Loadout gear;
            public CrewMember member;   // null = capitão
            public string locked;       // motivo para não poder trocar (ex.: em expedição)
        }

        bool atSea, sellMode;
        int selected;
        System.Action<string> toast;
        RectTransform peopleList, slotRow, grid;
        Text personName, statsText, arsenalTitle, sellLabel;
        Image sellBg;
        RectTransform sellButton, faceBox;
        readonly List<Person> people = new List<Person>();

        public bool IsOpen => gameObject.activeSelf;

        public static EquipPanel Create(Transform canvas, bool atSea, System.Action<string> toast)
        {
            EquipPanel panel = null;
            var (win, content) = UIKit.Window("JanelaEquipamento", canvas, "Equipamento", new Vector2(1010, 470), () => panel.Hide());
            panel = win.gameObject.AddComponent<EquipPanel>();
            panel.atSea = atSea;
            panel.toast = toast;
            var h = UIKit.Horizontal(content.gameObject, 0, 12);
            h.childForceExpandHeight = true;
            h.childForceExpandWidth = false;   // a sobra de largura vai toda para o Arsenal

            // --- Esquerda: pessoas ---
            var left = Column(content, "Pessoas", 250);
            panel.peopleList = ScrollList(left, out _);
            var pv = UIKit.Vertical(panel.peopleList.gameObject, 0, 6);
            pv.childForceExpandHeight = false;

            // --- Meio: a pessoa escolhida e os 3 espaços ---
            var mid = Column(content, null, 240);
            var mv = UIKit.Vertical(mid.gameObject, 10, 8);
            mv.childAlignment = TextAnchor.UpperCenter;
            mv.childForceExpandHeight = false;
            var top = CrewCards.Row(mid, 76, 10);
            top.childAlignment = TextAnchor.MiddleLeft;
            panel.faceBox = UIKit.Rect("RostoGrande", top.transform);
            UIKit.Size(panel.faceBox, 72, 72);
            panel.personName = CrewCards.Label(top.transform, "", 18, UIKit.GoldColor, 140, 72);
            panel.personName.alignment = TextAnchor.MiddleLeft;
            panel.slotRow = CrewCards.Row(mid, 96, 10).GetComponent<RectTransform>();
            var slotLayout = panel.slotRow.GetComponent<HorizontalLayoutGroup>();
            slotLayout.childAlignment = TextAnchor.MiddleCenter;
            slotLayout.childForceExpandWidth = false;
            panel.statsText = CrewCards.Label(mid, "", 14, UIKit.TextColor, 220, 150);
            panel.statsText.alignment = TextAnchor.UpperLeft;
            panel.statsText.supportRichText = true;

            // --- Direita: o Arsenal ---
            var right = Column(content, null, -1);
            var rv = UIKit.Vertical(right.gameObject, 10, 6);
            rv.childForceExpandHeight = false;
            panel.arsenalTitle = CrewCards.Label(right, "Arsenal", 16, UIKit.GoldColor, -1, 22);
            var view = UIKit.Rect("Lista", right);
            view.gameObject.AddComponent<Image>().color = Color.clear;
            view.gameObject.AddComponent<RectMask2D>();
            UIKit.Size(view, flexWidth: 1).flexibleHeight = 1;
            panel.grid = UIKit.Rect("Pecas", view);
            panel.grid.anchorMin = new Vector2(0, 1); panel.grid.anchorMax = new Vector2(1, 1); panel.grid.pivot = new Vector2(0.5f, 1);
            panel.grid.sizeDelta = Vector2.zero;
            var g = panel.grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(62, 62);
            g.spacing = new Vector2(8, 8);
            panel.grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = panel.grid; sr.viewport = view; sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 24;

            var (sellBtn, sellText, sellImg) = UIKit.Button(right, "", panel.ToggleSell, 14);
            UIKit.Size(sellBtn, height: 38);
            panel.sellButton = (RectTransform)sellBtn.transform;
            panel.sellLabel = sellText;
            panel.sellBg = sellImg;
            UIKit.Tip(sellBtn, () => panel.sellMode ? "Clique numa peça do Arsenal para vender por metade do preço.\nClique aqui de novo para parar de vender."
                                                    : "Vender peças repetidas por metade do preço.");

            UIKit.Info(win, () =>
                "Cada pessoa usa uma arma, um chapéu e um amuleto.\n" +
                "• Arma: mais dano (algumas golpeiam mais rápido ou mais longe). A pá serve para cavar o X do tesouro.\n" +
                "• Chapéu: mais vida.\n" +
                "• Amuleto: bônus variados, como mais ouro nos saques.\n\n" +
                "Onde achar: baús das ilhas, navios vencidos ou saqueados, expedições, lojas dos portos e a Forja da vila.\n" +
                "Raridade: " + UIKit.Colored("Comum", GearDef.RarityColor(GearRarity.Comum)) + ", " + UIKit.Colored("Bom", GearDef.RarityColor(GearRarity.Bom)) + ", "
                + UIKit.Colored("Raro", GearDef.RarityColor(GearRarity.Raro)) + ", " + UIKit.Colored("Lendário", GearDef.RarityColor(GearRarity.Lendario)) + ".\n\n" +
                (atSea ? "No mar só dá para equipar quem está a bordo, e não no meio de uma briga." : "Quem está numa expedição só pode ser equipado na volta."));
            win.gameObject.SetActive(false);
            return panel;
        }

        static RectTransform Column(RectTransform parent, string title, float width)
        {
            var col = UIKit.Rect("Coluna", parent);
            col.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.18f);
            if (width > 0) { var le = UIKit.Size(col, width, flexWidth: 0); le.minWidth = width; } else UIKit.Size(col, flexWidth: 1);   // colunas fixas não crescem
            if (title != null)
            {
                var v = UIKit.Vertical(col.gameObject, 8, 6);
                v.childForceExpandHeight = false;
                CrewCards.Label(col, title, 16, UIKit.GoldColor, -1, 22);
            }
            return col;
        }

        static RectTransform ScrollList(RectTransform parent, out ScrollRect scroll)
        {
            var view = UIKit.Rect("Lista", parent);
            view.gameObject.AddComponent<Image>().color = Color.clear;
            view.gameObject.AddComponent<RectMask2D>();
            UIKit.Size(view, flexWidth: 1).flexibleHeight = 1;
            var list = UIKit.Rect("Itens", view);
            list.anchorMin = new Vector2(0, 1); list.anchorMax = new Vector2(1, 1); list.pivot = new Vector2(0.5f, 1);
            list.sizeDelta = Vector2.zero;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = list; scroll.viewport = view; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24;
            return list;
        }

        public void Show() { gameObject.SetActive(true); transform.SetAsLastSibling(); sellMode = false; Rebuild(); }
        public void Hide() => gameObject.SetActive(false);

        void OnEnable() { if (Armory.Instance != null) Armory.Instance.Changed += Rebuild; }
        void OnDisable() { if (Armory.Instance != null) Armory.Instance.Changed -= Rebuild; }

        void ToggleSell()
        {
            sellMode = !sellMode;
            Rebuild();
        }

        // ---------- Quem aparece ----------
        void CollectPeople()
        {
            people.Clear();
            var armory = Armory.Instance;
            people.Add(new Person { name = "Capitão", gear = armory.Captain });
            IEnumerable<CrewMember> crew = atSea
                ? (GameFlow.Instance != null ? GameFlow.Instance.Embarked : new List<CrewMember>())
                : (CrewManager.Instance != null ? (IEnumerable<CrewMember>)CrewManager.Instance.Crew : new List<CrewMember>());
            foreach (var m in crew)
                people.Add(new Person { name = m.name, gear = m.gear, member = m, locked = m.onExpedition ? "em expedição" : null });
            selected = Mathf.Clamp(selected, 0, people.Count - 1);
        }

        static Sprite CaptainPortrait()
        {
            var cm = CrewManager.Instance;
            var look = cm != null && cm.looks != null ? cm.looks.captain : null;
            return look == null ? null : look.portrait != null ? look.portrait : look.down[0];
        }

        static int HpOf(Person p) => p.member != null ? p.member.CombatHp : Armory.CaptainBaseHp + p.gear.Hp;
        static Vector2Int DamageOf(Person p) => p.member != null ? p.member.CombatDamage : Armory.CaptainBaseDamage + p.gear.Damage;

        // ---------- Montagem ----------
        void Rebuild()
        {
            var armory = Armory.Instance;
            if (armory == null || peopleList == null || !gameObject.activeSelf) return;
            CollectPeople();
            var who = people[selected];

            // Pessoas
            UIKit.Clear(peopleList);
            for (int i = 0; i < people.Count; i++)
            {
                var p = people[i];
                int index = i;
                var rt = UIKit.Rect("Pessoa", peopleList);
                var (btn, bg) = UIKit.ButtonBase(rt);
                UIKit.Size(rt, height: 52);
                UIKit.SetSelected(bg, i == selected);
                btn.onClick.AddListener(() => { selected = index; AudioManager.Play(Sfx.Click); Rebuild(); });
                var row = UIKit.Horizontal(rt.gameObject, 4, 8);
                row.padding = new RectOffset(8, 8, 4, 8);
                row.childAlignment = TextAnchor.MiddleLeft;
                row.childForceExpandWidth = false;
                if (p.member != null) CrewCards.Face(rt, p.member, 38);
                else CrewCards.Icon(rt, CaptainPortrait(), 38, 38);
                var dmg = DamageOf(p);
                string sub = p.locked ?? $"Vida {HpOf(p)} · Dano {dmg.x}–{dmg.y}";
                var t = CrewCards.Label(rt, $"{p.name}\n<size={UIKit.Scaled(11)}>{sub}</size>", 14, UIKit.ButtonText, 170, 40);
                t.supportRichText = true;
                t.alignment = TextAnchor.MiddleLeft;
                UIKit.Fit(t, 9);   // nomes compridos encolhem em vez de vazar
            }

            // A pessoa escolhida
            UIKit.Clear(faceBox);
            var face = who.member != null ? CrewCards.Face(faceBox, who.member, 72) : CrewCards.Icon(faceBox, CaptainPortrait(), 72, 72).rectTransform;
            UIKit.Stretch(face);
            personName.text = who.member != null ? $"{who.name}\n<size={UIKit.Scaled(12)}>{CrewText.Name(who.member.profession)} nv {who.member.level}</size>" : "Capitão";
            personName.supportRichText = true;

            UIKit.Clear(slotRow);
            foreach (GearSlot s in System.Enum.GetValues(typeof(GearSlot))) Slot(who, s);

            var d = DamageOf(who);
            var lines = new List<string>
            {
                $"Vida: {UIKit.Colored(HpOf(who).ToString(), UIKit.GoldColor)}",
                $"Dano: {UIKit.Colored($"{d.x}–{d.y}", UIKit.GoldColor)}",
            };
            if (Mathf.Abs(who.gear.Speed) > 0.001f) lines.Add($"Velocidade de golpe: {(who.gear.Speed > 0 ? "+" : "")}{Mathf.RoundToInt(who.gear.Speed * 100)}%");
            if (who.gear.Range > 0.001f) lines.Add($"Alcance: +{who.gear.Range:0.#} bloco");
            if (who.gear.Loot > 0.001f) lines.Add($"Ouro nos saques: +{Mathf.RoundToInt(who.gear.Loot * 100)}%");
            if (who.gear.Digs) lines.Add("Leva uma pá: pode cavar tesouro");
            if (who.locked != null) lines.Add(UIKit.Colored($"Não dá para trocar agora: {who.locked}.", CrewCards.Warn));
            statsText.text = string.Join("\n", lines);

            // Arsenal
            UIKit.Clear(grid);
            var owned = armory.Owned();
            arsenalTitle.text = $"Arsenal ({armory.Total} peça{(armory.Total == 1 ? "" : "s")})";
            if (owned.Count == 0)
            {
                var empty = CrewCards.Label(grid, "Nada guardado.", 13, UIKit.MutedText, 62, 62);
                empty.alignment = TextAnchor.MiddleCenter;
            }
            foreach (var (def, count) in owned) Piece(def, count, who);

            bool canSell = Armory.CanSellHere(atSea);
            if (!canSell) sellMode = false;
            sellButton.gameObject.SetActive(canSell);
            sellLabel.text = sellMode ? "Vendendo: clique numa peça (clique aqui para parar)" : "Vender peças (metade do preço)";
            UIKit.SetSelected(sellBg, sellMode);
        }

        void Slot(Person who, GearSlot slot)
        {
            var col = UIKit.Rect("Espaco", slotRow);
            UIKit.Size(col, 68, 96);
            var v = UIKit.Vertical(col.gameObject, 0, 2);
            v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.UpperCenter;
            var rt = UIKit.Rect("Botao", col);
            var (btn, bg) = UIKit.ButtonBase(rt);
            UIKit.Size(rt, 68, 68);
            var g = who.gear.Get(slot);
            var icon = UIKit.Rect("Icone", rt);
            icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = new Vector2(0, 3);
            icon.sizeDelta = new Vector2(42, 42);
            var img = icon.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (g != null)
            {
                img.sprite = g.icon;
                RarityBar(rt, g.Color);
            }
            else
            {
                img.sprite = slot == GearSlot.Weapon ? UIKit.Skin.Icon("espada") : null;
                img.enabled = img.sprite != null;
                img.color = new Color(1, 1, 1, 0.25f);
            }
            btn.onClick.AddListener(() =>
            {
                if (g == null) return;
                if (who.locked != null) { toast?.Invoke($"{who.name} está {who.locked}."); return; }
                AudioManager.Play(Sfx.Click);
                Armory.Instance.Unequip(who.gear, slot);
            });
            UIKit.Tip(rt, () => g != null ? g.Tooltip("Clique: guardar no Arsenal")
                                          : $"{GearDef.SlotName(slot)}: vazio.\nClique numa peça do Arsenal para equipar.");
            var label = CrewCards.Label(col, GearDef.SlotName(slot), 12, UIKit.MutedText, 72, 18);
            label.alignment = TextAnchor.MiddleCenter;
        }

        void Piece(GearDef def, int count, Person who)
        {
            var rt = UIKit.Rect("Peca", grid);
            var (btn, _) = UIKit.ButtonBase(rt);
            var icon = UIKit.Rect("Icone", rt);
            icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = new Vector2(0, 3);
            icon.sizeDelta = new Vector2(36, 36);
            var img = icon.gameObject.AddComponent<Image>();
            img.sprite = def.icon;
            img.preserveAspect = true;
            img.raycastTarget = false;
            RarityBar(rt, def.Color);
            if (count > 1)
            {
                var n = UIKit.Text(rt, "x" + count, 11, TextAnchor.UpperRight, UIKit.ButtonText);
                n.rectTransform.anchorMin = Vector2.zero; n.rectTransform.anchorMax = Vector2.one;
                n.rectTransform.offsetMin = new Vector2(2, 2); n.rectTransform.offsetMax = new Vector2(-5, -3);
                n.raycastTarget = false;
            }
            btn.onClick.AddListener(() =>
            {
                var armory = Armory.Instance;
                if (sellMode)
                {
                    if (armory.Sell(def)) toast?.Invoke($"Vendeu {def.displayName} por {def.SellPrice} ouro.");
                    return;
                }
                if (who.locked != null) { toast?.Invoke($"{who.name} está {who.locked}."); return; }
                AudioManager.Play(Sfx.Click);
                armory.Equip(who.gear, def);
            });
            UIKit.Tip(rt, () => def.Tooltip(sellMode ? $"Clique: vender por {def.SellPrice} ouro" : $"Clique: equipar em {who.name}"));
        }

        static void RarityBar(RectTransform parent, Color c)
        {
            var bar = UIKit.Rect("Raridade", parent);
            bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(1, 0); bar.pivot = new Vector2(0.5f, 0);
            bar.offsetMin = new Vector2(8, 8); bar.offsetMax = new Vector2(-8, 13);
            var img = bar.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
        }
    }
}
