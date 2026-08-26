namespace RestaurantLoop.Runtime.Conveyor
{
    public interface IConveyorPieceVisual
    {
        // axisSign flips belt flow to match the piece's spawn (world Y) rotation
        void OnPieceSpawned(float axisSign);
    }
}
