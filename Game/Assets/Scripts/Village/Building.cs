using System.Collections.Generic;
using UnityEngine;

namespace Pixel.Village
{
    /// <summary>
    /// Uma construção colocada na vila. Prédios de produção só trabalham com tripulantes
    /// no posto: a velocidade depende do ofício, nível, traços e moral de quem está lá.
    /// </summary>
    public class Building : MonoBehaviour
    {
        public BuildingDef def;
        public Vector2Int origin;

        /// <summary>Tripulantes designados para trabalhar aqui.</summary>
        public readonly List<CrewMember> Workers = new List<CrewMember>();

        float timer;
        bool waitingWarned;

        public float Progress => def != null && def.cycleSeconds > 0 ? timer / def.cycleSeconds : 0f;
        public string Status { get; private set; } = "";
        public float CurrentSpeed { get; private set; }

        /// <summary>Ponto onde visitantes e trabalhadores param (em frente à construção).</summary>
        public Vector2 Door => (Vector2)transform.position + Vector2.down * 0.4f;

        public bool HasInput(VillageManager vm) =>
            def.inputItem == null || vm.GetStock(def.inputItem) >= def.inputAmount;

        void Update()
        {
            if (def == null || def.kind != BuildingKind.Producer || def.outputItem == null) return;
            var vm = VillageManager.Instance;
            if (vm == null) return;

            CurrentSpeed = 0f;
            foreach (var w in Workers)
                if (w.agent != null && w.agent.AtWork) CurrentSpeed += w.EfficiencyAt(def);

            if (Workers.Count == 0) { Status = "Sem trabalhador: contrate na Taverna"; return; }
            if (CurrentSpeed <= 0f) { Status = "Trabalhador a caminho..."; return; }

            if (!HasInput(vm))
            {
                Status = $"Parado: falta {def.inputItem.displayName}";
                if (!waitingWarned)
                {
                    FloatingText.Spawn(TopOfSprite(), $"Falta {def.inputItem.displayName}", new Color(1f, 0.6f, 0.5f));
                    waitingWarned = true;
                }
                return;
            }
            waitingWarned = false;
            Status = $"Produzindo ({CurrentSpeed * 100f:0}% de velocidade)";

            timer += Time.deltaTime * CurrentSpeed;
            if (timer < def.cycleSeconds) return;
            timer = 0f;

            if (def.inputItem != null && !vm.TryTakeStock(def.inputItem, def.inputAmount)) return;
            vm.AddStock(def.outputItem, def.outputAmount);
            FloatingText.Spawn(TopOfSprite(), $"+{def.outputAmount} {def.outputItem.displayName}", def.outputItem.textColor);
            var cm = CrewManager.Instance;
            if (cm != null) foreach (var w in Workers.ToArray()) cm.GiveWorkXp(w, this);
        }

        public Vector3 TopOfSprite()
        {
            var sr = GetComponent<SpriteRenderer>();
            return sr != null ? new Vector3(sr.bounds.center.x, sr.bounds.max.y, 0) : transform.position + Vector3.up * 2f;
        }
    }
}
