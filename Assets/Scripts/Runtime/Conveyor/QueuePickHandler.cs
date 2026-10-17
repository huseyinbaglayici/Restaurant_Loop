using DG.Tweening;
using RestaurantLoop.Runtime.Audio;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Signals;
using RestaurantLoop.Runtime.Tutorial;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Queue pick rules: which queue item a click hits, whether it may leave (front row / pick-from-bottom,
    // tutorial gate, free trays, linked partner) and sending it (or its linked pair) onto the belt.
    public class QueuePickHandler
    {
        private readonly QueueAnchor _queueAnchor;
        private readonly QueueSpawner _queueSpawner;
        private readonly ConveyorManager _conveyorManager;
        private readonly TrayDispenserAnchor _trayAnchor;
        private readonly ConveyorTuning _tuning;
        private readonly CameraController _cameraController;
        private readonly AudioService _audioService;
        private readonly AudioLibrary _audioLibrary;
        private readonly TutorialInputGate _gate;
        private readonly SignalBus _signalBus;

        public QueuePickHandler(
            QueueAnchor queueAnchor,
            QueueSpawner queueSpawner,
            ConveyorManager conveyorManager,
            TrayDispenserAnchor trayAnchor,
            ConveyorTuning tuning,
            CameraController cameraController,
            AudioService audioService,
            AudioLibrary audioLibrary,
            TutorialInputGate gate,
            SignalBus signalBus)
        {
            _queueAnchor = queueAnchor;
            _queueSpawner = queueSpawner;
            _conveyorManager = conveyorManager;
            _trayAnchor = trayAnchor;
            _tuning = tuning;
            _cameraController = cameraController;
            _audioService = audioService;
            _audioLibrary = audioLibrary;
            _gate = gate;
            _signalBus = signalBus;
        }

        // false = the click didn't consume the input, so the exit stack still gets a chance at it.
        public bool TryHandleClick(Camera camera, Vector2 screenPoint)
        {
            // Shuffle is still dealing items back into their lanes.
            if (_queueSpawner.IsAnimating) return false;

            int bestLane = -1;
            int bestRow = -1;
            FoodItemController bestVisual = null;

            // Click radius grows on zoomed-out cameras so small on-screen items stay easy to hit
            float baseRadius = _tuning.ClickRadiusPixels > 0f ? _tuning.ClickRadiusPixels : 80f;
            float zoom = _cameraController.CameraZoomFactor;
            float bestDistance = baseRadius * Mathf.Max(1f, 1f / zoom) * 1.5f;

            for (int lane = 0; lane < _queueAnchor.LanePositions.Length; lane++)
            {
                // Pick From Bottom power-up: every row is pickable, not just the front.
                int rowLimit = _queueSpawner.PickFromBottomEnabled ? _queueSpawner.GetLaneCount(lane) : Mathf.Min(1, _queueSpawner.GetLaneCount(lane));

                for (int row = 0; row < rowLimit; row++)
                {
                    if (!_queueSpawner.TryGetVisual(lane, row, out var visual)) continue;

                    float distance = ConveyorPointer.ScreenDistance(camera, visual.VisualCenter, screenPoint);
                    if (distance >= bestDistance) continue;

                    bestDistance = distance;
                    bestLane = lane;
                    bestRow = row;
                    bestVisual = visual;
                }
            }

            if (bestLane < 0) return false;
            if (!_gate.CanClickQueueLane(bestLane, bestRow)) return true;

            var stack = bestVisual.Stack;
            _audioService.PlaySfx(_audioLibrary.FoodClickSfx != null ? _audioLibrary.FoodClickSfx : _audioLibrary.DefaultClickSfx);

            // Free trays = total trays minus the ones already on the belt or reserved in flight
            int netAvailableTrays = _trayAnchor.TotalCount - _conveyorManager.ReservedItemCount;

            if (stack.LinkId != 0 && TryFindLinkedPartner(stack.LinkId, bestLane, bestRow, out int partnerLane, out int partnerRow, out var partnerVisual))
            {
                // If not in pick-from-bottom mode, both must be accessible at the front:
                bool pickFromBottom = _queueSpawner.PickFromBottomEnabled;
                bool isBestFront = bestRow == 0 || pickFromBottom;
                bool isPartnerFront = (partnerLane == bestLane && (partnerRow == 0 || partnerRow == 1)) || partnerRow == 0 || pickFromBottom;

                if (!isBestFront || !isPartnerFront)
                {
                    ShakeRejected(bestVisual);
                    ShakeRejected(partnerVisual);
                    _signalBus.Fire(new LinkedPairRejectedSignal { LinkId = stack.LinkId });
                    return true;
                }

                if (netAvailableTrays < 2)
                {
                    _signalBus.Fire<TrayDispenserRejectedSignal>();
                    return true;
                }

                FoodItemController linkedVisual;
                FoodItemController linkedPartnerVisual;

                if (bestLane == partnerLane)
                {
                    int firstRow = Mathf.Max(bestRow, partnerRow);
                    int secondRow = Mathf.Min(bestRow, partnerRow);
                    _queueSpawner.TryDetachAt(bestLane, firstRow, out var v1);
                    _queueSpawner.TryDetachAt(bestLane, secondRow, out var v2);
                    linkedVisual = (bestRow == secondRow) ? v2 : v1;
                    linkedPartnerVisual = (bestRow == secondRow) ? v1 : v2;
                }
                else
                {
                    _queueSpawner.TryDetachAt(bestLane, bestRow, out linkedVisual);
                    _queueSpawner.TryDetachAt(partnerLane, partnerRow, out linkedPartnerVisual);
                }

                _queueSpawner.PickFromBottomEnabled = false;
                _cameraController.FocusOnQueue(false);

                // The queue visuals themselves fly onto the belt — no despawn + respawn snap. Goes through
                // the pair-atomic entry point so both always land together (1 cell apart) instead of
                // independently racing the entry-spacing gate and potentially splitting apart.
                SquashTween.Play(linkedVisual.transform, 0.25f, 0.22f);
                SquashTween.Play(linkedPartnerVisual.transform, 0.25f, 0.22f);
                _signalBus.Fire(new LinkedPairPickedSignal { LinkId = stack.LinkId });

                _conveyorManager.AddLinkedPairAtEntry(
                    stack, linkedVisual, linkedVisual.transform.position,
                    partnerVisual.Stack, linkedPartnerVisual, linkedPartnerVisual.transform.position);
                _audioService.PlaySfx(_audioLibrary.FoodSendSfx);
                FireTutorialAction(bestLane, bestRow);
                return true;
            }

            if (netAvailableTrays < 1)
            {
                _signalBus.Fire<TrayDispenserRejectedSignal>();
                return false;
            }

            _queueSpawner.TryDetachAt(bestLane, bestRow, out var pickedVisual);
            _queueSpawner.PickFromBottomEnabled = false;
            _cameraController.FocusOnQueue(false);
            _conveyorManager.AddItemAtEntry(stack, pickedVisual, pickedVisual.transform.position);
            _audioService.PlaySfx(_audioLibrary.FoodSendSfx);
            FireTutorialAction(bestLane, bestRow);
            return true;
        }

        private void FireTutorialAction(int lane, int row)
        {
            _signalBus.Fire(new TutorialActionPerformedSignal
                { Kind = TutorialHoverTargetKind.QueueLane, QueueLaneIndex = lane, QueueRowIndex = row });
        }

        // "Not yet": a short sideways shake in screen space, so both halves read as stuck together.
        private void ShakeRejected(FoodItemController visual)
        {
            var itemTransform = visual.transform;
            itemTransform.DOKill(true);
            itemTransform.DOPunchPosition(_cameraController.Camera.transform.right * 0.18f, 0.35f, 14, 0.6f)
                .SetTarget(itemTransform);
        }

        private bool TryFindLinkedPartner(int linkId, int excludeLane, int excludeRow, out int partnerLane, out int partnerRow, out FoodItemController partnerVisual)
        {
            // First check same lane
            int sameLaneCount = _queueSpawner.GetLaneCount(excludeLane);
            for (int r = 0; r < sameLaneCount; r++)
            {
                if (r == excludeRow) continue;
                if (_queueSpawner.TryGetVisual(excludeLane, r, out var visual) && visual.Stack.LinkId == linkId)
                {
                    partnerLane = excludeLane;
                    partnerRow = r;
                    partnerVisual = visual;
                    return true;
                }
            }

            // Then check other lanes
            for (int lane = 0; lane < _queueAnchor.LanePositions.Length; lane++)
            {
                if (lane == excludeLane) continue;
                int count = _queueSpawner.GetLaneCount(lane);
                for (int r = 0; r < count; r++)
                {
                    if (_queueSpawner.TryGetVisual(lane, r, out var visual) && visual.Stack.LinkId == linkId)
                    {
                        partnerLane = lane;
                        partnerRow = r;
                        partnerVisual = visual;
                        return true;
                    }
                }
            }

            partnerLane = -1;
            partnerRow = -1;
            partnerVisual = null;
            return false;
        }
    }
}
