using RestaurantLoop.Runtime.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace RestaurantLoop.Editor.LevelEditor
{
    // Draws belt tiles, flow arrows, customers and the gate marker for ConveyorGridCanvas.
    internal static class ConveyorTilePainter
    {
        private static readonly Color BeltColor = new(0.55f, 0.55f, 0.58f);
        private static readonly Color BeltOutline = new(0.25f, 0.25f, 0.27f);
        private static readonly Color TreadColor = new(0.38f, 0.38f, 0.40f);
        private static readonly Color ArrowColor = new(0.95f, 0.75f, 0.15f);
        private static readonly Color CustomerOutline = new(0.10f, 0.24f, 0.22f);
        private static readonly Color GateColor = new(0.2f, 0.9f, 0.95f);

        private const float HiddenCustomerAlpha = 0.55f;

        public static void DrawConveyorCell(Painter2D painter, Vector2 center, ConveyorCellPaint cell, float cellSize)
        {
            if (cell.Shape == ConveyorTileShape.Straight)
            {
                DrawStraightTile(painter, center, cell.Rotation, cellSize);
                DrawArrow(painter, center, new Vector2(1f, 0f), cell.Rotation, cellSize * 0.30f);
            }
            else
            {
                DrawCornerTile(painter, center, cell.Rotation, cellSize);
                DrawArrow(painter, center, new Vector2(-1f, 0f), cell.Rotation, cellSize * 0.22f, cellSize * 0.30f);
                DrawArrow(painter, center, new Vector2(0f, -1f), cell.Rotation, cellSize * 0.22f, cellSize * 0.30f);
            }
        }

        public static void DrawCustomer(Painter2D painter, Vector2 center, CustomerEntity customer, float cellSize)
        {
            float half = cellSize * 0.38f;
            var r = new Rect(center.x - half, center.y - half, half * 2, half * 2);

            Color color1 = GridPainter.GetFoodColor(customer.Order.Type);
            if (customer.IsHidden) color1.a = HiddenCustomerAlpha;

            if (customer.HasSecondOrder)
            {
                Color color2 = GridPainter.GetFoodColor(customer.SecondOrder.Type);
                if (customer.IsHidden) color2.a = HiddenCustomerAlpha;

                painter.fillColor = color1;
                GridPainter.FillRect(painter, new Rect(r.xMin, r.yMin, half, half * 2));

                painter.fillColor = color2;
                GridPainter.FillRect(painter, new Rect(center.x, r.yMin, half, half * 2));

                painter.strokeColor = CustomerOutline;
                painter.lineWidth = 2f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(center.x, r.yMin));
                painter.LineTo(new Vector2(center.x, r.yMax));
                painter.Stroke();
            }
            else
            {
                painter.fillColor = color1;
                GridPainter.FillRect(painter, r);
            }

            GridPainter.StrokeRect(painter, r, CustomerOutline, 2f);
        }

        public static void DrawGateMarker(Painter2D painter, Vector2 center, float cellSize)
        {
            float r = cellSize * 0.16f;
            painter.strokeColor = GateColor;
            painter.lineWidth = 2.5f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(center.x, center.y - r));
            painter.LineTo(new Vector2(center.x + r, center.y));
            painter.LineTo(new Vector2(center.x, center.y + r));
            painter.LineTo(new Vector2(center.x - r, center.y));
            painter.ClosePath();
            painter.Stroke();
        }

        private static Vector2 RotatePoint(Vector2 center, Vector2 local, float rotationDeg)
        {
            // Y grows downward, so negate to keep rotation clockwise on screen
            float rad = -rotationDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            return new Vector2(
                center.x + local.x * cos - local.y * sin,
                center.y + local.x * sin + local.y * cos);
        }

        private static void DrawStraightTile(Painter2D painter, Vector2 center, float rotation, float cellSize)
        {
            float h = cellSize * 0.46f;
            float bandHalf = cellSize * 0.16f;

            Vector2 p0 = RotatePoint(center, new Vector2(-h, -bandHalf), rotation);
            Vector2 p1 = RotatePoint(center, new Vector2(h, -bandHalf), rotation);
            Vector2 p2 = RotatePoint(center, new Vector2(h, bandHalf), rotation);
            Vector2 p3 = RotatePoint(center, new Vector2(-h, bandHalf), rotation);

            painter.fillColor = BeltColor;
            painter.strokeColor = BeltOutline;
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(p0);
            painter.LineTo(p1);
            painter.LineTo(p2);
            painter.LineTo(p3);
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();

            painter.strokeColor = TreadColor;
            painter.lineWidth = 2f;
            foreach (float lx in new[] { -h * 0.45f, h * 0.45f })
            {
                Vector2 t0 = RotatePoint(center, new Vector2(lx, -bandHalf), rotation);
                Vector2 t1 = RotatePoint(center, new Vector2(lx, bandHalf), rotation);
                painter.BeginPath();
                painter.MoveTo(t0);
                painter.LineTo(t1);
                painter.Stroke();
            }
        }

        private static void DrawCornerTile(Painter2D painter, Vector2 center, float rotation, float cellSize)
        {
            float h = cellSize * 0.46f;
            float outerR = h;
            float innerR = outerR - cellSize * 0.32f;
            Vector2 pivotLocal = new(-h, -h);
            const int segments = 10;

            painter.fillColor = BeltColor;
            painter.strokeColor = BeltOutline;
            painter.lineWidth = 2f;
            painter.BeginPath();

            for (int i = 0; i <= segments; i++)
            {
                float t = Mathf.Lerp(90f, 0f, i / (float)segments) * Mathf.Deg2Rad;
                var local = pivotLocal + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * outerR;
                var p = RotatePoint(center, local, rotation);
                if (i == 0) painter.MoveTo(p); else painter.LineTo(p);
            }
            for (int i = 0; i <= segments; i++)
            {
                float t = Mathf.Lerp(0f, 90f, i / (float)segments) * Mathf.Deg2Rad;
                var local = pivotLocal + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * innerR;
                var p = RotatePoint(center, local, rotation);
                painter.LineTo(p);
            }

            painter.ClosePath();
            painter.Fill();
            painter.Stroke();

            painter.strokeColor = TreadColor;
            painter.lineWidth = 1.5f;
            float midR = (outerR + innerR) * 0.5f;
            painter.BeginPath();
            for (int i = 0; i <= segments; i++)
            {
                float t = Mathf.Lerp(90f, 0f, i / (float)segments) * Mathf.Deg2Rad;
                var local = pivotLocal + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * midR;
                var p = RotatePoint(center, local, rotation);
                if (i == 0) painter.MoveTo(p); else painter.LineTo(p);
            }
            painter.Stroke();
        }

        private static void DrawArrow(Painter2D painter, Vector2 center, Vector2 baseDir, float rotationDeg, float size, float centerOffset = 0f)
        {
            Vector2 dir = RotatePoint(Vector2.zero, baseDir, rotationDeg);
            Vector2 perp = new Vector2(-dir.y, dir.x);
            Vector2 arrowCenter = center + dir * centerOffset;

            painter.fillColor = ArrowColor;
            painter.BeginPath();
            painter.MoveTo(arrowCenter + dir * (size * 0.5f));
            painter.LineTo(arrowCenter - dir * (size * 0.15f) + perp * (size * 0.35f));
            painter.LineTo(arrowCenter - dir * (size * 0.15f) - perp * (size * 0.35f));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
