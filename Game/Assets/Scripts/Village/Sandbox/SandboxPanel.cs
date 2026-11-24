using System;
using Pixel.Combat;
using Pixel.Ship;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>
    /// Painel de truques do Modo Sandbox (tecla F1). Aparece no HUD da vila e no do mar;
    /// os botões mudam conforme o lugar. Só funciona com o Sandbox ligado.
    /// </summary>
    public class SandboxPanel : MonoBehaviour
    {
        RectTransform window, grid, badge;
        Text badgeText;
        bool atSea;
        Func<string, bool> showMessage;

        /// <param name="atSea">Painel do HUD do mar (truques do navio e das ilhas).</param>
        /// <param name="message">Como mostrar o resultado de um truque (aviso do HUD).</param>
        public static SandboxPanel Create(Transform canvas, bool atSea, Func<string, bool> message)
        {
            var holder = UIKit.Rect("Sandbox", canvas);
            UIKit.Stretch(holder);
            var p = holder.gameObject.AddComponent<SandboxPanel>();
            p.atSea = atSea;
            p.showMessage = message;

            // Selo no canto: lembra que é Sandbox e como abrir o painel
            p.badge = UIKit.Panel("SeloSandbox", holder, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, atSea ? 12 : 104), new Vector2(190, 30),
                new Color(0.55f, 0.15f, 0.55f, 0.92f));
            var bb = p.badge.gameObject.AddComponent<Button>();
            bb.targetGraphic = p.badge.GetComponent<Image>();
            bb.onClick.AddListener(p.Toggle);
            p.badgeText = UIKit.Text(p.badge, "", 14, TextAnchor.MiddleCenter, Color.white);
            UIKit.Stretch(p.badgeText.rectTransform, 4);

            var (win, content) = UIKit.Window("JanelaSandbox", holder, "Modo Sandbox (testes)", new Vector2(560, 470), p.Hide);
            p.window = win;
            var info = UIKit.Text(content, "Nada é salvo neste modo. F1 abre e fecha este painel.", 13, TextAnchor.UpperLeft, UIKit.MutedText);
            var irt = info.rectTransform;
            irt.anchorMin = new Vector2(0, 1); irt.anchorMax = new Vector2(1, 1); irt.pivot = new Vector2(0.5f, 1);
            irt.sizeDelta = new Vector2(0, 22); irt.anchoredPosition = Vector2.zero;
            p.grid = UIKit.Rect("Botoes", content);
            p.grid.anchorMin = Vector2.zero; p.grid.anchorMax = Vector2.one;
            p.grid.offsetMin = Vector2.zero; p.grid.offsetMax = new Vector2(0, -28);
            var g = p.grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(260, 40);
            g.spacing = new Vector2(12, 8);
            win.gameObject.SetActive(false);
            return p;
        }

        public bool IsOpen => window != null && window.gameObject.activeSelf;
        public void Hide() => window.gameObject.SetActive(false);

        public void Toggle()
        {
            if (!SandboxMode.Active) return;
            bool open = !window.gameObject.activeSelf;
            window.gameObject.SetActive(open);
            if (open) { window.SetAsLastSibling(); Rebuild(); }
        }

        void Update()
        {
            badge.gameObject.SetActive(SandboxMode.Active);
            if (!SandboxMode.Active) { if (IsOpen) Hide(); return; }
            badgeText.text = $"SANDBOX · {SandboxMode.Speed:0}x · F1";
            SandboxMode.ApplySpeed();
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame && !GameMenu.IsOpen) Toggle();
        }

        void Say(string msg) => showMessage?.Invoke(msg);

        void Btn(string label, Action action)
        {
            UIKit.Button(grid, label, () => { action(); AudioManager.Play(Sfx.Click); }, 15);
        }

        void Rebuild()
        {
            UIKit.Clear(grid);
            var vm = VillageManager.Instance;
            var cm = CrewManager.Instance;

            Btn("+1.000 ouro", () => { vm?.AddGold(1000); Say("+1.000 ouro"); });
            Btn("+10.000 ouro", () => { vm?.AddGold(10000); Say("+10.000 ouro"); });
            Btn("+20 de cada item", () =>
            {
                if (vm == null) return;
                foreach (var it in vm.items) if (it != null) vm.AddStock(it, 20);
                Say("+20 rum, peixe e cana");
            });
            Btn($"Velocidade: {SandboxMode.Speed:0}x (trocar)", () => { float s = SandboxMode.NextSpeed(); Say($"Velocidade {s:0}x"); Rebuild(); });

            if (!atSea)
            {
                Btn("Novo tripulante (nível 3)", () => { var m = cm?.SandboxAddCrew(3); if (m != null) Say($"{m.name} entrou para o bando"); });
                Btn("Tripulação +1 nível", () => { cm?.SandboxBoostCrew(true, false); Say("Todos subiram de nível"); });
                Btn("Moral 100 para todos", () => { cm?.SandboxBoostCrew(false, true); Say("Tripulação animada!"); });
                Btn("Terminar o dia agora", () => { cm?.SandboxEndDay(); Say("O dia está acabando..."); });
                Btn("Concluir a crônica atual", () =>
                {
                    var ch = ChronicleManager.Instance;
                    if (ch == null || ch.AllDone) { Say("Todas as crônicas já foram concluídas."); return; }
                    string t = ch.Active.title;
                    ch.SandboxSkip();
                    Say($"Crônica concluída: {t}");
                });
                Btn("Liberar todas as masmorras", () => { ExpeditionManager.Instance?.SandboxUnlockAll(); Say("Masmorras liberadas"); });
            }
            else
            {
                var sea = SeaController.Instance;
                var dir = EncounterDirector.Instance;
                Btn("Consertar o navio", () => { sea?.SandboxRepair(); Say("Casco novinho!"); });
                Btn("Curar todo mundo", () => { sea?.SandboxHealAll(); Say("Todos curados"); });
                Btn("Revelar o mapa inteiro", () => { sea?.SandboxRevealMap(); Say("Mapa revelado (abra pelo Zarpar)"); });
                Btn("Chegar ao destino agora", () => Say(sea != null && sea.SandboxArrive() ? "Chegando!" : "Só funciona navegando."));
                Btn("Navio inimigo agora", () => Say(dir != null && dir.SandboxEncounter() ? "Navio à vista!" : "Só funciona navegando, com o mar livre."));
                Btn("Vencer o combate", () => Say(dir != null && dir.SandboxWin() ? "Vitória!" : "Não há combate agora."));
                Btn("Derrubar inimigos da ilha", () =>
                {
                    int n = sea != null && sea.islands != null ? sea.islands.SandboxKillAll() : 0;
                    Say(n > 0 ? $"{n} inimigo(s) derrubado(s)" : "Não há inimigos aqui.");
                });
            }
        }
    }
}
