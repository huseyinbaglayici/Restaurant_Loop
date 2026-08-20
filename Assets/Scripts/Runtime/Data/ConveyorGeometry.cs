using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    public enum ConveyorTileShape { Straight, Corner }

    /// <summary>In-memory paint value for one grid cell (shape + explicit designer-set rotation).</summary>
    [System.Serializable]
    public struct ConveyorCellPaint
    {
        public ConveyorTileShape Shape;

        /// <summary>Degrees, one of 0/90/180/270. Set explicitly by the level designer (rotate tool), never inferred.</summary>
        public float Rotation;

        /// <summary>At most one cell in a level is the Gate — the belt's spawn/despawn seam.</summary>
        public bool IsGate;
    }

    /// <summary>Flattened form of a painted cell for the level asset's cell list.</summary>
    [System.Serializable]
    public struct ConveyorCellData
    {
        public Vector2Int Coord;
        public ConveyorTileShape Shape;
        public float Rotation;
        public bool IsGate;
    }
}
