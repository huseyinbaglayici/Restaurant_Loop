using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Visual-only companion to ConveyorVisualService: pairs one of the dispenser's 5 tray children with
    // every belt item (same ItemId), keeping it directly under the food for the ride, then releasing it
    // back to the dispenser either with a shrink punch (food fully eaten) or without one (food completed
    // its lap and docked). Subscribes to the same signals ConveyorVisualService does, on the same visual
    // lag, so both layers react in the same frame.
    public class TrayVisualService : IInitializable, ITickable, IDisposable
    {
        private class TrayRide
        {
            public TrayController Tray;
            public float Progress;
            public bool IsEntering;
        }

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

        private readonly Dictionary<int, TrayRide> _rides = new();
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
            _signalBus.Subscribe<FoodEnteredBeltSignal>(OnFoodEnteredBelt);
            _signalBus.Subscribe<FoodDockedSignal>(OnFoodDocked);
            _signalBus.Subscribe<FoodConsumedSignal>(OnFoodConsumed);
            _signalBus.Subscribe<BeltClearedSignal>(OnBeltCleared);
        }

        public void Dispose()
        {
            _signalBus.TryUnsubscribe<FoodEnteredBeltSignal>(OnFoodEnteredBelt);
            _signalBus.TryUnsubscribe<FoodDockedSignal>(OnFoodDocked);
            _signalBus.TryUnsubscribe<FoodConsumedSignal>(OnFoodConsumed);
            _signalBus.TryUnsubscribe<BeltClearedSignal>(OnBeltCleared);

            _deferred.Clear();
            ReleaseAllRidesImmediately();
        }

        public void Tick()
        {
            FlushDueCommands();
            AdvanceRides();
        }

        private void FlushDueCommands()
        {
            float now = Time.time;
            while (_deferred.Count > 0 && _deferred.Peek().DueTime <= now)
                _deferred.Dequeue().Command?.Invoke();
        }

        private void Defer(Action command) =>
            _deferred.Enqueue(new DeferredCommand(Time.time + Mathf.Max(0f, _feel.VisualLagSeconds), command));

        private void AdvanceRides()
        {
            if (_rides.Count == 0) return;

            float delta = _tuning.BeltSpeed * Time.deltaTime;
            float lastProgress = Mathf.Max(0, _conveyorManager.PathLength - 1);

            foreach (var ride in _rides.Values)
            {
                if (ride.Tray == null) continue;

                // Progress keeps advancing during the entry flight too (matching ConveyorVisualService's
                // BeltVisual exactly) — otherwise the tray falls a whole flight's worth of frames behind
                // its food the instant the flight ends. Only the position write is skipped while entering;
                // the flight owns the transform until it arrives.
                ride.Progress = Mathf.Min(ride.Progress + delta, lastProgress);
                if (ride.IsEntering) continue;

                var position = _conveyorManager.SampleWorldPosition(ride.Progress);
                position.y = _tuning.TrayBeltHeight;
                ride.Tray.Follow(position);
            }
        }

        private void OnFoodEnteredBelt(FoodEnteredBeltSignal signal)
        {
            int id = signal.ItemId;
            float startProgress = signal.StartProgress;

            Defer(() =>
            {
                if (!_trayAnchor.TryAcquire(out var tray)) return; // no tray free — item rides without one

                var ride = new TrayRide { Tray = tray, Progress = startProgress, IsEntering = true };
                _rides[id] = ride;

                tray.EnterBelt(
                    () =>
                    {
                        var position = _conveyorManager.SampleWorldPosition(ride.Progress);
                        position.y = _tuning.TrayBeltHeight;
                        return position;
                    },
                    tray.ResolveFlatLocalRotation(),
                    _feel.TrayRotateDuration,
                    _feel.DispenserToBelt,
                    _flightService,
                    () => ride.IsEntering = false);
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
            Defer(() =>
            {
                if (!_rides.TryGetValue(id, out var ride)) return;
                _rides.Remove(id);
                if (ride.Tray == null) return;

                var tray = ride.Tray;
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
            foreach (var ride in _rides.Values)
            {
                if (ride.Tray == null) continue;
                _flightService.Cancel(ride.Tray.transform);
            }

            _rides.Clear();
            _trayAnchor.ResetAll();
        }
    }
}
