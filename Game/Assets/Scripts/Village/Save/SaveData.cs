using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pixel.Village
{
    /// <summary>Tudo que vai para o arquivo de save (formato JSON). Só tipos simples.</summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public string savedAt;

        // Vila
        public int gold;
        public List<ItemCount> stock = new List<ItemCount>();
        public List<BuildingSave> buildings = new List<BuildingSave>();
        public float captainX, captainY;

        // Tripulação
        public int day;
        public float dayTimer;
        public List<CrewSave> crew = new List<CrewSave>();
        public List<CrewSave> recruits = new List<CrewSave>();
        public List<LogLine> log = new List<LogLine>();

        // Navio e mapa
        public List<BlockSave> ship = new List<BlockSave>();
        public int shipDamage;
        public int worldSeed;
        public List<IslandSave> islands = new List<IslandSave>();

        // Expedições (as que estavam em andamento não são salvas: o grupo volta pra casa)
        public List<string> dungeonsCleared = new List<string>();
        public List<ExpeditionManager.Preset> presets = new List<ExpeditionManager.Preset>();
    }

    [Serializable] public class ItemCount { public string item; public int count; }
    [Serializable] public class BuildingSave { public string def; public int x, y; }
    [Serializable] public class BlockSave { public string def; public int x, y; }
    [Serializable] public class LogLine { public string msg; public Color color; }

    [Serializable]
    public class IslandSave
    {
        public int id;
        public bool revealed, visited, looted, cleared, treasure;
    }

    [Serializable]
    public class CrewSave
    {
        public string name;
        public int profession, level, xp, morale;
        public List<int> traits = new List<int>();
        public bool fed, paid;
        public Color skin, outfit, hair;

        public static CrewSave From(CrewMember m)
        {
            var s = new CrewSave
            {
                name = m.name, profession = (int)m.profession, level = m.level, xp = m.xp, morale = m.morale,
                fed = m.fedToday, paid = m.paidToday, skin = m.skin, outfit = m.outfit, hair = m.hair,
            };
            foreach (var t in m.traits) s.traits.Add((int)t);
            return s;
        }

        public CrewMember ToMember()
        {
            var m = new CrewMember
            {
                name = name, profession = (Profession)profession, level = level, xp = xp, morale = morale,
                fedToday = fed, paidToday = paid, skin = skin, outfit = outfit, hair = hair,
            };
            foreach (var t in traits) m.traits.Add((Trait)t);
            return m;
        }
    }
}
