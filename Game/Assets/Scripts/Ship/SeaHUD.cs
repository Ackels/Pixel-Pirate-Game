using Pixel.Combat;
using Pixel.Village;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Pixel.Ship
{
    /// <summary>Interface da cena do Mar: ouro, atributos do navio, botões de ação e paleta do estaleiro.</summary>
    public class SeaHUD : MonoBehaviour
    {
        public SeaController sea;

        Text goldText, titleText, statsText, hintText, helpText;
        RectTransform actionBar;
        SeaMode builtFor = (SeaMode)(-1);
        EncounterState builtEncounter = (EncounterState)(-1);
        Text enemyText;
        string message;
        float messageTimer;

        void Start()
        {
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("Canvas_Mar", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            var top = UIKit.Panel("Topo", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -12), new Vector2(260, 64));
            goldText = UIKit.Text(top, "", 18, TextAnchor.MiddleLeft, UIKit.GoldColor);
            UIKit.Stretch(goldText.rectTransform, 10);

            var titlePanel = UIKit.Panel("Titulo", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(420, 40));
            titleText = UIKit.Text(titlePanel, "", 22, TextAnchor.MiddleCenter, UIKit.GoldColor);
            UIKit.Stretch(titleText.rectTransform, 6);

            var enemy = UIKit.Panel("Inimigo", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -58), new Vector2(420, 54),
                new Color(0.35f, 0.1f, 0.08f, 0.9f));
            enemyText = UIKit.Text(enemy, "", 16, TextAnchor.MiddleCenter);
            UIKit.Stretch(enemyText.rectTransform, 6);

            var stats = UIKit.Panel("Atributos", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -12), new Vector2(260, 230));
            statsText = UIKit.Text(stats, "", 16, TextAnchor.UpperLeft);
            UIKit.Stretch(statsText.rectTransform, 10);

            var help = UIKit.Panel("Ajuda", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -84), new Vector2(260, 52),
                new Color(0.12f, 0.08f, 0.05f, 0.75f));
            helpText = UIKit.Text(help, "", 14, TextAnchor.MiddleLeft, UIKit.MutedText);
            UIKit.Stretch(helpText.rectTransform, 8);

            actionBar = UIKit.Panel("Acoes", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(0, 84));
            var layout = UIKit.Horizontal(actionBar.gameObject, 8, 8);
            layout.childAlignment = TextAnchor.MiddleCenter;
            actionBar.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var hintPanel = UIKit.Panel("Dica", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 104), new Vector2(760, 34));
            hintText = UIKit.Text(hintPanel, "", 16, TextAnchor.MiddleCenter);
            UIKit.Stretch(hintText.rectTransform, 6);

            // No estaleiro a barra só é refeita ao trocar de bloco (para não "engolir" cliques).
            sea.Changed += () => { if (sea.Mode != SeaMode.Editing) Refresh(); };
            sea.Message += m => { message = m; messageTimer = 4f; };
            Refresh();
        }

        void Update()
        {
            if (sea == null) return;
            var director = EncounterDirector.Instance;
            var est = director != null ? director.State : EncounterState.None;
            if (builtFor != sea.Mode || builtEncounter != est) Refresh();

            enemyText.transform.parent.gameObject.SetActive(est != EncounterState.None);
            if (director != null && est != EncounterState.None)
            {
                string crewInfo = est == EncounterState.Approaching ? "" : $" · Tripulação: {director.EnemyCrewAlive}";
                enemyText.text = UIKit.Colored(director.EnemyName, UIKit.GoldColor) + $"\nCasco: {director.EnemyHp}/{director.EnemyMaxHp}{crewInfo}";
            }

            if (sea.Mode == SeaMode.Editing && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (sea.editor.Selected != null) { sea.editor.Selected = null; Refresh(); }
                else sea.StopEditing();
            }

            messageTimer -= Time.unscaledDeltaTime;
            string hint = messageTimer > 0 ? message : sea.Mode == SeaMode.Editing ? sea.editor.Hint : null;
            hintText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(hint));
            hintText.text = hint;

            var cm = CrewManager.Instance;
            goldText.text = $"Ouro: {sea.Gold}" + (cm != null ? $"\nDia {cm.Day} na vila" : "");
            UpdateStats();
        }

        void UpdateStats()
        {
            var s = sea.Stats;
            var cap = sea.captainCombatant;
            string speed = s.speedKnots > 0 ? $"{s.speedKnots:0.0} nós" : UIKit.Colored("parado (sem velas)", new Color(1f, 0.5f, 0.45f));
            statsText.text =
                UIKit.Colored("Navio", UIKit.GoldColor) + "\n" +
                $"Casco: {Hp(sea.ShipHp, s.hp)}\n" +
                (cap != null ? $"Capitão: {Hp(cap.hp, cap.maxHp)}\n" : "") +
                $"A bordo: {sea.CrewFighters.Count} de {Mathf.Max(0, s.MaxCrew - 1)} vagas\n" +
                $"Velocidade: {speed}\n" +
                $"Tripulação máx.: {s.MaxCrew}\n" +
                $"Canhões: {s.cannons}\n" +
                $"Blocos: {s.blocks} · Valor: {s.value} ouro" +
                (sea.Mode == SeaMode.Sailing ? $"\n\nDistância: {sea.DistanceMiles:0.0} milhas" : "");
        }

        static string Hp(int cur, int max)
        {
            var c = cur > max * 0.6f ? new Color(0.55f, 0.95f, 0.5f) : cur > max * 0.3f ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.45f, 0.4f);
            return UIKit.Colored($"{cur}/{max}", c);
        }

        void Refresh()
        {
            if (actionBar == null) return;
            builtFor = sea.Mode;
            var director = EncounterDirector.Instance;
            builtEncounter = director != null ? director.State : EncounterState.None;
            UIKit.Clear(actionBar);

            switch (sea.Mode)
            {
                case SeaMode.Docked:
                    titleText.text = "Atracado na Enseada";
                    helpText.text = "A/D ou setas: andar · Espaço: pular";
                    bool canEdit = sea.CanEdit(out string editWhy);
                    ActionButton(canEdit ? "Editar navio\n(Estaleiro)" : "Editar navio\n(precisa de Estaleiro)", sea.StartEditing, canEdit);
                    bool canSail = sea.CanSail(out _);
                    if (sea.RepairCost > 0)
                        ActionButton($"Consertar casco\n({sea.RepairCost} ouro)", sea.Repair, sea.Gold >= sea.RepairCost);
                    ActionButton("Zarpar!", sea.SetSail, canSail);
                    ActionButton("Voltar à vila", sea.GoHome, true);
                    break;

                case SeaMode.Editing:
                    titleText.text = "Estaleiro — editando o navio";
                    helpText.text = "Clique: colocar · Botão direito: remover\nEsc: soltar bloco / sair";
                    foreach (var def in sea.Palette)
                    {
                        var d = def;
                        bool selected = sea.editor.Selected == d;
                        PaletteButton(d, selected, () =>
                        {
                            sea.editor.Selected = sea.editor.Selected == d ? null : d;
                            Refresh();
                        });
                    }
                    ActionButton("Concluir", sea.StopEditing, true);
                    break;

                case SeaMode.Sailing:
                    bool fighting = builtEncounter != EncounterState.None;
                    titleText.text = fighting ? "Combate!" : "Navegando";
                    helpText.text = "A/D: andar · Espaço: pular\nF / J / clique: golpear com a espada";
                    if (director != null && director.CanBoard) ActionButton("Abordar!", director.Board, true);
                    if (director != null && director.CanFlee) ActionButton("Fugir", director.Flee, true);
                    ActionButton("Voltar à Enseada", sea.GoHome, !fighting);
                    break;
            }
        }

        void ActionButton(string label, UnityEngine.Events.UnityAction onClick, bool interactable)
        {
            var (btn, _, _) = UIKit.Button(actionBar, label, onClick, 16);
            btn.interactable = interactable;
            UIKit.Size(btn, 170, 64);
        }

        void PaletteButton(ShipBlockDef def, bool selected, UnityEngine.Events.UnityAction onClick)
        {
            var rt = UIKit.Rect("Bloco", actionBar);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = selected ? UIKit.ButtonSelected : UIKit.ButtonColor;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(onClick);
            UIKit.Size(rt, 118, 64);
            var l = UIKit.Horizontal(rt.gameObject, 5, 4);
            l.childAlignment = TextAnchor.MiddleCenter;
            var iconRt = UIKit.Rect("Icone", rt);
            var img = iconRt.gameObject.AddComponent<Image>();
            img.sprite = def.sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            UIKit.Size(iconRt, 36, 36);
            var t = UIKit.Text(rt, $"{def.displayName}\n{def.cost} ouro", 14, TextAnchor.MiddleCenter);
            UIKit.Size(t, flexWidth: 1);
        }
    }
}
