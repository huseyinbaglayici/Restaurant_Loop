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

    // Per-food display scale (by location and Closed/Opened) and stack spacing, tuned in one place.
    // Applied at runtime only; prefabs and meshes are never modified.
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

            [Header("Stacking Spacing")]
            [Tooltip("Spacing between stacked units on the belt (1 = touching, above 1 = gap, below 1 = overlap).")]
            public float ConveyorStackSpacing = 1f;

            [Tooltip("Spacing between stacked units on the exit rack (1 = touching, above 1 = gap, 0 = use the belt value).")]
            public float ExitStackSpacing = 0f;

            [Tooltip("Default stack spacing (1 = touching).")]
            public float StackSpacingFactor = 1f;
        }

        [Header("Global Scale Multipliers (All Foods)")]
        [Tooltip("Global scale multiplier for all food/bubbles in Queue (default 1.0). Multiplies on top of per-food scales.")]
        [SerializeField] private float _globalQueueScale = 1f;

        [Tooltip("Global scale multiplier for all food in Rack / FoodStack (default 1.0). Multiplies on top of per-food scales.")]
        [SerializeField] private float _globalFoodStackScale = 1f;

        [Tooltip("Global scale multiplier for all food on Conveyor belt (default 1.0). Multiplies on top of per-food scales.")]
        [SerializeField] private float _globalConveyorScale = 1f;

        public float GlobalQueueScale => _globalQueueScale > 0f ? _globalQueueScale : 1f;
        public float GlobalFoodStackScale => _globalFoodStackScale > 0f ? _globalFoodStackScale : 1f;
        public float GlobalConveyorScale => _globalConveyorScale > 0f ? _globalConveyorScale : 1f;

        [Header("Per-Food Entries")]
        [SerializeField] private List<Entry> _entries = new();

        public float ResolveScale(FoodType type, FoodLocation location, FoodVisualState state)
        {
            var entry = FindEntry(type);
            var locationScale = entry != null ? SelectLocation(entry, location) : LocationScale.Default;
            float raw = state == FoodVisualState.Opened ? locationScale.OpenedScale : locationScale.ClosedScale;
            float scale = raw > 0f ? raw : 1f;

            float globalMultiplier = location switch
            {
                FoodLocation.Queue => GlobalQueueScale,
                FoodLocation.FoodStack => GlobalFoodStackScale,
                FoodLocation.Conveyor => GlobalConveyorScale,
                _ => 1f
            };

            return scale * globalMultiplier;
        }

        public float ResolveStackSpacingFactor(FoodType type, FoodLocation location = FoodLocation.Conveyor)
        {
            var entry = FindEntry(type);
            if (entry == null) return 1f;

            float factor = location switch
            {
                FoodLocation.FoodStack => entry.ExitStackSpacing > 0f ? entry.ExitStackSpacing : (entry.ConveyorStackSpacing > 0f ? entry.ConveyorStackSpacing : entry.StackSpacingFactor),
                FoodLocation.Conveyor => entry.ConveyorStackSpacing > 0f ? entry.ConveyorStackSpacing : entry.StackSpacingFactor,
                _ => entry.StackSpacingFactor > 0f ? entry.StackSpacingFactor : (entry.ConveyorStackSpacing > 0f ? entry.ConveyorStackSpacing : 1f)
            };

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
