using UnityEngine;

namespace Pixel.Village
{
    public enum BuildingKind { House, Producer, Market, Tavern, Shipyard, Expedition }

    /// <summary>Um tipo de construção da vila. Edite custo, tamanho e produção no Inspector.</summary>
    [CreateAssetMenu(menuName = "Pixel/Construção", fileName = "Building")]
    public class BuildingDef : ScriptableObject
    {
        public string displayName = "Construção";
        [TextArea] public string description;
        public Sprite sprite;
        public Sprite icon;
        public BuildingKind kind;
        [Tooltip("Tamanho no chão, em tiles (largura x profundidade).")]
        public Vector2Int footprint = new Vector2Int(3, 2);
        public int cost = 50;
        [Tooltip("Se o capitão e o resto colidem com ela (hortas não bloqueiam).")]
        public bool blocksMovement = true;
        [Tooltip("Plana no chão (ex.: Horta): a colisão cobre a área toda, para ninguém andar \"por trás\" dela.")]
        public bool flat;

        [Tooltip("Precisa encostar na água (ex.: Cabana de Pesca).")]
        public bool requiresWater;

        [Header("Trabalho (só para Producer)")]
        [Tooltip("Ofício ideal: quem tem esse ofício trabalha com 100%; os outros com 50%.")]
        public Profession profession = Profession.Marujo;
        public int workerSlots = 1;

        [Header("Produção (só para Producer)")]
        public ItemDef inputItem;
        public int inputAmount;
        public ItemDef outputItem;
        public int outputAmount = 1;
        public float cycleSeconds = 10f;

        [Header("Forja (Producer sem item de saída)")]
        [Tooltip("Faz peças de equipamento (armas e chapéus) em vez de um item do estoque.")]
        public bool forgesGear;
        [Tooltip("Ouro gasto em cada peça forjada.")]
        public int goldPerPiece = 25;

        [Header("Moradia (só para House)")]
        public int housing = 2;
    }
}
