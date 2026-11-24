using UnityEngine;

namespace Pixel.Village
{
    /// <summary>
    /// Visitante que chega pelas docas, vai até o Mercado, compra o item mais valioso
    /// que houver no estoque e vai embora. (Fase 1: anda em linha reta, sem desviar.)
    /// </summary>
    public class Visitor : MonoBehaviour
    {
        public float speed = 2.5f;
        public Sprite[] down, up, left, right;
        public Vector2 exitPoint;

        enum State { Going, Leaving }
        State state;
        Building market;
        SpriteRenderer sr;
        float anim;

        public void Init(Building targetMarket, Vector2 exit)
        {
            market = targetMarket;
            exitPoint = exit;
            state = State.Going;
            sr = GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            if (state == State.Going && market == null) state = State.Leaving; // mercado demolido
            Vector2 target = state == State.Going ? market.Door : exitPoint;
            Vector2 pos = transform.position;
            Vector2 delta = target - pos;
            float step = speed * Time.deltaTime;

            if (delta.magnitude <= step)
            {
                transform.position = target;
                if (state == State.Going) { Buy(); state = State.Leaving; }
                else Destroy(gameObject);
                return;
            }

            transform.position = pos + delta.normalized * step;
            Animate(delta);
        }

        void Buy()
        {
            var vm = VillageManager.Instance;
            ItemDef best = null;
            foreach (var item in vm.items)
                if (item != null && item.sellPrice > 0 && vm.GetStock(item) > vm.Reserve(item) && (best == null || item.sellPrice > best.sellPrice))
                    best = item;

            Vector3 head = transform.position + Vector3.up * 1.8f;
            if (best == null)
            {
                FloatingText.Spawn(head, "Nada pra vender...", new Color(0.85f, 0.85f, 0.85f));
                return;
            }
            vm.TryTakeStock(best, 1);
            vm.AddGold(best.sellPrice);
            GameStats.Add(GameStats.Sales);
            AudioManager.Play(Sfx.Coin, 0.6f);
            FloatingText.Spawn(head, $"+{best.sellPrice} ouro ({best.displayName})", new Color(1f, 0.85f, 0.3f));
        }

        void Animate(Vector2 dir)
        {
            Sprite[] set = Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? (dir.x < 0 ? left : right) : (dir.y < 0 ? down : up);
            if (set == null || set.Length == 0 || sr == null) return;
            anim += Time.deltaTime * 7f;
            sr.sprite = CharacterLook.Frame(set, anim);
        }
    }
}
