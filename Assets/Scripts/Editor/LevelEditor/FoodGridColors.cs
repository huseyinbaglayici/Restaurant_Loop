using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace Editor.LevelEditor
{
    // Single source of truth for grid cell backgrounds — reads FoodMeshSettings so the editor grid
    // stays in sync with each food's current art instead of a separate hardcoded palette.
    internal static class FoodGridColors
    {
        private static FoodMeshSettings _settings;
        private static bool _loaded;

        private static FoodMeshSettings Settings()
        {
            if (!_loaded)
            {
                _settings = Resources.Load<FoodMeshSettings>("FoodMeshSettings");
                _loaded = true;
            }
            return _settings;
        }

        // Fallback tint for entries with no art yet (no icon sprite, no explicit GridColor).
        internal static Color For(FoodType type)
        {
            var settings = Settings();
            if (settings != null && settings.TryGetGridColor(type, out var color)) return color;
            return FoodTypeUtil.ToColor(type);
        }

        // The bubble sprite already carries its own background art — matches the in-game customer bubble.
        internal static Sprite CustomerSpriteFor(FoodType type)
        {
            var settings = Settings();
            return settings != null && settings.TryGetCustomerIcon(type, out var icon) ? icon : null;
        }

        // Matches the in-game queue lane look.
        internal static Sprite QueueSpriteFor(FoodType type)
        {
            var settings = Settings();
            return settings != null && settings.TryGetQueueIcon(type, out var icon) ? icon : null;
        }
    }
}
