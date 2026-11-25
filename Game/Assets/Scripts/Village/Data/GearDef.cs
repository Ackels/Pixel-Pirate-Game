using System.Collections.Generic;
using UnityEngine;

namespace Pixel.Village
{
    public enum GearSlot { Weapon, Hat, Charm }
    public enum GearRarity { Comum, Bom, Raro, Lendario }

    /// <summary>
    /// Uma peça de equipamento (arma, chapéu ou amuleto). Vai no capitão ou num tripulante e muda os números
    /// de combate (no mar, nas ilhas e nas expedições). Edite os valores no Inspector (Assets/Data/Equipamento).
    /// </summary>
    [CreateAssetMenu(menuName = "Pixel/Equipamento", fileName = "Equipamento")]
    public class GearDef : ScriptableObject
    {
        public string displayName = "Equipamento";
        public Sprite icon;
        public GearSlot slot;
        public GearRarity rarity;
        [Tooltip("Preço numa loja. Vender rende metade.")]
        public int price = 30;

        [Header("Números")]
        [Tooltip("Somado ao dano do golpe (mínimo, máximo).")]
        public Vector2Int damage;
        [Tooltip("Somado à vida.")]
        public int hp;
        [Tooltip("Golpes mais rápidos: 0,15 = 15% mais rápido (negativo = mais lento).")]
        public float speed;
        [Tooltip("Alcance extra do golpe, em blocos.")]
        public float range;
        [Tooltip("Ouro extra nos saques: 0,1 = +10%.")]
        public float loot;
        [Tooltip("Serve para cavar tesouro enterrado.")]
        public bool digs;
        [TextArea] public string flavor;

        public static string SlotName(GearSlot s) => s switch
        {
            GearSlot.Weapon => "Arma",
            GearSlot.Hat => "Chapéu",
            _ => "Amuleto",
        };

        public static string RarityName(GearRarity r) => r switch
        {
            GearRarity.Bom => "Bom",
            GearRarity.Raro => "Raro",
            GearRarity.Lendario => "Lendário",
            _ => "Comum",
        };

        public static Color RarityColor(GearRarity r) => r switch
        {
            GearRarity.Bom => new Color(0.45f, 0.85f, 0.45f),
            GearRarity.Raro => new Color(0.45f, 0.7f, 1f),
            GearRarity.Lendario => new Color(1f, 0.72f, 0.25f),
            _ => new Color(0.85f, 0.8f, 0.72f),
        };

        public Color Color => RarityColor(rarity);
        public int SellPrice => Mathf.Max(1, price / 2);

        /// <summary>Linhas com o que a peça faz (só o que não é zero).</summary>
        public List<string> StatLines()
        {
            var l = new List<string>();
            if (damage != Vector2Int.zero) l.Add(damage.x == damage.y ? $"+{damage.x} de dano" : $"+{damage.x}–{damage.y} de dano");
            if (hp != 0) l.Add($"{(hp > 0 ? "+" : "")}{hp} de vida");
            if (Mathf.Abs(speed) > 0.001f) l.Add($"{(speed > 0 ? "+" : "")}{Mathf.RoundToInt(speed * 100)}% de velocidade de golpe");
            if (Mathf.Abs(range) > 0.001f) l.Add($"+{range:0.#} bloco de alcance");
            if (Mathf.Abs(loot) > 0.001f) l.Add($"+{Mathf.RoundToInt(loot * 100)}% de ouro nos saques");
            if (digs) l.Add("Serve para cavar tesouro (G)");
            return l;
        }

        /// <summary>Texto da dica do mouse: nome colorido, tipo, raridade e números.</summary>
        public string Tooltip(string extra = null)
        {
            var s = UIKit.Colored(displayName, Color) + $"\n{SlotName(slot)} · {RarityName(rarity)}";
            foreach (var line in StatLines()) s += "\n" + line;
            if (!string.IsNullOrEmpty(flavor)) s += "\n" + UIKit.Colored(flavor, UIKit.MutedText);
            if (!string.IsNullOrEmpty(extra)) s += "\n" + extra;
            return s;
        }
    }

    /// <summary>O que uma pessoa está usando: uma arma, um chapéu e um amuleto.</summary>
    public class Loadout
    {
        public GearDef weapon, hat, charm;

        public GearDef Get(GearSlot s) => s switch { GearSlot.Weapon => weapon, GearSlot.Hat => hat, _ => charm };

        /// <summary>Põe a peça no espaço dela e devolve a que estava lá (ou null).</summary>
        public GearDef Set(GearSlot s, GearDef def)
        {
            var old = Get(s);
            switch (s)
            {
                case GearSlot.Weapon: weapon = def; break;
                case GearSlot.Hat: hat = def; break;
                default: charm = def; break;
            }
            return old;
        }

        public IEnumerable<GearDef> All()
        {
            if (weapon != null) yield return weapon;
            if (hat != null) yield return hat;
            if (charm != null) yield return charm;
        }

        public int Hp { get { int v = 0; foreach (var g in All()) v += g.hp; return v; } }
        public Vector2Int Damage { get { var v = Vector2Int.zero; foreach (var g in All()) v += g.damage; return v; } }
        public float Speed { get { float v = 0; foreach (var g in All()) v += g.speed; return v; } }
        public float Range { get { float v = 0; foreach (var g in All()) v += g.range; return v; } }
        public float Loot { get { float v = 0; foreach (var g in All()) v += g.loot; return v; } }
        public bool Digs { get { foreach (var g in All()) if (g.digs) return true; return false; } }

        /// <summary>Multiplica o intervalo entre golpes (golpes 25% mais rápidos = intervalo / 1,25).</summary>
        public float CooldownFactor => 1f / Mathf.Max(0.4f, 1f + Speed);

        public List<string> Names() => new List<string> { weapon != null ? weapon.name : "", hat != null ? hat.name : "", charm != null ? charm.name : "" };
    }
}
