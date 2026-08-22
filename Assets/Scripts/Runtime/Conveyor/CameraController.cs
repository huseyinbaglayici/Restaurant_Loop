using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Keeps the scene's current camera offset (X/Z/Y delta from the queue center) as a fixed
    // baseline, then re-centers the camera on the queue's lane center for whatever level loads —
    // rotation is left untouched.
    public class CameraController : IInitializable
    {
        private readonly QueueAnchor _queueAnchor;
        private readonly Camera _camera;
        private readonly Vector3 _offsetFromQueueCenter;

        public CameraController(QueueAnchor queueAnchor, Camera camera)
        {
            _queueAnchor = queueAnchor;
            _camera = camera;
            _offsetFromQueueCenter = camera.transform.position - ComputeQueueCenter();
        }

        private Vector3 ComputeQueueCenter()
        {
            var lanePositions = _queueAnchor.LanePositions;
            if (lanePositions.Length == 0) return Vector3.zero;

            var sum = Vector3.zero;
            foreach (var position in lanePositions) sum += position;
            return sum / lanePositions.Length;
        }

        public void Initialize()
        {
            _camera.transform.position = ComputeQueueCenter() + _offsetFromQueueCenter;
        }
    }
}
