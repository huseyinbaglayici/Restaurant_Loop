using System.Collections.Generic;
using RestaurantLoop.Runtime.Tutorial;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Raw pointer side of conveyor input: detects the press edge, picks the camera to look through and
    // measures screen distance to world points. What a click is allowed to pick lives in the pick handlers.
    public class ConveyorPointer
    {
        private readonly CameraController _cameraController;
        private readonly Camera _camera;

        private static readonly List<RaycastResult> UiHits = new();

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
            return !IsOverBlockingUi(screenPoint);
        }

        // A press on a HUD button or popup must not also pick the food behind it. Tutorial overlays and
        // world-space canvases (tray counter) are ignored: the tutorial gate decides those clicks.
        private static bool IsOverBlockingUi(Vector2 screenPoint)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            UiHits.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPoint }, UiHits);
            foreach (var hit in UiHits)
            {
                if (hit.module is GraphicRaycaster raycaster && raycaster.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace) continue;
                if (hit.gameObject.GetComponentInParent<TutorialView>(true) != null) continue;
                return true;
            }

            return false;
        }

        public static float ScreenDistance(Camera camera, Vector3 worldPoint, Vector2 screenPoint)
        {
            Vector3 projected = camera.WorldToScreenPoint(worldPoint);
            if (projected.z < 0f) return float.MaxValue;
            return Vector2.Distance(new Vector2(projected.x, projected.y), screenPoint);
        }
    }
}
