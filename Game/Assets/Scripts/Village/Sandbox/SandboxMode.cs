using UnityEngine;

namespace Pixel.Village
{
    /// <summary>
    /// Modo Sandbox (testes): começa com muito ouro, itens e tripulantes, libera o painel de truques (F1)
    /// e NUNCA salva (o save normal fica intacto). Liga pelo menu inicial.
    /// </summary>
    public static class SandboxMode
    {
        public static bool Active { get; private set; }
        /// <summary>Velocidade do jogo no Sandbox (1x, 2x, 4x, 8x).</summary>
        public static float Speed { get; private set; } = 1f;

        static readonly float[] Speeds = { 1f, 2f, 4f, 8f };

        public static void Reset()
        {
            Active = false;
            Speed = 1f;
        }

        /// <summary>Começa um jogo de testes, sem mexer no save.</summary>
        public static void Begin()
        {
            Active = true;
            Speed = 1f;
            if (GameFlow.Instance != null) GameFlow.Instance.SavingEnabled = false;
            var vm = VillageManager.Instance;
            if (vm != null)
            {
                vm.AddGold(10000);
                foreach (var item in vm.items) if (item != null) vm.AddStock(item, 50);
            }
            var cm = CrewManager.Instance;
            if (cm != null) for (int i = 0; i < 4; i++) cm.SandboxAddCrew(3);
        }

        public static float NextSpeed()
        {
            int i = System.Array.IndexOf(Speeds, Speed);
            Speed = Speeds[(i + 1) % Speeds.Length];
            return Speed;
        }

        /// <summary>Aplica a velocidade escolhida (menus pausados ficam pausados).</summary>
        public static void ApplySpeed()
        {
            if (!Active || GameMenu.IsOpen || Pixel.Ship.CrewCommander.Paused || Pixel.Ship.SeaDialog.IsOpen) return;
            if (!Mathf.Approximately(Time.timeScale, Speed)) Time.timeScale = Speed;
        }
    }
}
