using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Local pose of one tray slot inside the dispenser.
    public readonly struct TraySlotPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;

        public TraySlotPose(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
    }
}
