using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    public enum ConveyorTileShape { Straight, Corner }

    [System.Serializable]
    public struct ConveyorCellPaint
    {
        public ConveyorTileShape Shape;
        public float Rotation; // degrees, 0/90/180/270
        public bool IsGate; // at most one per level
    }

    [System.Serializable]
    public struct ConveyorCellData
    {
        public Vector2Int Coord;
        public ConveyorTileShape Shape;
        public float Rotation;
        public bool IsGate;
    }
}
