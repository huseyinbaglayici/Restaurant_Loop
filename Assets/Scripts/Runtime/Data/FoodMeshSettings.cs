using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
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
