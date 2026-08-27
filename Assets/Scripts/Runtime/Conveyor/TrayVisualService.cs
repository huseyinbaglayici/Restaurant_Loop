using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Visual-only companion to ConveyorVisualService: pairs one of the dispenser's 5 tray children with
    // every belt item (same ItemId) by making the tray a child of the food's transform for the ride —
    // not a second position tracker kept in lockstep with the food's every tick, which is what used to
    // drift. Waits for ConveyorVisualService's FoodBeltEntryLandedSignal before flying a tray in from the
    // dispenser, then attaches it the instant that flight lands; releases it back to the dispenser either
    // with a shrink punch (food fully eaten) or without one (food completed its lap and docked).
    public class TrayVisualService : IInitializable, ITickable, IDisposable
    {
        private readonly struct DeferredCommand
        {
            public readonly float DueTime;
            public readonly Action Command;

            public DeferredCommand(float dueTime, Action command)
            {
                DueTime = dueTime;
                Command = command;
            }
        }

        private readonly SignalBus _signalBus;
        private readonly ConveyorManager _conveyorManager;
        private readonly ConveyorTuning _tuning;
        private readonly ConveyorFeelSettings _feel;
        private readonly FoodFlightService _flightService;
        private readonly TrayDispenserAnchor _trayAnchor;

        private readonly Dictionary<int, TrayController> _rides = new();
        private readonly Queue<DeferredCommand> _deferred = new();

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
            _signalBus.Subscribe<FoodBeltEntryLandedSignal>(OnFoodBeltEntryLanded);
            _signalBus.Subscribe<FoodDockedSignal>(OnFoodDocked);
            _signalBus.Subscribe<FoodConsumedSignal>(OnFoodConsumed);
            _signalBus.Subscribe<BeltClearedSignal>(OnBeltCleared);
        }

        public void Dispose()
        {
            _signalBus.TryUnsubscribe<FoodBeltEntryLandedSignal>(OnFoodBeltEntryLanded);
            _signalBus.TryUnsubscribe<FoodDockedSignal>(OnFoodDocked);
            _signalBus.TryUnsubscribe<FoodConsumedSignal>(OnFoodConsumed);
            _signalBus.TryUnsubscribe<BeltClearedSignal>(OnBeltCleared);

            _deferred.Clear();
            ReleaseAllRidesImmediately();
        }

        public void Tick()
        {
            FlushDueCommands();
        }

        private void FlushDueCommands()
        {
            float now = Time.time;
            while (_deferred.Count > 0 && _deferred.Peek().DueTime <= now)
                _deferred.Dequeue().Command?.Invoke();
        }

        private void Defer(Action command) =>
            _deferred.Enqueue(new DeferredCommand(Time.time + Mathf.Max(0f, _feel.VisualLagSeconds), command));

        // Fires once the food's own belt-entry flight has actually landed (or immediately, for
        // level-authored items that never played one). The tray flies in from the dispenser toward that
        // landing point, then AttachToRider makes it a child of the food — from then on the food's own
        // transform is the only thing driving where the tray sits; there is nothing left to keep in sync.
        private void OnFoodBeltEntryLanded(FoodBeltEntryLandedSignal signal)
        {
            int id = signal.ItemId;
            var foodVisual = signal.Visual;
            float landedProgress = signal.Progress;

            if (foodVisual == null) return;

            Defer(() =>
            {
                if (!_trayAnchor.TryAcquire(out var tray)) return; // no tray free — item rides without one

                _rides[id] = tray;

                tray.EnterBelt(
                    () =>
                    {
                        var position = _conveyorManager.SampleWorldPosition(landedProgress);
                        position.y = _tuning.TrayBeltHeight;
                        return position;
                    },
                    tray.ResolveFlatLocalRotation(),
                    _feel.TrayRotateDuration,
                    _feel.DispenserToBelt,
                    _flightService,
                    () => tray.AttachToRider(foodVisual.transform, _tuning.TrayBeltHeight));
            });
        }

        private void OnFoodDocked(FoodDockedSignal signal) => ReturnRide(signal.ItemId, withPunch: false);

        private void OnFoodConsumed(FoodConsumedSignal signal)
        {
            if (!signal.ItemFullyConsumed) return; // partial take: item (and its tray) stay on the belt
            ReturnRide(signal.ItemId, withPunch: true);
        }

        private void ReturnRide(int id, bool withPunch)
        {
            // Detached immediately, not deferred: ConveyorVisualService reacts to the same signal on the
            // same frame by flying the food itself (to the exit stack, or to the customer) — if the tray
            // were still attached when that flight starts ticking, it would ride along for a few frames
            // before snapping back. Detaching now (worldPositionStays keeps it exactly where it is) just
            // stops it from moving with the food; the return flourish still starts on the usual visual lag.
            if (!_rides.TryGetValue(id, out var tray)) return;
            _rides.Remove(id);
            if (tray == null) return;

            tray.DetachFromRider();

            Defer(() =>
            {
                var slotPose = _trayAnchor.ReserveReturnSlot(tray);
                tray.ReturnToInitialPose(
                    slotPose.Position,
                    slotPose.Rotation,
                    slotPose.Scale,
                    withPunch,
                    _feel.TrayVanishDuration,
                    _feel.TrayVanishPunchOvershoot,
                    () => _trayAnchor.ConfirmReturn(tray));
            });
        }

        private void OnBeltCleared()
        {
            _deferred.Clear();
            ReleaseAllRidesImmediately();
        }

        private void ReleaseAllRidesImmediately()
        {
            foreach (var tray in _rides.Values)
            {
                if (tray == null) continue;
                _flightService.Cancel(tray.transform);
            }

            _rides.Clear();
            _trayAnchor.ResetAll(); // SnapToInitialPose detaches any tray still parented to a food item
        }
    }
}
