using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    // One shared unit prefab/VFX for every FoodType (see FoodItemController) — this is the only
    // thing that varies per type: which Mesh a stack's unit visuals use. Resources-loaded like the
    // other *Settings assets. Optional: if empty or missing, FoodItemController falls back to the
    // color-only look (FoodTypeUtil.ToColor) it already had.
    [CreateAssetMenu(fileName = "FoodMeshSettings", menuName = "RestaurantLoop/Food Mesh Settings")]
    public class FoodMeshSettings : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public FoodType Type;
            public Mesh Mesh;
        }

        [SerializeField] private List<Entry> _entries = new();

        public bool TryGetMesh(FoodType type, out Mesh mesh)
        {
            foreach (var entry in _entries)
            {
                if (entry.Type == type && entry.Mesh != null)
                {
                    mesh = entry.Mesh;
                    return true;
                }
            }

            mesh = null;
            return false;
        }
    }
}
