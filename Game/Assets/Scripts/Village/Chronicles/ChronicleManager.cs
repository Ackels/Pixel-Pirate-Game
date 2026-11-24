using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pixel.Village
{
    /// <summary>Uma missão das Crônicas: título, o que fazer, recompensa e a condição para concluir.</summary>
    public class Chronicle
    {
        public string title;
        public string goal;
        public string hint;
        public int reward;
        public Func<bool> done;
        public Func<string> progress;   // ex.: "2/3" (opcional)
    }

    /// <summary>
    /// Crônicas: missões-guia em sequência que ensinam o jogo inteiro (vila → tripulação → navio →
    /// mar → ilhas → expedições → Capitão Lendário). Cada uma dá ouro ao ser concluída.
    /// </summary>
    public class ChronicleManager : MonoBehaviour
    {
        public static ChronicleManager Instance { get; private set; }

        public List<Chronicle> Chronicles { get; } = new List<Chronicle>();
        public int Current { get; private set; }
        public bool AllDone => Current >= Chronicles.Count;
        public Chronicle Active => AllDone ? null : Chronicles[Current];
        public event Action<Chronicle> Completed;
        public event Action Changed;

        float checkTimer;

        void Awake()
        {
            Instance = this;
            GameStats.Reset();   // contadores começam do zero a cada jogo (o save os recarrega)
            Build();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        static VillageManager VM => VillageManager.Instance;
        static CrewManager CM => CrewManager.Instance;
        static bool Has(BuildingKind kind) => VM != null && VM.FindNearest(kind, Vector2.zero) != null;
        static bool HasNamed(string defName)
        {
            if (VM == null) return false;
            foreach (var b in VM.Buildings) if (b.def.name == defName) return true;
            return false;
        }
        static int Stock(string item)
        {
            if (VM == null) return 0;
            foreach (var i in VM.items) if (i != null && i.displayName == item) return VM.GetStock(i);
            return 0;
        }
        static bool ShipHas(Pixel.Ship.BlockRole role)
        {
            var f = GameFlow.Instance;
            if (f == null) return false;
            foreach (var b in f.Ship.Blocks.Values) if (b.role == role) return true;
            return false;
        }
        static bool Cleared(int index)
        {
            var em = ExpeditionManager.Instance;
            return em != null && index < em.dungeons.Length && em.IsCleared(em.dungeons[index]);
        }

        void Add(string title, string goal, string hint, int reward, Func<bool> done, Func<string> progress = null) =>
            Chronicles.Add(new Chronicle { title = title, goal = goal, hint = hint, reward = reward, done = done, progress = progress });

        void Build()
        {
            Add("Um teto para o bando", "Construa uma Casa.",
                "Clique no martelo (B), escolha Casa e depois um lugar verde da vila.", 30, () => Has(BuildingKind.House));
            Add("Plantar o futuro", "Construa uma Horta.",
                "A Horta produz cana. O Agricultor vai trabalhar nela sozinho.", 30, () => HasNamed("Horta"));
            Add("Peixe na mesa", "Construa uma Cabana de Pesca.",
                "Ela precisa encostar na água. Peixe é a comida da tripulação.", 40, () => HasNamed("CabanaDePesca"));
            Add("O espírito da Enseada", "Construa uma Destilaria.",
                "Ela transforma 2 Cana em 1 Rum. Rum vende bem e alegra o bando.", 50, () => HasNamed("Destilaria"));
            Add("Comércio pirata", "Construa um Mercado e venda 5 itens aos visitantes.",
                "Visitantes chegam pelas docas e compram no Mercado.", 50,
                () => Has(BuildingKind.Market) && GameStats.Get(GameStats.Sales) >= 5,
                () => $"{Mathf.Min(5, GameStats.Get(GameStats.Sales))}/5 vendas");
            Add("Mais braços", "Construa uma Taverna e contrate 1 tripulante.",
                "Clique na Taverna para ver os recrutas do dia. Cada um precisa de uma cama.", 60,
                () => GameStats.Get(GameStats.Hires) >= 1);
            Add("Bebida da casa", "Tenha 5 Rum no estoque.",
                "Destilaria com um Destilador trabalhando + Horta abastecendo.", 60,
                () => Stock("Rum") >= 5, () => $"{Mathf.Min(5, Stock("Rum"))}/5 rum");
            Add("Todo capitão precisa de um navio", "Construa o Estaleiro.",
                "O Estaleiro libera a edição do navio nas docas.", 60, () => Has(BuildingKind.Shipyard));
            Add("Ao mar!", "Embarque no navio.",
                "Vá até o fim do píer (lá embaixo) e aperte E.", 40, () => GameStats.Get(GameStats.Voyages) >= 1);
            Add("Dentes de ferro", "Coloque um Canhão no navio.",
                "Nas docas, clique em Editar navio. O canhão precisa de piso embaixo.", 60, () => ShipHas(Pixel.Ship.BlockRole.Cannon));
            Add("Terra à vista!", "Visite uma ilha.",
                "Zarpar abre o mapa. Leve peixe para a viagem!", 60, () => GameStats.Get(GameStats.IslandsVisited) >= 1);
            Add("Primeiro saque", "Vença um combate no mar.",
                "Aborde um navio (botão Abordar!) ou afunde-o com os canhões.", 80, () => GameStats.Get(GameStats.SeaVictories) >= 1);
            Add("Caçador de tesouros", "Abra 3 baús em ilhas.",
                "Chegue perto do baú e aperte E.", 80,
                () => GameStats.Get(GameStats.Chests) >= 3, () => $"{Mathf.Min(3, GameStats.Get(GameStats.Chests))}/3 baús");
            Add("Aventureiros", "Construa o Quadro de Expedições e conclua a Caverna dos Caranguejos.",
                "Tecla X: monte um grupo de até 4 e leve rum como curativo.", 100, () => Cleared(0));
            Add("A lenda dos mares", "Derrote o Capitão Lendário no Covil.",
                "Conclua as masmorras em ordem. Tripulantes de nível 4 e bastante rum!", 500, () => Cleared(3));
        }

        void Update()
        {
            if (AllDone || GameMenu.IsOpen) return;
            checkTimer -= Time.unscaledDeltaTime;
            if (checkTimer > 0) return;
            checkTimer = 0.5f;
            var c = Active;
            if (c == null || !c.done()) return;

            VM?.AddGold(c.reward);
            CM?.Report($"Crônica concluída: \"{c.title}\" (+{c.reward} ouro)", new Color(1f, 0.85f, 0.3f));
            AudioManager.Play(Sfx.Quest);
            Current++;
            Completed?.Invoke(c);
            VillageHUD.Instance?.Toast($"Crônica concluída: {c.title}! +{c.reward} ouro");
            Pixel.Ship.SeaController.Instance?.Say($"Crônica concluída: {c.title}! +{c.reward} ouro");
            if (AllDone) CM?.Report("Todas as Crônicas concluídas! Você é a lenda dos sete mares.", new Color(1f, 0.85f, 0.3f));
            Changed?.Invoke();
        }

        /// <summary>Sandbox: conclui a crônica atual na hora (com a recompensa).</summary>
        public void SandboxSkip()
        {
            if (AllDone) return;
            var c = Active;
            VM?.AddGold(c.reward);
            Current++;
            Completed?.Invoke(c);
            Changed?.Invoke();
        }

        public void RestoreFromSave(int index)
        {
            Current = Mathf.Clamp(index, 0, Chronicles.Count);
            Changed?.Invoke();
        }
    }
}
