using System;
using System.Collections.Generic;
using System.Linq;
using Pixel.Combat;
using UnityEngine;

namespace Pixel.Village
{
    /// <summary>
    /// Arsenal do bando: as peças de equipamento guardadas (de baús, saques, lojas e da Forja) e o que
    /// o capitão está usando. Tripulantes guardam o que usam no próprio <see cref="CrewMember.gear"/>.
    /// Fica no objeto "Vila" (a vila nunca descarrega, então vale também no mar).
    /// </summary>
    public class Armory : MonoBehaviour
    {
        public static Armory Instance { get; private set; }

        [Tooltip("Todas as peças que existem no jogo (para sortear saque e achar pelo nome no save).")]
        public GearDef[] catalog;

        readonly Dictionary<GearDef, int> stock = new Dictionary<GearDef, int>();
        public Loadout Captain { get; } = new Loadout();
        public event Action Changed;

        /// <summary>Vida e dano do capitão sem equipamento (os mesmos do capitão no mar).</summary>
        public const int CaptainBaseHp = 100;
        public static readonly Vector2Int CaptainBaseDamage = new Vector2Int(16, 24);

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (stock.Count == 0 && !Captain.All().Any()) GiveStarterKit();
        }

        /// <summary>Kit de começo: uma pá (para cavar o X), uma adaga e uma bandana.</summary>
        void GiveStarterKit()
        {
            var shovel = Find("Pa");
            if (shovel != null) Captain.weapon = shovel;
            Add(Find("Adaga"), 1, false);
            Add(Find("Bandana"), 1, false);
            Changed?.Invoke();
        }

        // ---------- Estoque ----------
        public GearDef Find(string assetName)
        {
            if (string.IsNullOrEmpty(assetName) || catalog == null) return null;
            foreach (var g in catalog) if (g != null && g.name == assetName) return g;
            return null;
        }

        public int Count(GearDef def) => def != null && stock.TryGetValue(def, out var n) ? n : 0;
        public int Total => stock.Values.Sum();

        /// <summary>Peças guardadas, por tipo (arma, chapéu, amuleto) e da mais rara para a mais comum.</summary>
        public List<(GearDef def, int count)> Owned() =>
            stock.Where(kv => kv.Value > 0 && kv.Key != null)
                 .OrderBy(kv => kv.Key.slot).ThenByDescending(kv => kv.Key.rarity).ThenBy(kv => kv.Key.displayName)
                 .Select(kv => (kv.Key, kv.Value)).ToList();

        public void Add(GearDef def, int n = 1, bool notify = true)
        {
            if (def == null || n <= 0) return;
            stock[def] = Count(def) + n;
            if (notify) Changed?.Invoke();
        }

        bool Take(GearDef def)
        {
            int n = Count(def);
            if (n <= 0) return false;
            if (n == 1) stock.Remove(def); else stock[def] = n - 1;
            return true;
        }

        // ---------- Equipar ----------
        /// <summary>Tira a peça do arsenal e põe na pessoa (a que ela usava volta para o arsenal).</summary>
        public bool Equip(Loadout who, GearDef def)
        {
            if (who == null || def == null || !Take(def)) return false;
            var old = who.Set(def.slot, def);
            if (old != null) Add(old, 1, false);
            Changed?.Invoke();
            return true;
        }

        public void Unequip(Loadout who, GearSlot slot)
        {
            if (who == null) return;
            var old = who.Set(slot, null);
            if (old == null) return;
            Add(old, 1, false);
            Changed?.Invoke();
        }

        /// <summary>Tripulante saiu do bando: o que ele usava volta para o arsenal.</summary>
        public void ReturnGear(CrewMember m)
        {
            if (m == null) return;
            foreach (var g in m.gear.All().ToList()) Add(g, 1, false);
            m.gear = new Loadout();
            Changed?.Invoke();
        }

        /// <summary>Vende uma peça por metade do preço.</summary>
        public bool Sell(GearDef def)
        {
            if (!Take(def)) return false;
            VillageManager.Instance?.AddGold(def.SellPrice);
            AudioManager.Play(Sfx.Coin);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Onde dá para vender: num porto, ou na vila com Mercado construído.</summary>
        public static bool CanSellHere(bool atSea)
        {
            if (atSea)
            {
                var sea = Pixel.Ship.SeaController.Instance;
                return sea != null && sea.Mode == Pixel.Ship.SeaMode.Landed && sea.Location != null && sea.Location.type == Pixel.Ship.IslandType.Port;
            }
            var vm = VillageManager.Instance;
            return vm != null && vm.FindNearest(BuildingKind.Market, Vector2.zero) != null;
        }

        // ---------- Quem está no navio ----------
        static IEnumerable<CrewMember> Aboard()
        {
            var flow = GameFlow.Instance;
            if (flow == null || !flow.AtSea) return Enumerable.Empty<CrewMember>();
            return flow.Embarked;
        }

        /// <summary>Alguém (capitão ou quem está a bordo) leva uma pá?</summary>
        public bool CanDig() => Captain.Digs || Aboard().Any(m => m.gear.Digs);

        /// <summary>Multiplicador do ouro dos saques (amuletos do capitão e de quem está a bordo, até +60%).</summary>
        public float LootFactor()
        {
            float bonus = Captain.Loot + Aboard().Sum(m => m.gear.Loot);
            return 1f + Mathf.Clamp(bonus, 0f, 0.6f);
        }

        // ---------- Sorteio ----------
        /// <summary>
        /// Sorteia uma peça. Quanto maior o perigo (0–5), mais chance de peça boa, rara ou lendária.
        /// forge = só armas e chapéus até Raro (o que a Forja sabe fazer).
        /// </summary>
        public GearDef Roll(int danger, GearRarity min = GearRarity.Comum, bool forge = false, int bonus = 0)
        {
            if (catalog == null || catalog.Length == 0) return null;
            danger = Mathf.Clamp(danger + bonus, 0, 6);
            float wC = Mathf.Max(10f, 60f - danger * 9f), wB = 30f + danger * 3f, wR = 7f + danger * 4f, wL = danger >= 3 ? 1f + (danger - 2) * 1.5f : 0f;
            if (forge) wL = 0f;
            if (min >= GearRarity.Bom) wC = 0f;
            if (min >= GearRarity.Raro) wB = 0f;
            if (min >= GearRarity.Lendario) { wR = 0f; wL = Mathf.Max(wL, 1f); }
            float r = UnityEngine.Random.value * (wC + wB + wR + wL);
            var rarity = r < wC ? GearRarity.Comum : r < wC + wB ? GearRarity.Bom : r < wC + wB + wR ? GearRarity.Raro : GearRarity.Lendario;
            for (; rarity >= GearRarity.Comum; rarity--)
            {
                var pool = catalog.Where(g => g != null && g.rarity == rarity && (!forge || g.slot != GearSlot.Charm)).ToList();
                if (pool.Count > 0) return pool[UnityEngine.Random.Range(0, pool.Count)];
            }
            return null;
        }

        /// <summary>Arma para um inimigo segurar (só o desenho; o dano dele é o de sempre).</summary>
        public Sprite EnemyWeapon(bool pirate)
        {
            if (catalog == null) return null;
            var pool = catalog.Where(g => g != null && g.slot == GearSlot.Weapon && !g.digs
                                          && (pirate ? g.rarity <= GearRarity.Raro : g.rarity == GearRarity.Comum)).ToList();
            return pool.Count > 0 ? pool[UnityEngine.Random.Range(0, pool.Count)].icon : null;
        }

        // ---------- Números de combate ----------
        /// <summary>
        /// Aplica o equipamento num lutador do mar: vida, dano, velocidade e alcance a partir dos valores
        /// sem equipamento (guardados na primeira vez) e a arma na mão.
        /// </summary>
        public static void ApplyTo(Combatant c, Loadout gear)
        {
            if (c == null) return;
            var b = c.GetComponent<GearBase>();
            if (b == null)
            {
                b = c.gameObject.AddComponent<GearBase>();
                b.hp = c.maxHp; b.damage = c.damage; b.cooldown = c.attackCooldown; b.range = c.attackRange;
            }
            gear ??= new Loadout();
            float ratio = c.maxHp > 0 ? (float)c.hp / c.maxHp : 1f;
            c.maxHp = b.hp + gear.Hp;
            if (!c.IsDown) c.hp = Mathf.Clamp(Mathf.RoundToInt(c.maxHp * ratio), 1, c.maxHp);
            c.damage = b.damage + gear.Damage;
            c.attackCooldown = b.cooldown * gear.CooldownFactor;
            c.attackRange = b.range + gear.Range;
            c.RefreshBar();
            HandWeapon.Set(c.gameObject, gear.weapon != null ? gear.weapon.icon : null);
        }

        // ---------- Salvar / carregar ----------
        public void Collect(SaveData d)
        {
            d.gearVersion = 1;
            d.armory = stock.Where(kv => kv.Key != null && kv.Value > 0).Select(kv => new ItemCount { item = kv.Key.name, count = kv.Value }).ToList();
            d.captainGear = Captain.Names();
        }

        public void Restore(SaveData d)
        {
            stock.Clear();
            Captain.weapon = Captain.hat = Captain.charm = null;
            if (d.gearVersion < 1) { GiveStarterKit(); return; }   // save de antes do equipamento: ganha o kit
            if (d.armory != null) foreach (var ic in d.armory) Add(Find(ic.item), ic.count, false);
            if (d.captainGear != null)
                foreach (var n in d.captainGear) { var g = Find(n); if (g != null) Captain.Set(g.slot, g); }
            Changed?.Invoke();
        }

        /// <summary>Sandbox: duas de cada peça.</summary>
        public void SandboxGiveAll()
        {
            if (catalog == null) return;
            foreach (var g in catalog) Add(g, 2, false);
            Changed?.Invoke();
        }
    }

    /// <summary>Números do lutador sem equipamento (para trocar de peça sem acumular bônus).</summary>
    public class GearBase : MonoBehaviour
    {
        public int hp;
        public Vector2Int damage;
        public float cooldown, range;
    }
}
