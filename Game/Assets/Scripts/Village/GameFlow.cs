using System.Collections.Generic;
using Pixel.Ship;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Pixel.Village
{
    /// <summary>
    /// Liga a Vila (vista de cima) ao Mar (visão lateral).
    /// A cena do Mar é carregada "por cima" (aditiva): a vila continua viva e produzindo
    /// enquanto você navega. Também guarda o desenho do navio entre as viagens.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

        public const string SeaScene = "Sea";

        [Header("Vila (desligados enquanto estiver no mar)")]
        public GameObject villageCamera;
        public GameObject villageHUD;
        public GameObject villageLight;
        public GameObject captain;
        public BuildMode buildMode;

        [Header("Docas")]
        [Tooltip("Retângulo do fim do píer onde aparece 'E: embarcar'.")]
        public Rect boardingArea = new Rect(30f, 0f, 3f, 3.5f);
        public Vector2 returnPoint = new Vector2(31.5f, 4f);

        [Header("Navio")]
        public ShipBlockDef[] shipBlocks;
        public ShipBlockDef raftHull, raftMast, raftSail, raftSlope;

        public ShipLayout Ship { get; private set; }
        /// <summary>Mapa-múndi (gerado uma vez por jogo).</summary>
        public WorldMap World { get; private set; }
        [Tooltip("Semente do mapa. 0 = aleatória a cada jogo.")]
        public int worldSeed;
        /// <summary>Semente realmente usada (vai para o save).</summary>
        public int WorldSeedUsed { get; private set; }
        public bool AtSea { get; private set; }
        /// <summary>Dano no casco que continua até ser consertado nas docas.</summary>
        public int ShipDamage { get; set; }
        /// <summary>Tripulantes a bordo nesta viagem.</summary>
        public List<CrewMember> Embarked { get; } = new List<CrewMember>();
        /// <summary>Ouro e itens ganhos na viagem atual (para o resumo na volta).</summary>
        public int VoyageGold { get; set; }

        void Awake()
        {
            Instance = this;
            Ship = ShipLayout.Raft(raftHull, raftMast, raftSail, raftSlope);
            WorldSeedUsed = worldSeed != 0 ? worldSeed : System.Environment.TickCount;
            World = WorldMap.Generate(WorldSeedUsed);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            // Salva sozinho no fim de cada dia (se o capitão estiver na vila)
            if (CrewManager.Instance != null) CrewManager.Instance.DayEnded += () => AutoSave("Fim do dia: jogo salvo.");
        }

        void OnApplicationQuit() { if (SavingEnabled) SaveSystem.Save(); }

        /// <summary>Salva só depois que o jogador escolheu Continuar/Novo jogo no menu inicial.</summary>
        public bool SavingEnabled { get; set; }

        public void AutoSave(string toast)
        {
            if (!SavingEnabled || !SaveSystem.Save()) return;
            VillageHUD.Instance?.Toast(toast);
        }

        void Update()
        {
            if (AtSea || captain == null) return;
            var hud = VillageHUD.Instance;
            Vector2 p = captain.transform.position;
            bool onDock = boardingArea.Contains(p) && (buildMode == null || !buildMode.Active);
            if (hud != null) hud.Prompt = onDock ? "Aperte E para embarcar no navio" : null;
            if (onDock && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) GoToSea();
        }

        /// <summary>Recoloca navio e mapa vindos de um save.</summary>
        public void RestoreFromSave(ShipLayout ship, int damage, int seed)
        {
            Ship = ship;
            ShipDamage = damage;
            WorldSeedUsed = seed;
            World = WorldMap.Generate(seed);
        }

        public bool HasShipyard =>
            VillageManager.Instance != null && VillageManager.Instance.FindNearest(BuildingKind.Shipyard, Vector2.zero) != null;

        public void GoToSea()
        {
            if (AtSea) return;
            AtSea = true;
            if (buildMode != null) buildMode.Cancel();
            SetVillageVisible(false);
            GameStats.Add(GameStats.Voyages);
            AudioManager.Instance?.PlayMusic(true);
            Embarked.Clear();
            VoyageGold = 0;
            var cm = CrewManager.Instance;
            if (cm != null)
            {
                int seats = Mathf.Max(0, Ship.ComputeStats().MaxCrew - 1); // o capitão ocupa uma vaga
                Embarked.AddRange(cm.Embark(seats));
            }
            SceneManager.LoadScene(SeaScene, LoadSceneMode.Additive);
        }

        public void ReturnToVillage()
        {
            if (!AtSea) return;
            AtSea = false;
            var unload = SceneManager.UnloadSceneAsync(SeaScene);
            // A luz da vila só volta depois que a cena do Mar (com a luz dela) sair.
            if (unload != null && villageLight != null)
                unload.completed += _ => { if (!AtSea) villageLight.SetActive(true); };
            if (captain != null)
            {
                captain.transform.position = returnPoint;
                var body = captain.GetComponent<Rigidbody2D>();
                if (body != null) body.position = returnPoint;
            }
            SetVillageVisible(true, includeLight: unload == null);
            AudioManager.Instance?.PlayMusic(false);

            var cm = CrewManager.Instance;
            if (cm != null)
            {
                cm.Disembark(Embarked);
                if (VoyageGold > 0) cm.Report($"De volta do mar com {VoyageGold} ouro de saque!", new Color(1f, 0.85f, 0.3f));
                if (ShipDamage > 0) cm.Report($"O navio voltou avariado ({ShipDamage} de dano). Conserte nas docas.", new Color(1f, 0.65f, 0.4f));
            }
            Embarked.Clear();
            AutoSave("De volta à Enseada: jogo salvo.");
        }

        void SetVillageVisible(bool visible, bool includeLight = true)
        {
            if (villageCamera) villageCamera.SetActive(visible);
            if (villageHUD) villageHUD.SetActive(visible);
            if (villageLight && includeLight) villageLight.SetActive(visible);
            if (captain) captain.SetActive(visible);
            if (buildMode) buildMode.enabled = visible;
        }
    }
}
