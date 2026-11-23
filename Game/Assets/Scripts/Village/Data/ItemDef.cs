using UnityEngine;

namespace Pixel.Village
{
    /// <summary>Um recurso ou produto da vila (cana, rum...). Edite os valores no Inspector.</summary>
    [CreateAssetMenu(menuName = "Pixel/Item", fileName = "Item")]
    public class ItemDef : ScriptableObject
    {
        public string displayName = "Item";
        public Sprite icon;
        [Tooltip("Quanto um visitante paga por 1 unidade no Mercado. 0 = não é vendido.")]
        public int sellPrice = 1;
        public Color textColor = Color.white;
        [Tooltip("O Mercado não vende abaixo desta reserva (x número de tripulantes), para a tripulação não passar vontade.")]
        public int reservePerCrew;
    }
}
