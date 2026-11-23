using UnityEngine;

namespace Pixel.Village
{
    /// <summary>
    /// Uma construção colocada na vila. Se for de produção, transforma itens sozinha
    /// (ex.: Horta gera cana; Destilaria transforma 2 cana em 1 rum).
    /// Na Fase 2, a produção passará a depender de tripulantes trabalhando.
    /// </summary>
    public class Building : MonoBehaviour
    {
        public BuildingDef def;
        public Vector2Int origin;

        float timer;
        bool waitingWarned;

        public float Progress => def != null && def.cycleSeconds > 0 ? timer / def.cycleSeconds : 0f;

        /// <summary>Ponto onde os visitantes param (em frente à construção).</summary>
        public Vector2 Door => (Vector2)transform.position + Vector2.down * 0.4f;

        public bool HasInput(VillageManager vm) =>
            def.inputItem == null || vm.GetStock(def.inputItem) >= def.inputAmount;

        void Update()
        {
            if (def == null || def.kind != BuildingKind.Producer || def.outputItem == null) return;
            var vm = VillageManager.Instance;
            if (vm == null) return;

            if (!HasInput(vm))
            {
                if (!waitingWarned)
                {
                    FloatingText.Spawn(TopOfSprite(), $"Falta {def.inputItem.displayName}", new Color(1f, 0.6f, 0.5f));
                    waitingWarned = true;
                }
                return;
            }
            waitingWarned = false;

            timer += Time.deltaTime;
            if (timer < def.cycleSeconds) return;
            timer = 0f;

            if (def.inputItem != null && !vm.TryTakeStock(def.inputItem, def.inputAmount)) return;
            vm.AddStock(def.outputItem, def.outputAmount);
            FloatingText.Spawn(TopOfSprite(), $"+{def.outputAmount} {def.outputItem.displayName}", def.outputItem.textColor);
        }

        Vector3 TopOfSprite()
        {
            var sr = GetComponent<SpriteRenderer>();
            return sr != null ? new Vector3(sr.bounds.center.x, sr.bounds.max.y, 0) : transform.position + Vector3.up * 2f;
        }
    }
}
