using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Moves the dispenser's trays with the food: one tray per belt item, trayless items wait for the next
    // free tray, and released trays fly back to the dispenser.
    public class TrayVisualService : IInitializable, ITickable, IDisposable
    {
        private readonly SignalBus _signalBus;
        private readonly ConveyorManager _conveyorManager;
        private readonly ConveyorTuning _tuning;
        private readonly ConveyorFeelSettings _feel;
        private readonly FoodFlightService _flightService;
        private readonly TrayDispenserAnchor _trayAnchor;

        private readonly Dictionary<int, TrayController> _rides = new();
        private readonly HashSet<int> _ridingIds = new();
        private readonly List<int> _traylessIds = new();

        // Loop mode re-enters the same visual under a new id; its tray waits here until that happens.
        private readonly Dictionary<int, FoodItemController> _visualsById = new();
        private readonly Dictionary<FoodItemController, TrayController> _loopedTrays = new();

        public TrayVisualService(
            SignalBus signalBus,
            ConveyorManager conveyorManager,
            ConveyorTuning tuning,
            ConveyorFeelSettings feel,
            FoodFlightService flightService,
            TrayDispenserAnchor trayAnchor)
        {
            _signalBus = signalBus;
            _conveyorManager = conveyorManager;
            _tuning = tuning;
            _feel = feel;
            _flightService = flightService;
            _trayAnchor = trayAnchor;
        }

        public void Initialize()
        {
            _signalBus.Subscribe<FoodEnteredBeltSignal>(OnFoodEnteredBelt);
            _signalBus.Subscribe<FoodDockedSignal>(OnFoodDocked);
            _signalBus.Subscribe<FoodLoopedSignal>(OnFoodLooped);
            _signalBus.Subscribe<FoodConsumedSignal>(OnFoodConsumed);
            _signalBus.Subscribe<BeltClearedSignal>(OnBeltCleared);
        }

        public void Dispose()
        {
            _signalBus.TryUnsubscribe<FoodEnteredBeltSignal>(OnFoodEnteredBelt);
            _signalBus.TryUnsubscribe<FoodDockedSignal>(OnFoodDocked);
            _signalBus.TryUnsubscribe<FoodLoopedSignal>(OnFoodLooped);
            _signalBus.TryUnsubscribe<FoodConsumedSignal>(OnFoodConsumed);
            _signalBus.TryUnsubscribe<BeltClearedSignal>(OnBeltCleared);

            ReleaseAllRidesImmediately();
        }

        public void Tick()
        {
            AdvanceRidingTrays();
            TryAssignWaitingTrayless();
        }

        private void AdvanceRidingTrays()
        {
            if (_ridingIds.Count == 0) return;

            foreach (var id in _ridingIds)
            {
                if (!_rides.TryGetValue(id, out var tray) || tray == null) continue;

                var position = _conveyorManager.SampleWorldPosition(_conveyorManager.GetProgressById(id));
                position.y = _tuning.TrayBeltHeight;
                tray.SetRidePosition(position);
            }
        }

        private void OnFoodEnteredBelt(FoodEnteredBeltSignal signal)
        {
            int id = signal.ItemId;
            if (signal.Visual == null) return;

            _visualsById[id] = signal.Visual;

            if (_loopedTrays.Remove(signal.Visual, out var loopedTray) && loopedTray != null)
            {
                StartTrayEntry(id, loopedTray);
                return;
            }

            if (!_trayAnchor.TryAcquire(out var tray))
            {
                _traylessIds.Add(id);
                return;
            }

            StartTrayEntry(id, tray);
        }

        private void StartTrayEntry(int id, TrayController tray)
        {
            _rides[id] = tray;

            Vector3 TargetPosition()
            {
                var position = _conveyorManager.SampleWorldPosition(_conveyorManager.GetProgressById(id));
                position.y = _tuning.TrayBeltHeight;
                return position;
            }

            float flightDuration =
                _feel.TrayDispenserFlight.ResolveDuration(Vector3.Distance(tray.transform.position, TargetPosition()));

            tray.EnterBelt(
                TargetPosition,
                tray.ResolveFlatLocalRotation(),
                flightDuration,
                _feel.TrayDispenserFlight,
                _flightService,
                () => _ridingIds.Add(id));
        }

        private void OnFoodDocked(FoodDockedSignal signal) => HandleItemLeavingBelt(signal.ItemId);

        // The old id is gone; keep its tray for the same visual's re-entry under the new id.
        private void OnFoodLooped(FoodLoopedSignal signal)
        {
            int id = signal.ItemId;
            _traylessIds.Remove(id);
            _ridingIds.Remove(id);
            _visualsById.Remove(id, out var visual);

            if (!_rides.Remove(id, out var tray) || tray == null) return;

            _flightService.Cancel(tray.transform);

            if (visual != null)
            {
                _loopedTrays[visual] = tray;
                return;
            }

            ReleaseTray(tray);
        }

        private void OnFoodConsumed(FoodConsumedSignal signal)
        {
            if (!signal.ItemFullyConsumed) return;

            HandleItemLeavingBelt(signal.ItemId);

            if (signal.PartnerItemId.HasValue)
            {
                HandleItemLeavingBelt(signal.PartnerItemId.Value);
            }
        }

        private void HandleItemLeavingBelt(int id)
        {
            _traylessIds.Remove(id);
            _visualsById.Remove(id);

            if (!_rides.TryGetValue(id, out var tray)) return;
            _rides.Remove(id);
            _ridingIds.Remove(id);
            if (tray == null) return;

            _flightService.Cancel(tray.transform);
            ReleaseTray(tray);
        }

        // Hands the tray to the oldest trayless item, or flies it back to the dispenser.
        private void ReleaseTray(TrayController tray)
        {
            if (_traylessIds.Count > 0)
            {
                int waitingId = _traylessIds[0];
                _traylessIds.RemoveAt(0);
                StartTrayEntry(waitingId, tray);
                return;
            }

            var slotPose = _trayAnchor.ReserveReturnSlot(tray);
            tray.ReturnToInitialPose(
                slotPose.Position,
                slotPose.Rotation,
                slotPose.Scale,
                _feel.TrayDispenserFlight,
                _flightService,
                () =>
                {
                    _trayAnchor.ConfirmReturn(tray);
                    TryAssignWaitingTrayless();
                });
        }

        private void TryAssignWaitingTrayless()
        {
            while (_traylessIds.Count > 0 && _trayAnchor.TryAcquire(out var tray))
            {
                int id = _traylessIds[0];
                _traylessIds.RemoveAt(0);
                StartTrayEntry(id, tray);
            }
        }

        private void OnBeltCleared() => ReleaseAllRidesImmediately();

        private void ReleaseAllRidesImmediately()
        {
            foreach (var tray in _rides.Values)
            {
                if (tray == null) continue;
                _flightService.Cancel(tray.transform);
            }

            foreach (var tray in _loopedTrays.Values)
            {
                if (tray == null) continue;
                _flightService.Cancel(tray.transform);
            }

            _rides.Clear();
            _ridingIds.Clear();
            _traylessIds.Clear();
            _visualsById.Clear();
            _loopedTrays.Clear();
            _trayAnchor.ResetAll();
        }
    }
}