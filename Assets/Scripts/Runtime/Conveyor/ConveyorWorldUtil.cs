using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    public static class ConveyorWorldUtil
    {
        public static Vector3 ToWorldPosition(Vector2Int coord, float cellSize, float groundY, Vector3 origin) =>
            origin + new Vector3(coord.x * cellSize, groundY, coord.y * cellSize);

        // straight: R+90, corner: -(R+90) — different exported facings
        public static float ToWorldYRotation(ConveyorTileShape shape, float toolRotation) =>
            shape == ConveyorTileShape.Straight ? toolRotation + 90f : -(toolRotation + 90f);
    }
}
