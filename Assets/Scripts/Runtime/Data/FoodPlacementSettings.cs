using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    // Where a food visual currently sits — drives which per-location scale FoodPlacementSettings resolves.
    public enum FoodLocation
    {
        Queue,
        FoodStack,
        Conveyor
    }

    // Per-food display scale (by location x Closed/Opened) and stack spacing, all in one place so a food's
    // look never needs touching three different assets to tune. Runtime-only: nothing here is ever written
    // back to the prefab, the unit prefab or the source mesh.
    [CreateAssetMenu(fileName = "FoodPlacementSettings", menuName = "RestaurantLoop/Food Placement Settings")]
    public class FoodPlacementSettings : ScriptableObject
    {
        [System.Serializable]
        public struct LocationScale
        {
            [Tooltip("Multiplier for the packaged/closed mesh at this location. 0 or less is read as 1.")]
            public float ClosedScale;

            [Tooltip("Multiplier for the unpacked/opened mesh at this location. 0 or less is read as 1.")]
            public float OpenedScale;

            public static LocationScale Default => new LocationScale { ClosedScale = 1f, OpenedScale = 1f };
        }

        [System.Serializable]
        public class Entry
        {
            public FoodType Type;

            [Header("Scale per location")]
            public LocationScale Queue = LocationScale.Default;
            public LocationScale FoodStack = LocationScale.Default;
            public LocationScale Conveyor = LocationScale.Default;

            [Header("Stacking")]
            [Tooltip("Gap between this food type's stacked units, as a fraction of one unit's actual rendered " +
                     "height. 1 = units touch, below 1 = they overlap, above 1 = they float apart. 0 or less " +
                     "is read as 1.")]
            public float StackSpacingFactor = 1f;
        }

        [SerializeField] private List<Entry> _entries = new();

        public float ResolveScale(FoodType type, FoodLocation location, FoodVisualState state)
        {
            var entry = FindEntry(type);
            var locationScale = entry != null ? SelectLocation(entry, location) : LocationScale.Default;
            float raw = state == FoodVisualState.Opened ? locationScale.OpenedScale : locationScale.ClosedScale;
            return raw > 0f ? raw : 1f;
        }

        public float ResolveStackSpacingFactor(FoodType type)
        {
            var entry = FindEntry(type);
            float factor = entry != null ? entry.StackSpacingFactor : 1f;
            return factor > 0f ? factor : 1f;
        }

        private Entry FindEntry(FoodType type)
        {
            foreach (var entry in _entries)
                if (entry != null && entry.Type == type) return entry;

            return null;
        }

        private static LocationScale SelectLocation(Entry entry, FoodLocation location) => location switch
        {
            FoodLocation.Queue => entry.Queue,
            FoodLocation.FoodStack => entry.FoodStack,
            FoodLocation.Conveyor => entry.Conveyor,
            _ => LocationScale.Default
        };
    }
}
