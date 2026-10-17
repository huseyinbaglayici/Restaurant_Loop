using RestaurantLoop.Runtime.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace RestaurantLoop.Editor.LevelEditor
{
    // Painter2D primitives shared by the conveyor and queue canvases.
    internal static class GridPainter
    {
        private static readonly Color EntityOutline = new(0f, 0f, 0f, 0.55f);

        private static FoodMeshSettings _cachedFoodMeshSettings;

        public static Color GetFoodColor(FoodType type)
        {
            if (_cachedFoodMeshSettings == null)
                _cachedFoodMeshSettings = Resources.Load<FoodMeshSettings>("FoodMeshSettings");
            if (_cachedFoodMeshSettings != null) return _cachedFoodMeshSettings.GetColor(type);
            return FoodTypeUtil.ToColor(type);
        }

        public static void FillRect(Painter2D painter, Rect r)
        {
            TraceRect(painter, r);
            painter.Fill();
        }

        public static void StrokeRect(Painter2D painter, Rect r, Color color, float lineWidth)
        {
            painter.strokeColor = color;
            painter.lineWidth = lineWidth;
            TraceRect(painter, r);
            painter.Stroke();
        }

        public static void DrawGridLines(Painter2D painter, int columns, int rows, float cellSize, Color color)
        {
            painter.strokeColor = color;
            painter.lineWidth = 1f;
            for (int x = 0; x <= columns; x++)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x * cellSize, 0));
                painter.LineTo(new Vector2(x * cellSize, rows * cellSize));
                painter.Stroke();
            }
            for (int y = 0; y <= rows; y++)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, y * cellSize));
                painter.LineTo(new Vector2(columns * cellSize, y * cellSize));
                painter.Stroke();
            }
        }

        public static void DrawFoodDisc(Painter2D painter, Vector2 center, float radius, FoodType type)
        {
            painter.fillColor = GetFoodColor(type);
            painter.strokeColor = EntityOutline;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.Fill();
            painter.Stroke();
        }

        private static void TraceRect(Painter2D painter, Rect r)
        {
            painter.BeginPath();
            painter.MoveTo(r.position);
            painter.LineTo(new Vector2(r.xMax, r.yMin));
            painter.LineTo(new Vector2(r.xMax, r.yMax));
            painter.LineTo(new Vector2(r.xMin, r.yMax));
            painter.ClosePath();
        }
    }
}
