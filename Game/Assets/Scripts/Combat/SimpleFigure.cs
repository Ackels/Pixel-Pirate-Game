using UnityEngine;

namespace Pixel.Combat
{
    /// <summary>Qualquer boneco que sabe mostrar a pose (parado, andando, no ar) virado para um lado.</summary>
    public interface IPose
    {
        void Pose(bool moving, bool grounded, bool facingRight);
    }

    /// <summary>Boneco de um sprite só com quadros de animação (ex.: caranguejo). Desenhado virado para a direita.</summary>
    public class SimpleFigure : MonoBehaviour, IPose
    {
        public Sprite[] frames;
        public float fps = 8f;
        SpriteRenderer sr;
        float anim;

        public void Init(Sprite[] f, int order)
        {
            frames = f;
            sr = Pixel.Village.CharacterSprite.Create(gameObject);
            sr.sprite = f[0];
            sr.sortingOrder = order;
        }

        public void Pose(bool moving, bool grounded, bool facingRight)
        {
            if (sr == null || frames == null || frames.Length == 0) return;
            if (moving) anim += Time.deltaTime * fps;
            sr.sprite = frames[(int)anim % frames.Length];
            sr.flipX = !facingRight;
        }
    }
}
