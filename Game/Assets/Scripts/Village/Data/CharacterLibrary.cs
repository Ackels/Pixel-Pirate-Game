using UnityEngine;

namespace Pixel.Village
{
    /// <summary>
    /// Catálogo dos visuais de personagem: o do capitão, os possíveis para tripulantes,
    /// os de visitantes e os de inimigos. Tripulantes guardam o NOME do visual (vai para o save).
    /// </summary>
    [CreateAssetMenu(menuName = "Pixel/Catálogo de personagens")]
    public class CharacterLibrary : ScriptableObject
    {
        public CharacterLook captain;
        public CharacterLook[] crew;
        public CharacterLook[] visitors;
        public CharacterLook skeleton;
        public CharacterLook enemyPirate;
        public CharacterLook legendCaptain;

        public CharacterLook Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var l in crew) if (l != null && l.name == id) return l;
            foreach (var l in visitors) if (l != null && l.name == id) return l;
            if (captain != null && captain.name == id) return captain;
            if (skeleton != null && skeleton.name == id) return skeleton;
            return null;
        }

        /// <summary>Visual de tripulante para um nome qualquer (saves antigos sem visual escolhido).</summary>
        public CharacterLook ForName(string seed)
        {
            if (crew == null || crew.Length == 0) return captain;
            int h = 17;
            foreach (char c in seed ?? "") h = h * 31 + c;
            return crew[Mathf.Abs(h) % crew.Length];
        }
    }
}
