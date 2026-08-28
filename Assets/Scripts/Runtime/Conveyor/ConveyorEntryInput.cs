using RestaurantLoop.Audio;
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
        private readonly ConveyorTuning _tuning;
        private readonly TableSettings _tableSettings;
        private readonly CameraController _cameraController;
        private readonly Camera _camera;
        private readonly IAudioService _audioService;
        private readonly AudioLibrary _audioLibrary;

        private bool _wasMouseDown;

        public ConveyorEntryInput(
            QueueAnchor queueAnchor,
            QueueSpawner queueSpawner,
            ExitStackAnchor exitStackAnchor,
            ConveyorManager conveyorManager,
            ConveyorTuning tuning,
            [Inject(Optional = true)] TableSettings tableSettings,
            [Inject(Optional = true)] CameraController cameraController,
            [Inject(Optional = true)] Camera camera,
            IAudioService audioService,
            AudioLibrary audioLibrary)
        {
            _queueAnchor = queueAnchor;
            _queueSpawner = queueSpawner;
            _exitStackAnchor = exitStackAnchor;
            _conveyorManager = conveyorManager;
            _tuning = tuning;
            _tableSettings = tableSettings;
            _cameraController = cameraController;
            _camera = camera;
            _audioService = audioService;
            _audioLibrary = audioLibrary;
        }

        public void Tick()
        {
            bool isMouseDown = Input.GetMouseButton(0);
            bool clicked = isMouseDown && !_wasMouseDown;
            _wasMouseDown = isMouseDown;
            if (!clicked) return;

            var camera = _camera != null ? _camera : (_cameraController != null && _cameraController.Camera != null ? _cameraController.Camera : Camera.main);
            if (camera == null) return;

            Vector2 screenPoint = Input.mousePosition;

            if (TryHandleQueueClick(camera, screenPoint)) return;
            TryHandleExitStackClick(camera, screenPoint);
        }

        private bool TryHandleQueueClick(Camera camera, Vector2 screenPoint)
        {
            int bestLane = -1;
            int bestRow = -1;
            FoodItemController bestVisual = null;

            // Compute dynamic click radius taking into account screen size and camera zoom factor
            float baseRadius = _tuning.ClickRadiusPixels > 0f ? _tuning.ClickRadiusPixels : 80f;
            float zoom = _cameraController != null ? _cameraController.CameraZoomFactor : 1f;
            // Larger radius on zoomed out cameras / higher resolution screens
            float bestDistance = baseRadius * Mathf.Max(1f, 1f / zoom) * 1.5f;

            for (int lane = 0; lane < _queueAnchor.LanePositions.Length; lane++)
            {
                // Pick From Bottom power-up: every row is pickable, not just the front.
                int rowLimit = _queueSpawner.PickFromBottomEnabled ? _queueSpawner.GetLaneCount(lane) : Mathf.Min(1, _queueSpawner.GetLaneCount(lane));

                for (int row = 0; row < rowLimit; row++)
                {
                    if (!_queueSpawner.TryGetVisual(lane, row, out var visual)) continue;

                    float distance = ScreenDistance(camera, visual.VisualCenter, screenPoint);
                    if (distance >= bestDistance) continue;

                    bestDistance = distance;
                    bestLane = lane;
                    bestRow = row;
                    bestVisual = visual;
                }
            }

            if (bestLane < 0) return false;

            var stack = bestVisual.Stack;
            _audioService.PlaySfx(_audioLibrary.FoodClickSfx != null ? _audioLibrary.FoodClickSfx : _audioLibrary.DefaultClickSfx);

            if (stack.LinkId != 0)
            {
                // both halves must be at the front of their lane, or neither goes
                if (bestRow != 0 || !TryFindLinkedFrontPartner(stack.LinkId, bestLane, out int partnerLane, out var partnerVisual))
                    return true;

                if (_conveyorManager.ReservedItemCount + _exitStackAnchor.OccupiedCount + 2 > _exitStackAnchor.SlotCount)
                {
                    TriggerRejectFeedback();
                    return true;
                }

                _queueSpawner.TryDetachAt(bestLane, 0, out var linkedVisual);
                _queueSpawner.TryDetachAt(partnerLane, 0, out var linkedPartnerVisual);
                _queueSpawner.PickFromBottomEnabled = false;

                // The queue visuals themselves fly onto the belt — no despawn + respawn snap. Goes through
                // the pair-atomic entry point so both always land together (1 cell apart) instead of
                // independently racing the entry-spacing gate and potentially splitting apart.
                _conveyorManager.AddLinkedPairAtEntry(
                    stack, linkedVisual, linkedVisual.transform.position,
                    partnerVisual.Stack, linkedPartnerVisual, linkedPartnerVisual.transform.position);
                _audioService.PlaySfx(_audioLibrary.FoodSendSfx);
                return true;
            }

            if (_conveyorManager.ReservedItemCount + _exitStackAnchor.OccupiedCount >= _exitStackAnchor.SlotCount)
            {
                TriggerRejectFeedback();
                return false;
            }

            _queueSpawner.TryDetachAt(bestLane, bestRow, out var pickedVisual);
            _queueSpawner.PickFromBottomEnabled = false;
            _conveyorManager.AddItemAtEntry(stack, pickedVisual, pickedVisual.transform.position);
            _audioService.PlaySfx(_audioLibrary.FoodSendSfx);
            return true;
        }

        private void TriggerRejectFeedback()
        {
            var counterUI = Object.FindAnyObjectByType<TrayDispenserCounterUI>();
            if (counterUI != null)
            {
                counterUI.PlayRejectAnimation();
            }
        }

        private bool TryFindLinkedFrontPartner(int linkId, int excludeLane, out int partnerLane, out FoodItemController partnerVisual)
        {
            for (int lane = 0; lane < _queueAnchor.LanePositions.Length; lane++)
            {
                if (lane == excludeLane) continue;
                if (!_queueSpawner.TryGetVisual(lane, 0, out var visual)) continue;
                if (visual.Stack.LinkId != linkId) continue;

                partnerLane = lane;
                partnerVisual = visual;
                return true;
            }

            partnerLane = -1;
            partnerVisual = null;
            return false;
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

            _audioService.PlaySfx(_audioLibrary.FoodClickSfx != null ? _audioLibrary.FoodClickSfx : _audioLibrary.DefaultClickSfx);

            _exitStackAnchor.TryRemoveFromSlot(bestSlot, out var visual);
            _conveyorManager.AddItemAtEntry(visual.Stack, visual, visual.transform.position);
            _audioService.PlaySfx(_audioLibrary.FoodSendSfx);
            return true;
        }

        private static float ScreenDistance(Camera camera, Vector3 worldPoint, Vector2 screenPoint)
        {
            Vector3 projected = camera.WorldToScreenPoint(worldPoint);
            if (projected.z < 0f) return float.MaxValue;
            return Vector2.Distance(new Vector2(projected.x, projected.y), screenPoint);
        }
    }
}
