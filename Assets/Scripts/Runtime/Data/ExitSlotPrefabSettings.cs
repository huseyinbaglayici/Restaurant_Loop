using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    // Same pattern as ConveyorPrefabSettings/FoodItemPrefabSettings: one persisted asset under
    // Resources holding the placeholder prefab for one exit-stack slot floor. Swap the prefab
    // reference for real art later without touching ExitStackAnchor.
    public class ExitSlotPrefabSettings : ScriptableObject
    {
        public GameObject ExitSlotPrefab;
    }
}
