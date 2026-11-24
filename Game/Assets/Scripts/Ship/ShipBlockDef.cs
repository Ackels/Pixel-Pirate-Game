using UnityEngine;

namespace Pixel.Ship
{
    public enum BlockRole { Hull, Deck, Mast, Sail, Cannon, Cabin, Platform, Ladder, Wall, Decor, Flag }

    /// <summary>Forma do bloco. Rampa e curvo têm 4 orientações; meio bloco tem 2 (embaixo / em cima).</summary>
    public enum BlockShape { Full, Slope, Round, Half }

    /// <summary>Para que a tripulação usa a peça na vida a bordo.</summary>
    public enum BlockUse { None, Food, Rum, Bed }

    /// <summary>Aba do estaleiro onde o bloco aparece.</summary>
    public enum BlockCategory { Casco, Estrutura, Velas, Armas, Enfeites }

    /// <summary>Como o desenho muda conforme os vizinhos.</summary>
    public enum ConnectMode
    {
        None,
        /// <summary>Peças de cima/meio/baixo com blocos iguais (spriteTop/Bottom/Single).</summary>
        Vertical,
        /// <summary>16 peças (connectSprites) pelos vizinhos iguais: 1=cima 2=baixo 4=esquerda 8=direita.</summary>
        Sail16,
        /// <summary>spriteTop (com friso) quando não há nada sólido em cima.</summary>
        ExposedTop,
    }

    /// <summary>Um tipo de bloco do navio (visão lateral). Edite custo e atributos no Inspector.</summary>
    [CreateAssetMenu(menuName = "Pixel/Bloco do Navio", fileName = "Bloco")]
    public class ShipBlockDef : ScriptableObject
    {
        public string displayName = "Bloco";
        [TextArea] public string description;
        public Sprite sprite;
        [Tooltip("Opcional: peças que se encaixam com os vizinhos (veja Connect).")]
        public Sprite spriteTop, spriteBottom, spriteSingle;
        public Sprite[] connectSprites;
        [Tooltip("Vela: pedaço que liga a vela ao mastro vizinho (4 peças: 1 = vela igual em cima, 2 = embaixo).")]
        public Sprite[] tabSprites;
        [Tooltip("Animação contínua (ex.: bandeira tremulando). Para velas, connectSprites tem 16 peças por quadro de vento.")]
        public Sprite[] animFrames;
        public float animFps = 10f;
        public ConnectMode connect;
        public BlockRole role;
        public BlockShape shape;
        public BlockCategory category;
        [Tooltip("Vida a bordo: caixa de comida, barril de rum, rede de dormir.")]
        public BlockUse use;
        [Tooltip("Blocos da mesma família são o mesmo formato em cores diferentes (o estaleiro mostra um botão e as cores).")]
        public string family;
        public Color swatch = Color.white;
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

        public string Family => string.IsNullOrEmpty(family) ? name : family;
        public bool Standable => solid || role == BlockRole.Platform;

        /// <summary>Desenho para botões e para a prévia (a peça "sozinha").</summary>
        public Sprite Preview => connect switch
        {
            ConnectMode.Sail16 when connectSprites != null && connectSprites.Length > 0 && connectSprites[0] != null => connectSprites[0],
            ConnectMode.Vertical when spriteSingle != null => spriteSingle,
            ConnectMode.ExposedTop when spriteTop != null => spriteTop,
            _ => sprite,
        };
    }

    /// <summary>Orientação das formas: o desenho é feito na orientação 0 (sólido embaixo à direita, ◢) e espelhado.</summary>
    public static class ShipShapes
    {
        public static int Orientations(BlockShape s) => s == BlockShape.Slope || s == BlockShape.Round ? 4 : s == BlockShape.Half ? 2 : 1;

        /// <summary>Rampa/curvo: 0 ◢, 1 ◣, 2 ◤, 3 ◥. Meio bloco: 0 embaixo, 1 em cima.</summary>
        public static (bool x, bool y) Flips(BlockShape s, int rot)
        {
            switch (s)
            {
                case BlockShape.Slope:
                case BlockShape.Round:
                    return rot switch { 1 => (true, false), 2 => (true, true), 3 => (false, true), _ => (false, false) };
                case BlockShape.Half:
                    return (false, rot == 1);
                default:
                    return (false, false);
            }
        }

        /// <summary>A mesma peça vista no espelho (para o modo simetria).</summary>
        public static int Mirror(BlockShape s, int rot) =>
            s == BlockShape.Slope || s == BlockShape.Round ? new[] { 1, 0, 3, 2 }[rot & 3] : rot;

        /// <summary>Contorno da colisão (centro da célula = 0,0).</summary>
        public static Vector2[] Polygon(BlockShape s, int rot)
        {
            Vector2[] pts;
            switch (s)
            {
                case BlockShape.Slope:
                    pts = new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f) };
                    break;
                case BlockShape.Round:
                    var list = new System.Collections.Generic.List<Vector2> { new Vector2(0.5f, -0.5f) };
                    for (int a = 90; a <= 180; a += 10)
                        list.Add(new Vector2(0.5f + Mathf.Cos(a * Mathf.Deg2Rad), -0.5f + Mathf.Sin(a * Mathf.Deg2Rad)));
                    pts = list.ToArray();
                    break;
                case BlockShape.Half:
                    pts = new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0f), new Vector2(-0.5f, 0f) };
                    break;
                default:
                    pts = new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f) };
                    break;
            }
            var (fx, fy) = Flips(s, rot);
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(fx ? -pts[i].x : pts[i].x, fy ? -pts[i].y : pts[i].y);
            if (fx ^ fy) System.Array.Reverse(pts);
            return pts;
        }
    }
}
