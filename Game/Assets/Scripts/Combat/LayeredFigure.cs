using Pixel.Village;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pixel.Combat
{
    /// <summary>Boneco em camadas (pele, roupa, cabelo, base) na visão lateral, com animação de andar.</summary>
    public class LayeredFigure : MonoBehaviour
    {
        CrewManager.LayerSprites sprites;
        SpriteRenderer skin, outfit, hair, baseLayer;
        float anim;

        public void Init(CrewManager.LayerSprites s, Color skinColor, Color outfitColor, Color hairColor, int order)
        {
            sprites = s;
            var sg = gameObject.AddComponent<SortingGroup>();
            sg.sortingOrder = order;
            skin = Layer("Pele", 0, skinColor);
            outfit = Layer("Roupa", 1, outfitColor);
            hair = Layer("Cabelo", 2, hairColor);
            baseLayer = Layer("Base", 3, Color.white);
            Pose(false, true, true);
        }

        SpriteRenderer Layer(string n, int order, Color c)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            sr.color = c;
            return sr;
        }

        public void Pose(bool moving, bool grounded, bool facingRight)
        {
            if (sprites == null) return;
            var set = facingRight ? sprites.right : sprites.left;
            int frame;
            if (!grounded) frame = 1;
            else if (moving) { anim += Time.deltaTime * 9f; frame = 1 + (int)anim % 2; }
            else { anim = 0; frame = 0; }
            skin.sprite = set.skin[frame];
            outfit.sprite = set.outfit[frame];
            hair.sprite = set.hair[frame];
            baseLayer.sprite = set.baseLayer[frame];
        }
    }
}
