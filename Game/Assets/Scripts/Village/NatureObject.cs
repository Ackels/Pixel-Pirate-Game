using UnityEngine;

namespace Pixel.Village
{
    /// <summary>
    /// Uma coisa da natureza na ilha da vila (árvore, coqueiro, rocha, pedra, toco, arbusto, flor, capim).
    /// As grandes ocupam a grade e impedem construir em cima: use Demolir para limpar o terreno (fica salvo pelo id).
    /// As pequenas (flores, capim) são só enfeite: somem sozinhas quando uma construção é posta em cima.
    /// </summary>
    public class NatureObject : MonoBehaviour
    {
        public enum Kind { Tree, Palm, Rock, Stump, Bush, Flower, Grass }

        [Tooltip("Número fixo desta peça (a ilha é sempre gerada igual, então dá para salvar quais foram limpas).")]
        public int id;
        public Kind kind;
        [Tooltip("Ocupa a grade (impede construir). Flores e capim não ocupam.")]
        public bool blocks = true;
        [Tooltip("Canto inferior esquerdo e tamanho, em células, da área que ocupa no chão.")]
        public Vector2Int cell;
        public Vector2Int size = Vector2Int.one;
        [Tooltip("Cor normal do desenho (as árvores têm tons levemente diferentes).")]
        public Color baseColor = Color.white;

        public string DisplayName => kind switch
        {
            Kind.Tree => "árvore",
            Kind.Palm => "coqueiro",
            Kind.Rock => "rocha",
            Kind.Stump => "toco",
            Kind.Bush => "arbusto",
            Kind.Flower => "flor",
            _ => "capim",
        };

        void Start()
        {
            if (VillageManager.Instance != null) VillageManager.Instance.RegisterNature(this);
        }
    }

    /// <summary>Some com um "puf": encolhe e desbota (árvore cortada, pedra quebrada).</summary>
    public class VanishPuff : MonoBehaviour
    {
        float t;
        Vector3 startScale;
        SpriteRenderer[] renderers;

        void Start()
        {
            startScale = transform.localScale;
            renderers = GetComponentsInChildren<SpriteRenderer>();
            foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
        }

        void Update()
        {
            t += Time.unscaledDeltaTime / 0.35f;
            float k = Mathf.Clamp01(t);
            transform.localScale = new Vector3(startScale.x * (1f + k * 0.3f), startScale.y * (1f - k), startScale.z);
            foreach (var r in renderers) if (r != null) { var c = r.color; c.a = 1f - k; r.color = c; }
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
