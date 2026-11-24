using UnityEngine;

namespace Pixel.Ship
{
    public enum BlockRole { Hull, Deck, Mast, Sail, Cannon, Cabin }

    /// <summary>Um tipo de bloco do navio (visão lateral). Edite custo e atributos no Inspector.</summary>
    [CreateAssetMenu(menuName = "Pixel/Bloco do Navio", fileName = "Bloco")]
    public class ShipBlockDef : ScriptableObject
    {
        public string displayName = "Bloco";
        [TextArea] public string description;
        public Sprite sprite;
        [Tooltip("Opcional: peças que se encaixam na vertical com blocos iguais (ex.: vela). Vazio = usa o sprite.")]
        public Sprite spriteTop, spriteBottom, spriteSingle;
        public BlockRole role;
        public int cost = 10;
        [Tooltip("Sólido = dá pra pisar e bloqueia (casco, convés).")]
        public bool solid;
        [Tooltip("Ordem de desenho: fundo (cabine, mastro, vela) atrás, casco na frente.")]
        public int sortingOrder;

        [Header("Atributos")]
        public int hp = 2;
        public int sailPower;
        public int bunks;
        public int cannons;
    }
}
