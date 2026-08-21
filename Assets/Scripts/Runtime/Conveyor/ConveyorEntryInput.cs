using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{

    public class ConveyorEntryInput : ITickable
    {
        private readonly QueueAnchor _queueAnchor;
        private readonly QueueSpawner _queueSpawner;
        private readonly ExitStackAnchor _exitStackAnchor;
        private readonly ConveyorManager _conveyorManager;
        private readonly FoodItemController.Pool _foodPool;
        private readonly ConveyorTuning _tuning;

        private bool _wasMouseDown;

        public ConveyorEntryInput(
            QueueAnchor queueAnchor,
            QueueSpawner queueSpawner,
            ExitStackAnchor exitStackAnchor,
            ConveyorManager conveyorManager,
            FoodItemController.Pool foodPool,
            ConveyorTuning tuning)
        {
            _queueAnchor = queueAnchor;
            _queueSpawner = queueSpawner;
            _exitStackAnchor = exitStackAnchor;
            _conveyorManager = conveyorManager;
            _foodPool = foodPool;
            _tuning = tuning;
        }

        public void Tick()
        {
            bool isMouseDown = Input.GetMouseButton(0);
            bool clicked = isMouseDown && !_wasMouseDown;
            _wasMouseDown = isMouseDown;
            if (!clicked) return;

            var camera = Camera.main;
            if (camera == null) return;

            Vector2 screenPoint = Input.mousePosition;

            if (TryHandleQueueClick(camera, screenPoint)) return;
            TryHandleExitStackClick(camera, screenPoint);
        }

        private bool TryHandleQueueClick(Camera camera, Vector2 screenPoint)
        {
            int bestLane = -1;
            FoodItemController bestVisual = null;
            float bestDistance = _tuning.ClickRadiusPixels;

            for (int lane = 0; lane < _queueAnchor.LanePositions.Length; lane++)
            {
                if (!_queueSpawner.TryGetFrontVisual(lane, out var visual)) continue;

                float distance = ScreenDistance(camera, visual.VisualCenter, screenPoint);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                bestLane = lane;
                bestVisual = visual;
            }

            if (bestLane < 0) return false;

            var stack = bestVisual.Stack;
            _queueSpawner.PopFront(bestLane);
            _conveyorManager.AddItemAtEntry(stack);
            return true;
        }

        private bool TryHandleExitStackClick(Camera camera, Vector2 screenPoint)
        {
            int bestSlot = -1;
            float bestDistance = _tuning.ClickRadiusPixels;

            for (int i = 0; i < _exitStackAnchor.SlotPositions.Length; i++)
            {
                if (!_exitStackAnchor.TryGetOccupant(i, out var occupant)) continue;

                float distance = ScreenDistance(camera, occupant.VisualCenter, screenPoint);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                bestSlot = i;
            }

            if (bestSlot < 0) return false;

            _exitStackAnchor.TryRemoveFromSlot(bestSlot, out var visual);
            _conveyorManager.AddItemAtEntry(visual.Stack);
            _foodPool.Despawn(visual);
            return true;
        }

        // float.MaxValue for anything behind the camera, so it can never win the closest-match
        // comparison above.
        private static float ScreenDistance(Camera camera, Vector3 worldPoint, Vector2 screenPoint)
        {
            Vector3 projected = camera.WorldToScreenPoint(worldPoint);
            if (projected.z < 0f) return float.MaxValue;
            return Vector2.Distance(new Vector2(projected.x, projected.y), screenPoint);
        }
    }
}
