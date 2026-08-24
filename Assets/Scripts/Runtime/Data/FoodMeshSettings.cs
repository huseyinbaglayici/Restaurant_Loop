using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    // Closed: packaged, sits in the queue. Opened: unpacked, on the belt / in a customer's hand.
    public enum FoodVisualState
    {
        Closed,
        Opened
    }

    [CreateAssetMenu(fileName = "FoodMeshSettings", menuName = "RestaurantLoop/Food Mesh Settings")]
    public class FoodMeshSettings : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public FoodType Type;

            public Mesh OpenedMesh;
            [Tooltip("One material per submesh of the opened mesh. Leave empty to keep the unit prefab's own " +
                     "material and tint it with the food type colour.")]
            public Material[] OpenedMaterials;
            [Tooltip("Fixed local orientation for the opened mesh, for models authored facing the wrong way. " +
                     "Constant — it is the same in the queue, on the belt and in the exit stack.")]
            public Vector3 OpenedRotationEuler;

            public Mesh ClosedMesh;
            [Tooltip("Same as Opened Materials, for the packaged mesh.")]
            public Material[] ClosedMaterials;
            [Tooltip("Same as Opened Rotation Euler, for the packaged mesh.")]
            public Vector3 ClosedRotationEuler;
        }

        // What a single food unit should look like in one visual state.
        public readonly struct MeshVisual
        {
            public readonly Mesh Mesh;
            public readonly Material[] Materials;
            public readonly Vector3 RotationEuler;

            public MeshVisual(Mesh mesh, Material[] materials, Vector3 rotationEuler)
            {
                Mesh = mesh;
                Materials = materials;
                RotationEuler = rotationEuler;
            }

            public bool HasMaterials => Materials != null && Materials.Length > 0;
        }

        [SerializeField] private List<Entry> _entries = new();

        // Multi-submesh models (a burger is bun + patty + cheese + ...) need their own material array —
        // the unit prefab only carries one, so every submesh past the first would render unshaded.
        public bool TryGetVisual(FoodType type, FoodVisualState state, out MeshVisual visual)
        {
            foreach (var entry in _entries)
            {
                if (entry.Type != type) continue;

                bool isOpened = state == FoodVisualState.Opened;
                visual = isOpened
                    ? new MeshVisual(entry.OpenedMesh, entry.OpenedMaterials, entry.OpenedRotationEuler)
                    : new MeshVisual(entry.ClosedMesh, entry.ClosedMaterials, entry.ClosedRotationEuler);

                return visual.Mesh != null;
            }

            visual = default;
            return false;
        }
    }
}
