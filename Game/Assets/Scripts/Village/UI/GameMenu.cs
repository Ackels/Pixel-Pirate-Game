using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Pixel.Village
{
    /// <summary>
    /// Menu inicial (Continuar / Novo jogo / Sair) e menu de pausa (Esc: Continuar / Salvar / Sair para o menu).
    /// O jogo fica pausado enquanto um deles está aberto.
    /// </summary>
    public class GameMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        RectTransform box;
        Text title, subtitle;
        bool confirmNew;

        public static GameMenu Create(Transform canvas)
        {
            var root = UIKit.Rect("Menu", canvas);
            UIKit.Stretch(root);
            root.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.08f, 0.12f, 0.82f);
            var menu = root.gameObject.AddComponent<GameMenu>();

            menu.box = UIKit.Panel("Caixa", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 0),
                new Color(UIKit.PanelColor.r, UIKit.PanelColor.g, UIKit.PanelColor.b, 1f));
            var v = UIKit.Vertical(menu.box.gameObject, 22, 10);
            v.childAlignment = TextAnchor.UpperCenter;
            menu.box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            root.gameObject.SetActive(false);
            return menu;
        }

        void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            IsOpen = true;
            Time.timeScale = 0f;
            confirmNew = false;
        }

        void Close()
        {
            gameObject.SetActive(false);
            IsOpen = false;
            Time.timeScale = 1f;
        }

        void OnDestroy()
        {
            if (IsOpen) { IsOpen = false; Time.timeScale = 1f; }
        }

        void Header(string t, string sub)
        {
            UIKit.Clear(box);
            title = UIKit.Text(box, t, 30, TextAnchor.MiddleCenter, UIKit.GoldColor);
            UIKit.Size(title, height: 44);
            subtitle = UIKit.Text(box, sub, 15, TextAnchor.MiddleCenter, UIKit.MutedText);
            UIKit.Size(subtitle, height: 40);
        }

        void Btn(string label, UnityEngine.Events.UnityAction a, bool interactable = true)
        {
            var (b, _, _) = UIKit.Button(box, label, a, 18);
            b.interactable = interactable;
            UIKit.Size(b, height: 48);
        }

        // ---------- Menu inicial ----------
        public void OpenStart() => ShowStart(false);

        void ShowStart(bool confirming)
        {
            Open();
            confirmNew = confirming;
            var save = SaveSystem.Peek();
            string sub = confirming ? "Começar do zero vai APAGAR o jogo salvo."
                : save != null ? $"Jogo salvo: Dia {save.day} · {save.gold} ouro · {save.crew.Count} tripulantes\n(salvo em {save.savedAt})"
                : "Construa sua vila pirata, monte seu navio\ne domine os mares.";
            Header("Enseada dos Corsários", sub);
            if (save != null) Btn("Continuar", Continue);
            Btn(confirming ? "Sim, apagar e começar do zero" : "Novo jogo", NewGame);
            if (confirming) Btn("Não, voltar", () => ShowStart(false));
            Btn("Sair", Quit);
        }

        void Continue()
        {
            Close();
            if (GameFlow.Instance != null) GameFlow.Instance.SavingEnabled = true;
            if (!SaveSystem.Load()) VillageHUD.Instance?.Toast("Não consegui carregar o jogo salvo.");
            else VillageHUD.Instance?.Toast("Bem-vindo de volta, capitão!");
        }

        void NewGame()
        {
            if (SaveSystem.Exists && !confirmNew) { ShowStart(true); return; }
            SaveSystem.Delete();
            Close();
            if (GameFlow.Instance != null) GameFlow.Instance.SavingEnabled = true;
            VillageHUD.Instance?.Toast("Novo jogo! Comece construindo uma Casa.");
        }

        // ---------- Pausa ----------
        public void OpenPause()
        {
            Open();
            Header("Pausa", $"Arquivo de save:\n{SaveSystem.FilePath}");
            subtitle.fontSize = 12;
            Btn("Continuar", Close);
            Btn("Salvar agora", () =>
            {
                bool ok = SaveSystem.Save();
                Header("Pausa", ok ? "Jogo salvo!" : "Não deu para salvar agora.");
                Btn("Continuar", Close);
                Btn("Sair para o menu", BackToMenu);
                Btn("Sair do jogo", Quit);
            }, SaveSystem.CanSaveNow);
            Btn(AudioManager.SoundOn ? "Som: ligado" : "Som: desligado", () => { AudioManager.SoundOn = !AudioManager.SoundOn; OpenPause(); });
            Btn("Sair para o menu", BackToMenu);
            Btn("Sair do jogo", Quit);
        }

        void BackToMenu()
        {
            SaveSystem.Save();
            Close();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void Quit()
        {
            if (GameFlow.Instance != null && GameFlow.Instance.SavingEnabled) SaveSystem.Save();
            Close();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
