using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    public static class ConveyorWorldUtil
    {
        public static Vector3 ComputeBeltOrigin(ConveyorLevelDataSO level, ConveyorTuning tuning, float cellSize)
        {
            if (!tuning.AutoCenterBeltGrid) return tuning.WorldOrigin;

            if (level.ConveyorCells.Count == 0) return tuning.WorldOrigin;

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            foreach (var cell in level.ConveyorCells)
            {
                if (cell.Coord.x < minX) minX = cell.Coord.x;
                if (cell.Coord.x > maxX) maxX = cell.Coord.x;
                if (cell.Coord.y < minY) minY = cell.Coord.y;
                if (cell.Coord.y > maxY) maxY = cell.Coord.y;
            }

            float deltaX = ((level.GridWidth - 1) * 0.5f - (tuning.ReferenceGridWidth - 1) * 0.5f) * cellSize;
            float deltaZ = ((level.GridHeight - 1) * 0.5f - (tuning.ReferenceGridHeight - 1) * 0.5f) * cellSize;

            return new Vector3(tuning.WorldOrigin.x - deltaX, tuning.WorldOrigin.y, tuning.WorldOrigin.z - deltaZ);
        }

        public static Vector3 ToWorldPosition(Vector2Int coord, float cellSize, float groundY, Vector3 origin) =>
            origin + new Vector3(coord.x * cellSize, groundY, coord.y * cellSize);

        // straight: R+90, corner: -(R+90) — different exported facings
        public static float ToWorldYRotation(ConveyorTileShape shape, float toolRotation) =>
            shape == ConveyorTileShape.Straight ? toolRotation + 90f : -(toolRotation + 90f);
    }
}
