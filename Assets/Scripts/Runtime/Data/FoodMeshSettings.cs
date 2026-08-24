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

            [Tooltip("Instantiated as-is for the opened unit visual — its own authored mesh, materials and " +
                     "orientation are used directly. Leave empty to keep the pool's generic unit prefab, " +
                     "tinted with the food type colour.")]
            public GameObject OpenedPrefab;
            [Tooltip("Extra local rotation applied on top of the prefab's own orientation, for models authored " +
                     "facing the wrong way. Constant — same in the queue, on the belt and in the exit stack.")]
            public Vector3 OpenedRotationEuler;

            [Tooltip("Same as Opened Prefab, for the packaged/closed look.")]
            public GameObject ClosedPrefab;
            [Tooltip("Same as Opened Rotation Euler, for the packaged prefab.")]
            public Vector3 ClosedRotationEuler;

            [Tooltip("UI sprite for this food type (order icons, recipe cards, etc).")]
            public Sprite Icon;

            [Tooltip("This prefab is one fixed rack holding several pre-placed items (e.g. a shelf of drinks), " +
                     "not a single item to be stacked N times. One instance is spawned regardless of Amount; " +
                     "individual items are shown/hidden as the stack is consumed instead. Structure: child[0] " +
                     "is the always-visible base/decoration (e.g. the tray), children[1..] are the toggleable slots.")]
            public bool FixedChildSlots;
        }

        // What a single food unit should look like in one visual state.
        public readonly struct MeshVisual
        {
            public readonly GameObject Prefab;
            public readonly Vector3 RotationEuler;
            public readonly bool FixedChildSlots;

            public MeshVisual(GameObject prefab, Vector3 rotationEuler, bool fixedChildSlots)
            {
                Prefab = prefab;
                RotationEuler = rotationEuler;
                FixedChildSlots = fixedChildSlots;
            }
        }

        [SerializeField] private List<Entry> _entries = new();

        public bool TryGetVisual(FoodType type, FoodVisualState state, out MeshVisual visual)
        {
            foreach (var entry in _entries)
            {
                if (entry.Type != type) continue;

                bool isOpened = state == FoodVisualState.Opened;
                visual = isOpened
                    ? new MeshVisual(entry.OpenedPrefab, entry.OpenedRotationEuler, entry.FixedChildSlots)
                    : new MeshVisual(entry.ClosedPrefab, entry.ClosedRotationEuler, entry.FixedChildSlots);

                return visual.Prefab != null;
            }

            visual = default;
            return false;
        }

        public bool TryGetIcon(FoodType type, out Sprite icon)
        {
            foreach (var entry in _entries)
            {
                if (entry.Type != type) continue;

                icon = entry.Icon;
                return icon != null;
            }

            icon = null;
            return false;
        }
    }
}
