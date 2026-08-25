using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    public class ExitSlotPrefabSettings : ScriptableObject
    {
        public GameObject ExitSlotPrefab;

        [Tooltip("Extra Tray power-up: decorative rack visual placed once, alongside the base slots.")]
        public GameObject ExtraRackPrefab;
    }
}
