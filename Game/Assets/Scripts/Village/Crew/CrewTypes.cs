using System.Collections.Generic;
using UnityEngine;

namespace Pixel.Village
{
    /// <summary>Ofício de um tripulante na vila. Marujo = sem ofício (trabalha em tudo com 50%).</summary>
    public enum Profession { Marujo, Agricultor, Pescador, Destilador }

    /// <summary>Traço de personalidade. Sempre visível antes de contratar (lição do Pixel Piracy).</summary>
    public enum Trait { Trabalhador, Preguicoso, Beberrao, Guloso, Alegre, Rabugento, Leal, Talentoso }

    public static class CrewText
    {
        public static string Name(Profession p) => p switch
        {
            Profession.Agricultor => "Agricultor",
            Profession.Pescador => "Pescador",
            Profession.Destilador => "Destilador",
            _ => "Marujo",
        };

        public static string Name(Trait t) => t switch
        {
            Trait.Trabalhador => "Trabalhador",
            Trait.Preguicoso => "Preguiçoso",
            Trait.Beberrao => "Beberrão",
            Trait.Guloso => "Guloso",
            Trait.Alegre => "Alegre",
            Trait.Rabugento => "Rabugento",
            Trait.Leal => "Leal",
            Trait.Talentoso => "Talentoso",
            _ => t.ToString(),
        };

        public static string Describe(Trait t) => t switch
        {
            Trait.Trabalhador => "trabalha 20% mais rápido",
            Trait.Preguicoso => "trabalha 20% mais devagar, mas cobra menos",
            Trait.Beberrao => "quer 2 rum por dia",
            Trait.Guloso => "come 2 peixes por dia",
            Trait.Alegre => "+10 de moral por dia",
            Trait.Rabugento => "-5 de moral por dia",
            Trait.Leal => "nunca abandona o bando",
            Trait.Talentoso => "aprende o ofício 2x mais rápido",
            _ => "",
        };

        public static bool IsGood(Trait t) =>
            t == Trait.Trabalhador || t == Trait.Alegre || t == Trait.Leal || t == Trait.Talentoso;

        public static Color MoraleColor(int morale) =>
            morale >= 60 ? new Color(0.55f, 0.95f, 0.5f) : morale >= 35 ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.45f, 0.4f);

        public static string MoraleLabel(int morale) =>
            morale >= 80 ? "Animado" : morale >= 60 ? "Contente" : morale >= 35 ? "Insatisfeito" : "Revoltado";
    }

    /// <summary>Um tripulante (ou recruta na taverna).</summary>
    public class CrewMember
    {
        public string name;
        public Profession profession;
        public int level = 1;
        public int xp;
        public List<Trait> traits = new List<Trait>();
        public int morale = 70;
        public bool fedToday = true;
        public bool paidToday = true;
        public Color skin, outfit, hair;

        public Building home;
        public Building workplace;
        public CrewAgent agent;

        /// <summary>Embarcado no navio (não trabalha na vila enquanto isso).</summary>
        public bool atSea;
        /// <summary>Foi nocauteado em combate nesta viagem (perde moral ao voltar).</summary>
        public bool injured;

        public int CombatHp => 50 + level * 8;
        public Vector2Int CombatDamage => new Vector2Int(7 + level * 2, 10 + level * 2);

        public bool Has(Trait t) => traits.Contains(t);

        public int Wage => Mathf.Max(1, 2 + level - (Has(Trait.Preguicoso) ? 1 : 0));
        public int HireCost => Wage * 4 + 5;
        /// <summary>Ciclos de produção para o próximo nível (~1,5 dia para o nível 2 numa Horta).</summary>
        public int XpToNextLevel => level * 20;

        /// <summary>Velocidade de trabalho neste prédio (1 = normal).</summary>
        public float EfficiencyAt(BuildingDef def)
        {
            float e = def.profession == Profession.Marujo || def.profession == profession ? 1f : 0.5f;
            e *= 1f + 0.15f * (level - 1);
            if (Has(Trait.Trabalhador)) e *= 1.2f;
            if (Has(Trait.Preguicoso)) e *= 0.8f;
            e *= Mathf.Lerp(0.6f, 1.2f, morale / 100f);
            return e;
        }

        public string TraitsText()
        {
            var parts = new List<string>();
            foreach (var t in traits) parts.Add(CrewText.Name(t));
            return parts.Count > 0 ? string.Join(", ", parts) : "-";
        }
    }
}
