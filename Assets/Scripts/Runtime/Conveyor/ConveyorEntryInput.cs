using System;
using System.Collections.Generic;
using RestaurantLoop.Audio;
using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Tutorial;
using RestaurantLoop.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class ConveyorEntryInput : ITickable, IInitializable, IDisposable
    {
        private readonly QueueAnchor _queueAnchor;
        private readonly QueueSpawner _queueSpawner;
        private readonly ExitStackAnchor _exitStackAnchor;
        private readonly ConveyorManager _conveyorManager;
        private readonly TrayDispenserAnchor _trayAnchor;
        private readonly ConveyorTuning _tuning;
        private readonly TableSettings _tableSettings;
        private readonly CameraController _cameraController;
        private readonly Camera _camera;
        private readonly FoodFlightService _flightService;
        private readonly IAudioService _audioService;
        private readonly AudioLibrary _audioLibrary;
        private readonly TutorialInputGate _gate;
        private readonly SignalBus _signalBus;

        private readonly Dictionary<FoodItemController, float> _dockSettleUntil = new();
        private bool _wasMouseDown;

        public ConveyorEntryInput(
            QueueAnchor queueAnchor,
            QueueSpawner queueSpawner,
            ExitStackAnchor exitStackAnchor,
            ConveyorManager conveyorManager,
            [Inject(Optional = true)] TrayDispenserAnchor trayAnchor,
            ConveyorTuning tuning,
            [Inject(Optional = true)] TableSettings tableSettings,
            [Inject(Optional = true)] CameraController cameraController,
            [Inject(Optional = true)] Camera camera,
            [Inject(Optional = true)] FoodFlightService flightService,
            IAudioService audioService,
            AudioLibrary audioLibrary,
            TutorialInputGate gate,
            SignalBus signalBus)
        {
            _queueAnchor = queueAnchor;
            _queueSpawner = queueSpawner;
            _exitStackAnchor = exitStackAnchor;
            _conveyorManager = conveyorManager;
            _trayAnchor = trayAnchor;
            _tuning = tuning;
            _tableSettings = tableSettings;
            _cameraController = cameraController;
            _camera = camera;
            _flightService = flightService;
            _audioService = audioService;
            _audioLibrary = audioLibrary;
            _gate = gate;
            _signalBus = signalBus;
        }

        public void Initialize()
        {
            _signalBus.Subscribe<FoodDockLandedSignal>(OnFoodDockLanded);
        }

        public void Dispose()
        {
            _signalBus.TryUnsubscribe<FoodDockLandedSignal>(OnFoodDockLanded);
            _dockSettleUntil.Clear();
        }

        private void OnFoodDockLanded(FoodDockLandedSignal signal)
        {
            if (signal.Visual != null)
            {
                float cooldown = _tuning != null && _tuning.ExitRackSendCooldown > 0f ? _tuning.ExitRackSendCooldown : 0.35f;
                _dockSettleUntil[signal.Visual] = Time.time + cooldown;
            }
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
            if (!_gate.CanClickQueueLane(bestLane, bestRow)) return true;

            var stack = bestVisual.Stack;
            _audioService.PlaySfx(_audioLibrary.FoodClickSfx != null ? _audioLibrary.FoodClickSfx : _audioLibrary.DefaultClickSfx);

            // Boştaki tepsi sayısı: Toplam tepsi sayısından (aktif banttakiler + havadakiler) düşülerek net hesaplanır
            int maxTrays = _trayAnchor != null ? _trayAnchor.TotalCount : _exitStackAnchor.SlotCount;
            int currentReservedOrOnBelt = _conveyorManager.ReservedItemCount;
            int netAvailableTrays = maxTrays - currentReservedOrOnBelt;

            if (stack.LinkId != 0)
            {
                if (!TryFindLinkedPartnerInQueue(stack.LinkId, bestLane, bestRow, out int partnerLane, out int partnerRow, out var partnerVisual))
                {
                    return true;
                }

                // If not in pick-from-bottom mode, both must be accessible at the front:
                bool pickFromBottom = _queueSpawner.PickFromBottomEnabled;
                bool isBestFront = bestRow == 0 || pickFromBottom;
                bool isPartnerFront = (partnerLane == bestLane && (partnerRow == 0 || partnerRow == 1)) || partnerRow == 0 || pickFromBottom;

                if (!isBestFront || !isPartnerFront)
                {
                    return true;
                }

                if (netAvailableTrays < 2)
                {
                    TriggerRejectFeedback();
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

                // The queue visuals themselves fly onto the belt — no despawn + respawn snap. Goes through
                // the pair-atomic entry point so both always land together (1 cell apart) instead of
                // independently racing the entry-spacing gate and potentially splitting apart.
                _conveyorManager.AddLinkedPairAtEntry(
                    stack, linkedVisual, linkedVisual.transform.position,
                    partnerVisual.Stack, linkedPartnerVisual, linkedPartnerVisual.transform.position);
                _audioService.PlaySfx(_audioLibrary.FoodSendSfx);
                _signalBus.Fire(new TutorialActionPerformedSignal
                    { Kind = TutorialHoverTargetKind.QueueLane, QueueLaneIndex = bestLane, QueueRowIndex = bestRow });
                return true;
            }

            if (netAvailableTrays < 1)
            {
                TriggerRejectFeedback();
                return false;
            }

            _queueSpawner.TryDetachAt(bestLane, bestRow, out var pickedVisual);
            _queueSpawner.PickFromBottomEnabled = false;
            _conveyorManager.AddItemAtEntry(stack, pickedVisual, pickedVisual.transform.position);
            _audioService.PlaySfx(_audioLibrary.FoodSendSfx);
            _signalBus.Fire(new TutorialActionPerformedSignal
                { Kind = TutorialHoverTargetKind.QueueLane, QueueLaneIndex = bestLane, QueueRowIndex = bestRow });
            return true;
        }

        private void TriggerRejectFeedback()
        {
            var counterUI = UnityEngine.Object.FindAnyObjectByType<TrayDispenserCounterUI>();
            if (counterUI != null)
            {
                counterUI.PlayRejectAnimation();
            }
        }

        private bool TryFindLinkedPartnerInQueue(int linkId, int excludeLane, int excludeRow, out int partnerLane, out int partnerRow, out FoodItemController partnerVisual)
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

        private bool CanPickRackOccupant(FoodItemController occupant)
        {
            if (occupant == null) return false;

            if (_flightService != null && _flightService.IsFlying(occupant.transform))
                return false;

            if (_dockSettleUntil.TryGetValue(occupant, out float settleTime) && Time.time < settleTime)
                return false;

            return true;
        }

        private bool TryHandleExitStackClick(Camera camera, Vector2 screenPoint)
        {
            int bestSlot = -1;
            float bestDistance = _tuning.ClickRadiusPixels;

            for (int i = 0; i < _exitStackAnchor.SlotPositions.Length; i++)
            {
                if (!_exitStackAnchor.TryGetOccupant(i, out var occupant)) continue;
                if (!CanPickRackOccupant(occupant)) continue;

                float distance = ScreenDistance(camera, occupant.VisualCenter, screenPoint);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                bestSlot = i;
            }

            if (bestSlot < 0) return false;
            if (!_gate.CanClickExitStack()) return true;

            // Rack'ten gönderirken de toplam tepsi kapasitesini aşmamak için kontrol
            int maxTrays = _trayAnchor != null ? _trayAnchor.TotalCount : _exitStackAnchor.SlotCount;
            int currentReservedOrOnBelt = _conveyorManager.ReservedItemCount;
            if (currentReservedOrOnBelt >= maxTrays)
            {
                TriggerRejectFeedback();
                return false;
            }

            _audioService.PlaySfx(_audioLibrary.FoodClickSfx != null ? _audioLibrary.FoodClickSfx : _audioLibrary.DefaultClickSfx);

            if (_exitStackAnchor.TryGetOccupant(bestSlot, out var clickedOccupant) && clickedOccupant.Stack.LinkId != 0)
            {
                int linkId = clickedOccupant.Stack.LinkId;
                int partnerSlot = -1;
                FoodItemController partnerVisual = null;

                for (int i = 0; i < _exitStackAnchor.SlotPositions.Length; i++)
                {
                    if (i == bestSlot) continue;
                    if (_exitStackAnchor.TryGetOccupant(i, out var occ) && occ.Stack.LinkId == linkId)
                    {
                        partnerSlot = i;
                        partnerVisual = occ;
                        break;
                    }
                }

                if (partnerSlot >= 0 && partnerVisual != null)
                {
                    if (!CanPickRackOccupant(partnerVisual))
                    {
                        return true;
                    }

                    if (currentReservedOrOnBelt + 2 > maxTrays)
                    {
                        TriggerRejectFeedback();
                        return false;
                    }

                    _exitStackAnchor.TryRemoveFromSlot(bestSlot, out var v1);
                    _exitStackAnchor.TryRemoveFromSlot(partnerSlot, out var v2);
                    _exitStackAnchor.CompactSlots();
                    _conveyorManager.AddLinkedPairAtEntry(
                        v1.Stack, v1, v1.transform.position,
                        v2.Stack, v2, v2.transform.position);
                    _audioService.PlaySfx(_audioLibrary.FoodSendSfx);
                    return true;
                }
            }

            _exitStackAnchor.TryRemoveFromSlot(bestSlot, out var visual);
            _exitStackAnchor.CompactSlots();
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
