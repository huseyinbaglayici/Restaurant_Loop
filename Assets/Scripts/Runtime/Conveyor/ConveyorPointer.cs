using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Raw pointer side of conveyor input: detects the press edge, picks the camera to look through and
    // measures screen distance to world points. What a click is allowed to pick lives in the pick handlers.
    public class ConveyorPointer
    {
        private readonly CameraController _cameraController;
        private readonly Camera _camera;

        private bool _wasMouseDown;

        public ConveyorPointer(CameraController cameraController, Camera camera)
        {
            _cameraController = cameraController;
            _camera = camera;
        }

        // True only on the frame the left button goes down (and a camera is available).
        public bool TryGetClick(out Camera camera, out Vector2 screenPoint)
        {
            camera = null;
            screenPoint = default;

            bool isMouseDown = Input.GetMouseButton(0);
            bool clicked = isMouseDown && !_wasMouseDown;
            _wasMouseDown = isMouseDown;
            if (!clicked) return false;

            camera = _camera != null ? _camera : (_cameraController.Camera != null ? _cameraController.Camera : Camera.main);
            if (camera == null) return false;

            screenPoint = Input.mousePosition;
            return true;
        }

        public static float ScreenDistance(Camera camera, Vector3 worldPoint, Vector2 screenPoint)
        {
            Vector3 projected = camera.WorldToScreenPoint(worldPoint);
            if (projected.z < 0f) return float.MaxValue;
            return Vector2.Distance(new Vector2(projected.x, projected.y), screenPoint);
        }
    }
}
