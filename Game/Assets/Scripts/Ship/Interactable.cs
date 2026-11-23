using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pixel.Ship
{
    /// <summary>Algo com que o capitão interage apertando E por perto (baú, comerciante...).</summary>
    public class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        public string prompt = "E: interagir";
        public float range = 1.4f;
        public Action onUse;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static Interactable Nearest(Vector2 pos)
        {
            Interactable best = null;
            float bestD = float.MaxValue;
            foreach (var i in All)
            {
                if (i == null) continue;
                float d = Mathf.Abs(i.transform.position.x - pos.x);
                if (d > i.range || Mathf.Abs(i.transform.position.y - pos.y) > 1.6f || d >= bestD) continue;
                best = i;
                bestD = d;
            }
            return best;
        }
    }
}
