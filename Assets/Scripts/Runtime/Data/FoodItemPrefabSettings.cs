using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    // Same pattern as ConveyorPrefabSettings: one persisted asset under Resources so PoolInstaller
    // can Resources.Load it. One generic prefab, not one per FoodType — FoodItemController tints it
    // via FoodTypeUtil.ToColor at spawn, same color already used to represent food in the level
    // editor. Swap for per-type prefabs later without touching the pool/spawn code.
    public class FoodItemPrefabSettings : ScriptableObject
    {
        public GameObject FoodItemPrefab;
    }
}
