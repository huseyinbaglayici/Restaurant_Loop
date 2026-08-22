using RestaurantLoop.Runtime.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor.LevelEditor
{
    internal static class EntityLabelFactory
    {
        public static Label CreateIcon(FoodType type, Vector2 cellCenter, float cellSize)
        {
            var icon = new Label(FoodTypeUtil.ToIcon(type));
            icon.style.position = Position.Absolute;
            icon.style.left = cellCenter.x - cellSize * 0.5f;
            icon.style.top = cellCenter.y - cellSize * 0.32f;
            icon.style.width = cellSize;
            icon.style.unityTextAlign = TextAnchor.MiddleCenter;
            icon.style.fontSize = cellSize * 0.34f;
            return icon;
        }

        public static Label CreateAmountBadge(int amount, Vector2 cellCenter, float cellSize)
        {
            float size = cellSize * 0.42f;
            var badge = new Label(amount.ToString());
            badge.style.position = Position.Absolute;
            badge.style.left = cellCenter.x + cellSize * 0.5f - size * 0.85f;
            badge.style.top = cellCenter.y - cellSize * 0.5f - size * 0.15f;
            badge.style.width = size;
            badge.style.height = size;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.fontSize = size * 0.55f;
            badge.style.color = Color.white;
            badge.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius =
                badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = size * 0.5f;
            return badge;
        }

        public static Label CreateHiddenBadge(Vector2 cellCenter, float cellSize)
        {
            float size = cellSize * 0.42f;
            var badge = new Label("H");
            badge.style.position = Position.Absolute;
            badge.style.left = cellCenter.x + cellSize * 0.5f - size * 0.85f;
            badge.style.top = cellCenter.y + cellSize * 0.5f - size * 0.85f;
            badge.style.width = size;
            badge.style.height = size;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.fontSize = size * 0.55f;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.color = Color.white;
            badge.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius =
                badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = size * 0.5f;
            return badge;
        }
    }
}
