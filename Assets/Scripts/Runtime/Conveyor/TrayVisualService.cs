using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Visual-only companion to ConveyorVisualService: pairs one of the dispenser's 5 tray children with
    // every belt item (same ItemId). Never reparents the tray to the food — instead, once the tray's own
    // dispenser -> belt flight lands, Tick() drives its position every frame from the exact same Progress
    // value ConveyorManager tracks for that item (SampleWorldPosition(GetProgressById(id))), which is the
    // same source of truth the food's own visual is kept in sync with. One shared number driving both means
    // there is nothing to chase and nothing to drift — and because the tray is never a child of the food,
    // there is nothing of the food's own rotation (a flight's rotation reset, belt spin, a consume punch)
    // for it to ever inherit, on any frame, including the one where the food gets eaten a cell or two in.
    // Leaves the dispenser on the same FoodEnteredBeltSignal that starts the food's own entry flight, so the
    // two set off together. If the pool is empty when a food enters, it rides trayless and joins a wait
    // list — the moment any tray frees up it's handed straight to whoever's been waiting longest (see
    // HandleItemLeavingBelt) instead of always finishing a trip home first. Only goes back to the dispenser,
    // with a shrink punch (food fully eaten) or without one (food completed its lap and docked), when
    // nobody's waiting.
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

        // Every tray currently out (still flying in from the dispenser, or already riding).
        private readonly Dictionary<int, TrayController> _rides = new();

        // Subset of _rides.Keys whose EnterBelt flight has landed — Tick() positions exactly these every
        // frame. Split from _rides so a tray recalled mid-flight (food eaten on piece 1 or 2, see
        // ReturnRide) is never mistaken for one Tick() should be moving.
        private readonly HashSet<int> _ridingIds = new();

        // Items riding trayless because the pool was empty when they entered, oldest first. The instant any
        // tray frees up (a food fully eaten or docked), it's handed straight to whichever of these has been
        // waiting longest — see HandleTrayFreed — instead of always completing a trip home first.
        private readonly List<int> _traylessIds = new();

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
            AdvanceRidingTrays();
        }

        // The tray's entire steady-state ride, in one line per tray: go where the belt says this item is.
        // No tween, no flight — same direct-set approach ConveyorVisualService itself uses for the food.
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

        private void FlushDueCommands()
        {
            float now = Time.time;
            while (_deferred.Count > 0 && _deferred.Peek().DueTime <= now)
                _deferred.Dequeue().Command?.Invoke();
        }

        private void Defer(Action command) =>
            _deferred.Enqueue(new DeferredCommand(Time.time + Mathf.Max(0f, _feel.VisualLagSeconds), command));

        // Fires the instant the food commits to the belt — same signal ConveyorVisualService reacts to for
        // the food's own flight, no Defer added on top — so the tray leaves the dispenser the same frame
        // the food leaves the queue. Targets the item's own Progress (via ConveyorManager), not the food's
        // transform — the same number the food's visual gets synced to the instant its own flight lands —
        // so the two are always driven by one shared value instead of one chasing the other. Once this
        // flight lands, AdvanceRidingTrays takes over completely for the rest of the ride.
        private void OnFoodEnteredBelt(FoodEnteredBeltSignal signal)
        {
            int id = signal.ItemId;
            if (signal.Visual == null) return;

            if (!_trayAnchor.TryAcquire(out var tray))
            {
                _traylessIds.Add(id); // no tray free now — first in line for the next one that frees up
                return;
            }

            StartTrayEntry(id, tray);
        }

        // Shared by a fresh entry and a tray handed off mid-belt to whichever item has been waiting longest
        // (see HandleTrayFreed) — either way it's the same trip: fly from wherever the tray currently is to
        // this item's live position, turning flat alongside it, then ride it from Tick() once landed.
        private void StartTrayEntry(int id, TrayController tray)
        {
            _rides[id] = tray;

            Vector3 TargetPosition()
            {
                var position = _conveyorManager.SampleWorldPosition(_conveyorManager.GetProgressById(id));
                position.y = _tuning.TrayBeltHeight;
                return position;
            }

            // Times the upright -> flat turn to finish alongside the flight instead of on its own fixed
            // clock, so the tray is done rotating right as it arrives, not still mid-turn or sitting flat
            // and waiting for the flight to catch up.
            float flightDuration = _feel.DispenserToBelt.ResolveDuration(Vector3.Distance(tray.transform.position, TargetPosition()));

            tray.EnterBelt(
                TargetPosition,
                tray.ResolveFlatLocalRotation(),
                flightDuration,
                _feel.DispenserToBelt,
                _flightService,
                () => _ridingIds.Add(id));
        }

        private void OnFoodDocked(FoodDockedSignal signal) => HandleItemLeavingBelt(signal.ItemId, withPunch: false);

        private void OnFoodConsumed(FoodConsumedSignal signal)
        {
            if (!signal.ItemFullyConsumed) return; // partial take: item (and its tray) stay on the belt
            HandleItemLeavingBelt(signal.ItemId, withPunch: true);
        }

        // Every item that leaves the belt (docked or fully eaten) comes through here, tray or not — a
        // trayless one just needs its place in line cleared so a later handoff never targets a food that's
        // already gone.
        private void HandleItemLeavingBelt(int id, bool withPunch)
        {
            _traylessIds.Remove(id);

            if (!_rides.TryGetValue(id, out var tray)) return; // was trayless — nothing to return
            _rides.Remove(id);
            _ridingIds.Remove(id);
            if (tray == null) return;

            // Kills the entry flight outright if the food got eaten before this tray even landed (piece 1
            // or 2) — without this the old flight keeps ticking toward a Progress that no longer belongs
            // to any belt item while the return/handoff below also runs, and the two fight over the same
            // transform. Harmless no-op if the tray had already landed and Tick() was driving it.
            _flightService.Cancel(tray.transform);

            // Someone's been waiting the whole time this tray was out — hand it straight to whoever's been
            // waiting longest instead of sending it all the way home and dispatching a fresh one afterward.
            // That round trip is exactly the gap the player saw a new food ride the whole belt with no tray
            // under it even though one was about to be free: this closes it to roughly nothing.
            if (_traylessIds.Count > 0)
            {
                int waitingId = _traylessIds[0];
                _traylessIds.RemoveAt(0);
                StartTrayEntry(waitingId, tray);
                return;
            }

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
            _ridingIds.Clear();
            _traylessIds.Clear();
            _trayAnchor.ResetAll();
        }
    }
}
