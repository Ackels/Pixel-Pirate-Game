using UnityEngine;

namespace Pixel.Village
{
    /// <summary>
    /// Desenho de um boneco (tripulante, visitante, inimigo, bicho) num filho "Desenho" levemente deslocado para cima.
    ///
    /// Por quê: o jogo desenha por cima quem está mais embaixo na tela (eixo Y, configurado no Renderer2D). Dois bonecos
    /// na mesma altura (no convés do navio, ou parados na mesma linha na vila) empatam, e o Unity troca a ordem de um
    /// quadro para o outro: eles ficavam "piscando" um por cima do outro. Cada boneco ganha um deslocamento diferente
    /// (frações de pixel, invisível), então nunca há empate.
    /// </summary>
    public static class CharacterSprite
    {
        const int Slots = 12;
        const float Step = 0.0025f;
        static int next;

        public const string ChildName = "Desenho";

        /// <summary>Cria o SpriteRenderer do boneco no filho "Desenho" (com o desempate) e devolve ele.</summary>
        public static SpriteRenderer Create(GameObject root)
        {
            var go = new GameObject(ChildName);
            go.transform.SetParent(root.transform, false);
            next = next % Slots + 1;   // 1..12 (0 fica para o capitão, que é único)
            go.transform.localPosition = new Vector3(0f, next * Step, 0f);
            return go.AddComponent<SpriteRenderer>();
        }

        /// <summary>O desenho do boneco: o filho "Desenho" ou, nos bonecos antigos, o do próprio objeto.</summary>
        public static SpriteRenderer Of(Component c)
        {
            var t = c.transform.Find(ChildName);
            var sr = t != null ? t.GetComponent<SpriteRenderer>() : null;
            return sr != null ? sr : c.GetComponent<SpriteRenderer>();
        }
    }
}
