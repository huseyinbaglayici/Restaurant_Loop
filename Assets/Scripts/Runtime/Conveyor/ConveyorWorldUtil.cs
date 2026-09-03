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

            float deltaX = ((level.GridWidth - 1) * 0.5f - (tuning.ReferenceGridWidth - 1) * 0.5f) * cellSize;
            // The conveyor bottom and exit rack remain fixed along Z, expanding upwards towards the environment
            return new Vector3(tuning.WorldOrigin.x - deltaX, tuning.WorldOrigin.y, tuning.WorldOrigin.z);
        }

        public static Vector3 ToWorldPosition(Vector2Int coord, float cellSize, float groundY, Vector3 origin) =>
            origin + new Vector3(coord.x * cellSize, groundY, coord.y * cellSize);

        // straight: R+90, corner: -(R+90) — different exported facings
        public static float ToWorldYRotation(ConveyorTileShape shape, float toolRotation) =>
            shape == ConveyorTileShape.Straight ? toolRotation + 90f : -(toolRotation + 90f);
    }
}
