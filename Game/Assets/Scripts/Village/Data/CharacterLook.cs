using UnityEngine;

namespace Pixel.Village
{
    /// <summary>
    /// O visual de um personagem (Ninja Adventure, 16x16): 4 quadros de caminhada por direção,
    /// um quadro de ataque por direção e o retrato. O mesmo visual é usado na vila (de cima)
    /// e no mar (de lado, usando só esquerda/direita), para o tripulante ser sempre reconhecível.
    /// </summary>
    [CreateAssetMenu(menuName = "Pixel/Visual de personagem")]
    public class CharacterLook : ScriptableObject
    {
        public Sprite[] down, up, left, right;
        [Tooltip("Ataque: baixo, cima, esquerda, direita.")]
        public Sprite[] attack;
        public Sprite portrait;

        public Sprite[] Walk(Vector2 dir)
        {
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y)) return dir.x < 0 ? left : right;
            return dir.y < 0 ? down : up;
        }

        /// <summary>Quadro parado olhando para a direção.</summary>
        public Sprite Idle(Vector2 dir)
        {
            var set = Walk(dir);
            return set != null && set.Length > 0 ? set[0] : null;
        }

        public Sprite Attack(Vector2 dir)
        {
            if (attack == null || attack.Length < 4) return Idle(dir);
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y)) return dir.x < 0 ? attack[2] : attack[3];
            return dir.y < 0 ? attack[0] : attack[1];
        }

        /// <summary>Quadro de caminhada para um tempo de animação (avança em quadros por segundo).</summary>
        public static Sprite Frame(Sprite[] set, float animTime)
        {
            if (set == null || set.Length == 0) return null;
            return set[(int)animTime % set.Length];
        }
    }
}
