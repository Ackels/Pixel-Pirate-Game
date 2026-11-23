using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pixel.Village
{
    public enum EnemyType { Caranguejo, Pirata, Esqueleto, Serpente, CapitaoLendario }

    /// <summary>Atributos de cada tipo de inimigo das masmorras.</summary>
    public static class EnemyStats
    {
        public static string Name(EnemyType t) => t switch
        {
            EnemyType.Caranguejo => "Caranguejo",
            EnemyType.Pirata => "Pirata",
            EnemyType.Esqueleto => "Esqueleto",
            EnemyType.Serpente => "Serpente",
            EnemyType.CapitaoLendario => "Capitão Lendário",
            _ => t.ToString(),
        };

        /// <summary>(vida, dano mínimo, dano máximo, segundos entre golpes)</summary>
        public static (int hp, int min, int max, float interval) Get(EnemyType t) => t switch
        {
            EnemyType.Caranguejo => (25, 3, 6, 1.1f),
            EnemyType.Pirata => (45, 6, 10, 1.0f),
            EnemyType.Esqueleto => (40, 7, 11, 1.1f),
            EnemyType.Serpente => (70, 9, 14, 1.3f),
            EnemyType.CapitaoLendario => (240, 12, 18, 1.0f),
            _ => (30, 4, 8, 1f),
        };
    }

    [Serializable]
    public class DungeonStage
    {
        public List<EnemyType> enemies = new List<EnemyType>();
    }

    /// <summary>Uma masmorra para expedições automáticas. Edite andares e recompensas no Inspector.</summary>
    [CreateAssetMenu(menuName = "Pixel/Masmorra", fileName = "Masmorra")]
    public class DungeonDef : ScriptableObject
    {
        public string displayName = "Masmorra";
        [TextArea] public string description;
        [Tooltip("Nível de tripulante recomendado.")]
        public int recommendedLevel = 1;
        [Tooltip("Segundos de viagem até lá (e de volta).")]
        public float travelSeconds = 12f;
        [Tooltip("Multiplica vida e dano dos inimigos (dificuldade).")]
        public float enemyPower = 1f;
        public List<DungeonStage> stages = new List<DungeonStage>();
        public Vector2Int goldReward = new Vector2Int(50, 80);
        public string itemReward = "Rum";
        public Vector2Int itemAmount = new Vector2Int(2, 5);
        [Tooltip("Cor de fundo da faixa de combate.")]
        public Color background = new Color(0.2f, 0.2f, 0.25f);
    }
}
