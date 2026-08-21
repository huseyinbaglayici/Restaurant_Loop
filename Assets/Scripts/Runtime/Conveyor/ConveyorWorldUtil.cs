using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    public static class ConveyorWorldUtil
    {
        public static Vector3 ToWorldPosition(Vector2Int coord, float cellSize) =>
            new(coord.x * cellSize, 0f, coord.y * cellSize);

        // Straight and corner prefabs were exported with different default facings, so each needs
        // its own conversion from the 2D tool's rotation to a world Y angle. Both confirmed against
        // a real 4-corner loop in-scene:
        //   straight: target = R + 90            (0→90, 90→180, 180→270, 270→0)
        //   corner:   target = -(R + 90)          (0→270, 90→180, 180→90, 270→0)
        public static float ToWorldYRotation(ConveyorTileShape shape, float toolRotation) =>
            shape == ConveyorTileShape.Straight ? toolRotation + 90f : -(toolRotation + 90f);
    }
}
