using System.Collections.Generic;
using RestaurantLoop.Audio;
using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{

    public class ConveyorEntryInput : IInitializable, ITickable, System.IDisposable
    {
        private class PendingClick
        {
            public FoodStackData Stack;
            public FoodItemController Visual;
            public float EntryProgress;
        }

        private readonly QueueAnchor _queueAnchor;
        private readonly QueueSpawner _queueSpawner;
        private readonly ExitStackAnchor _exitStackAnchor;
        private readonly ConveyorManager _conveyorManager;
        private readonly ConveyorTuning _tuning;
        private readonly FoodFlightService _flightService;
        private readonly ConveyorFeelSettings _feel;
        private readonly FoodItemController.Pool _foodPool;
        private readonly SignalBus _signalBus;
        private readonly IAudioService _audioService;
        private readonly AudioLibrary _audioLibrary;

        // Items detached from the queue/exit stack while the belt entry was occupied. They're already
        // flying (see SendToBelt) — this just remembers what to hand ConveyorManager once room opens.
        private readonly Queue<PendingClick> _pendingEntries = new();

        private bool _wasMouseDown;

        public ConveyorEntryInput(
            QueueAnchor queueAnchor,
            QueueSpawner queueSpawner,
            ExitStackAnchor exitStackAnchor,
            ConveyorManager conveyorManager,
            ConveyorTuning tuning,
            FoodFlightService flightService,
            ConveyorFeelSettings feel,
            FoodItemController.Pool foodPool,
            SignalBus signalBus,
            IAudioService audioService,
            AudioLibrary audioLibrary)
        {
            _queueAnchor = queueAnchor;
            _queueSpawner = queueSpawner;
            _exitStackAnchor = exitStackAnchor;
            _conveyorManager = conveyorManager;
            _tuning = tuning;
            _flightService = flightService;
            _feel = feel;
            _foodPool = foodPool;
            _signalBus = signalBus;
            _audioService = audioService;
            _audioLibrary = audioLibrary;
        }

        public void Initialize()
        {
            _signalBus.Subscribe<BeltClearedSignal>(OnBeltCleared);
        }

        public void Dispose()
        {
            _signalBus.TryUnsubscribe<BeltClearedSignal>(OnBeltCleared);
            _pendingEntries.Clear();
        }

        public void Tick()
        {
            ProcessPendingEntries();

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

        // Promotes staged items to real belt entries the instant the entry cell clears. Wherever the
        // staging flight has the visual right now becomes the new flight's source, so it reads as one
        // continuous approach into the belt rather than a wait-then-snap.
        private void ProcessPendingEntries()
        {
            while (_pendingEntries.Count > 0 && _conveyorManager.CanAcceptEntry())
            {
                var pending = _pendingEntries.Dequeue();
                if (pending.Visual == null) continue;

                _conveyorManager.AddItemAtEntry(pending.Stack, pending.Visual, pending.Visual.transform.position, pending.EntryProgress);
            }
        }

        // Sends a detached item toward the belt. If the entry cell is free it enters right away; if not,
        // the click still feels instant — the item flies to a staging spot just behind the entry and
        // ProcessPendingEntries hands it off once room opens, with no extra click needed.
        private void SendToBelt(FoodStackData stack, FoodItemController visual, float entryProgress)
        {
            if (_conveyorManager.CanAcceptEntry())
            {
                _conveyorManager.AddItemAtEntry(stack, visual, visual.transform.position, entryProgress);
                return;
            }

            // Spread staged items further back the deeper the queue already is, so a burst of blocked
            // clicks doesn't pile every item onto the same staging spot.
            float stagingProgress = -1f - _pendingEntries.Count * 0.6f;
            Vector3 stagingPosition = _conveyorManager.SampleWorldPosition(stagingProgress);

            _pendingEntries.Enqueue(new PendingClick { Stack = stack, Visual = visual, EntryProgress = entryProgress });
            _flightService.Launch(visual.transform, stagingPosition, _feel.QueueToBelt);
        }

        private bool TryHandleQueueClick(Camera camera, Vector2 screenPoint)
        {
            int bestLane = -1;
            int bestRow = -1;
            FoodItemController bestVisual = null;
            float bestDistance = _tuning.ClickRadiusPixels;

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

                if (_conveyorManager.ItemCount + _pendingEntries.Count + _exitStackAnchor.OccupiedCount + 2 > _exitStackAnchor.SlotCount) return true;

                _queueSpawner.TryDetachAt(bestLane, 0, out var linkedVisual);
                _queueSpawner.TryDetachAt(partnerLane, 0, out var linkedPartnerVisual);
                _queueSpawner.PickFromBottomEnabled = false;

                // The queue visuals themselves fly onto the belt — no despawn + respawn snap.
                // Partner enters one grid cell behind so the linked pair doesn't spawn stacked on the same cell.
                SendToBelt(stack, linkedVisual, 0f);
                SendToBelt(partnerVisual.Stack, linkedPartnerVisual, -1f);
                _audioService.PlaySfx(_audioLibrary.FoodSendSfx);
                return true;
            }

            if (_conveyorManager.ItemCount + _pendingEntries.Count + _exitStackAnchor.OccupiedCount >= _exitStackAnchor.SlotCount) return false;

            _queueSpawner.TryDetachAt(bestLane, bestRow, out var pickedVisual);
            _queueSpawner.PickFromBottomEnabled = false;
            SendToBelt(stack, pickedVisual, 0f);
            _audioService.PlaySfx(_audioLibrary.FoodSendSfx);
            return true;
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
            SendToBelt(visual.Stack, visual, 0f);
            _audioService.PlaySfx(_audioLibrary.FoodSendSfx);
            return true;
        }

        private void OnBeltCleared()
        {
            while (_pendingEntries.Count > 0)
            {
                var pending = _pendingEntries.Dequeue();
                if (pending.Visual == null) continue;

                _flightService.Cancel(pending.Visual.transform);
                _foodPool.Despawn(pending.Visual);
            }
        }

        private static float ScreenDistance(Camera camera, Vector3 worldPoint, Vector2 screenPoint)
        {
            Vector3 projected = camera.WorldToScreenPoint(worldPoint);
            if (projected.z < 0f) return float.MaxValue;
            return Vector2.Distance(new Vector2(projected.x, projected.y), screenPoint);
        }
    }
}
