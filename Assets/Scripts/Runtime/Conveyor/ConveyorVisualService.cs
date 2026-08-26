using System;
using System.Collections.Generic;
using RestaurantLoop.Audio;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // The visual half of the conveyor. The logical half (ConveyorManager / CustomerConsumeService) owns
    // "what is where on the grid" and decides consumes/docks the instant they happen; this class owns every
    // food transform and replays those decisions ConveyorFeelSettings.VisualLagSeconds later.
    //
    // Nothing here may change gameplay state — it only moves, spawns and despawns visuals, and pokes the
    // customer visuals (order UI refresh, serve reaction) at the moment the food actually lands.
    public class ConveyorVisualService : IInitializable, ITickable, IDisposable
    {
        private class BeltVisual
        {
            public FoodItemController Visual;
            public float Progress;
            public bool IsEnteringBelt;

            // Set the instant the logical layer fully consumes this item — freezes it in place so it
            // doesn't keep sliding down the belt (and past the customer) during the VisualLagSeconds
            // wait before the consume flight actually launches.
            public bool IsFrozen;
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
        private readonly FoodItemController.Pool _foodPool;
        private readonly CustomerSpawner _customerSpawner;
        private readonly RuntimeCustomerState _customerState;
        private readonly CustomerConsumeService _consumeService;
        private readonly IAudioService _audioService;
        private readonly AudioLibrary _audioLibrary;

        // Belt items currently drawn by this service, keyed by the logical item id.
        private readonly Dictionary<int, BeltVisual> _beltVisuals = new();

        // Visuals the logical layer handed over (in flight to a customer) — this service despawns them.
        private readonly HashSet<FoodItemController> _ownedVisuals = new();

        private readonly Queue<DeferredCommand> _deferred = new();

        public ConveyorVisualService(
            SignalBus signalBus,
            ConveyorManager conveyorManager,
            ConveyorTuning tuning,
            ConveyorFeelSettings feel,
            FoodFlightService flightService,
            FoodItemController.Pool foodPool,
            CustomerSpawner customerSpawner,
            RuntimeCustomerState customerState,
            CustomerConsumeService consumeService,
            IAudioService audioService,
            AudioLibrary audioLibrary)
        {
            _signalBus = signalBus;
            _conveyorManager = conveyorManager;
            _tuning = tuning;
            _feel = feel;
            _flightService = flightService;
            _foodPool = foodPool;
            _customerSpawner = customerSpawner;
            _customerState = customerState;
            _consumeService = consumeService;
            _audioService = audioService;
            _audioLibrary = audioLibrary;
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
            _flightService.CancelAll();
            _beltVisuals.Clear();
            _audioService.StopConveyorLoop();
            DespawnOwnedVisuals();
        }

        public void Tick()
        {
            FlushDueCommands();
            AdvanceBeltVisuals();
            UpdateConveyorLoopAudio();
        }

        private void UpdateConveyorLoopAudio()
        {
            if (_beltVisuals.Count > 0)
            {
                _audioService.PlayConveyorLoop(_audioLibrary.ConveyorLoopSfx);
            }
            else
            {
                _audioService.StopConveyorLoop();
            }
        }

        private void FlushDueCommands()
        {
            float now = Time.time;
            while (_deferred.Count > 0 && _deferred.Peek().DueTime <= now)
                _deferred.Dequeue().Command?.Invoke();
        }

        // Every logical event lands here first — the lag is what makes the visual layer trail the logical one.
        private void Defer(Action command) =>
            _deferred.Enqueue(new DeferredCommand(Time.time + Mathf.Max(0f, _feel.VisualLagSeconds), command));

        private void AdvanceBeltVisuals()
        {
            if (_beltVisuals.Count == 0) return;

            float deltaTime = Time.deltaTime;
            float delta = _tuning.BeltSpeed * deltaTime;
            float lastProgress = Mathf.Max(0, _conveyorManager.PathLength - 1);

            foreach (var beltVisual in _beltVisuals.Values)
            {
                beltVisual.Progress = Mathf.Min(beltVisual.Progress + delta, lastProgress);

                if (beltVisual.IsFrozen) continue;

                // While flying in from the queue the flight owns the transform; it chases the belt slot,
                // which keeps moving, so the item never snaps at the handover.
                if (beltVisual.IsEnteringBelt) continue;
                if (beltVisual.Visual == null) continue;

                beltVisual.Visual.transform.position = _conveyorManager.SampleWorldPosition(beltVisual.Progress);

                if (!Mathf.Approximately(_feel.BeltSpinYDegreesPerSecond, 0f))
                    beltVisual.Visual.transform.Rotate(0f, _feel.BeltSpinYDegreesPerSecond * deltaTime, 0f, Space.World);
            }
        }

        private void OnFoodEnteredBelt(FoodEnteredBeltSignal signal)
        {
            int id = signal.ItemId;
            var visual = signal.Visual;
            var sourcePosition = signal.SourceWorldPosition;
            float startProgress = signal.StartProgress;
            bool playEntryFlight = signal.PlayEntryFlight;
            bool awaitsLanding = signal.AwaitsLanding;

            Defer(() =>
            {
                if (visual == null) return;

                var beltVisual = new BeltVisual { Visual = visual, Progress = startProgress };
                _beltVisuals[id] = beltVisual;

                if (!playEntryFlight)
                {
                    visual.transform.position = _conveyorManager.SampleWorldPosition(beltVisual.Progress);
                    visual.SetVisualState(FoodVisualState.Opened);
                    visual.SetLocation(FoodLocation.Conveyor);

                    // No actual travel to animate, but the logical layer still held this item for us
                    // (AwaitingEntryLanding) — confirm "visual is here" the same way the flight path does.
                    if (awaitsLanding) _signalBus.Fire(new FoodEntryLandedSignal { ItemId = id });
                    return;
                }

                beltVisual.IsEnteringBelt = true;
                visual.transform.position = sourcePosition;
                visual.SetVisualState(FoodVisualState.Opened);
                visual.SetLocation(FoodLocation.Conveyor);

                _flightService.Launch(
                    visual.transform,
                    () => _conveyorManager.SampleWorldPosition(beltVisual.Progress),
                    _feel.QueueToBelt,
                    () =>
                    {
                        beltVisual.IsEnteringBelt = false;
                        _signalBus.Fire(new FoodEntryLandedSignal { ItemId = id });
                    });
            });
        }

        private void OnFoodDocked(FoodDockedSignal signal)
        {
            int id = signal.ItemId;
            var visual = signal.Visual;
            var slotPosition = signal.SlotWorldPosition;

            Defer(() =>
            {
                _beltVisuals.Remove(id);
                if (visual == null) return;

                visual.SetLocation(FoodLocation.FoodStack);
                _flightService.Launch(visual.transform, slotPosition, _feel.BeltToStack, () =>
                {
                    _audioService.PlaySfx(_audioLibrary.FoodDropToRackSfx);
                });
            });
        }

        private void OnFoodConsumed(FoodConsumedSignal signal)
        {
            int id = signal.ItemId;
            var sourceVisual = signal.Visual;
            var takenStack = signal.TakenStack;
            bool fullyConsumed = signal.ItemFullyConsumed;
            var coord = signal.CustomerCoord;
            var serveResult = signal.ServeResult;
            bool levelWon = signal.LevelWon;

            if (fullyConsumed && _beltVisuals.TryGetValue(id, out var frozenVisual))
                frozenVisual.IsFrozen = true;

            // No VisualLagSeconds wait here: the item is already frozen at the exact spot it was
            // consumed, so launching the flight now is what keeps it seamless — deferring it just adds
            // a visible stall before the item takes off.
            if (sourceVisual == null) return;

            if (sourceVisual.UsesFixedChildSlots)
            {
                LaunchFixedSlotConsumeFlight(id, sourceVisual, fullyConsumed, coord, takenStack, serveResult, levelWon);
                return;
            }

            var flyer = ResolveConsumeFlyer(id, sourceVisual, takenStack, fullyConsumed);
            if (flyer == null) return;

            _ownedVisuals.Add(flyer);

            _flightService.Launch(
                flyer.transform,
                () => ResolveCustomerHandoffPosition(coord, flyer.transform.position),
                _feel.ConsumeToCustomer,
                () => OnConsumeFlightArrived(flyer, flyer.transform.position, coord, takenStack, serveResult, levelWon, fullyConsumed));
        }

        // Beverage rack (icecek_stack): fixed base + several pre-placed single-drink children. Rather than
        // flying a duplicate of the whole rack per consume, detach just the one drink that was actually
        // taken and fly that; the rack (and whatever drinks remain on it) stays exactly where ApplyStack
        // already left it.
        private void LaunchFixedSlotConsumeFlight(
            int id,
            FoodItemController sourceVisual,
            bool fullyConsumed,
            Vector2Int coord,
            FoodStackData takenStack,
            RuntimeCustomerState.ConsumeResult serveResult,
            bool levelWon)
        {
            var child = sourceVisual.TakeFixedSlotChild();

            if (fullyConsumed)
            {
                _beltVisuals.Remove(id);
                _flightService.Cancel(sourceVisual.transform);
                _foodPool.Despawn(sourceVisual);
            }

            if (child == null)
            {
                OnConsumeFlightArrived(null, sourceVisual != null ? sourceVisual.transform.position : Vector3.zero,
                    coord, takenStack, serveResult, levelWon, fullyConsumed);
                return;
            }

            _flightService.Launch(
                child,
                () => ResolveCustomerHandoffPosition(coord, child.position),
                _feel.ConsumeToCustomer,
                () =>
                {
                    var worldPosition = child.position;
                    UnityEngine.Object.Destroy(child.gameObject);
                    OnConsumeFlightArrived(null, worldPosition, coord, takenStack, serveResult, levelWon, fullyConsumed);
                });
        }

        // Full take: the logical layer already dropped the item, so its own visual becomes the flyer.
        // Partial take: the belt item stays, so a detached visual carries just the taken amount.
        private FoodItemController ResolveConsumeFlyer(
            int id, FoodItemController sourceVisual, FoodStackData takenStack, bool fullyConsumed)
        {
            if (sourceVisual == null) return null;

            if (fullyConsumed)
            {
                _beltVisuals.Remove(id);
                _flightService.Cancel(sourceVisual.transform);
                return sourceVisual;
            }

            var flyer = _foodPool.Spawn(sourceVisual.TopUnitWorldPosition, takenStack, FoodVisualState.Opened);
            flyer.SetLocation(sourceVisual.Location);
            return flyer;
        }

        private void OnConsumeFlightArrived(
            FoodItemController flyer,
            Vector3 worldPosition,
            Vector2Int coord,
            FoodStackData takenStack,
            RuntimeCustomerState.ConsumeResult serveResult,
            bool levelWon,
            bool fullyConsumed = false)
        {
            if (flyer != null)
            {
                _ownedVisuals.Remove(flyer);
                _foodPool.Despawn(flyer);
            }

            _customerSpawner.PlayConsumePunch(
                coord, _feel.ConsumePunchStrength, _feel.ConsumePunchDuration,
                _feel.ConsumePunchVibrato, _feel.ConsumePunchElasticity);

            ApplyServeResult(coord, serveResult);

            _audioService.PlaySfx(_audioLibrary.CustomerEatSfx);
            if (fullyConsumed)
            {
                _audioService.PlaySfx(_audioLibrary.FoodGroupCompletedSfx);
            }

            _signalBus.Fire(new FoodDeliveredToCustomerSignal
            {
                CustomerCoord = coord,
                DeliveredStack = takenStack,
                WorldPosition = worldPosition,
                ServeResult = serveResult
            });

            if (levelWon) _consumeService.NotifyLevelWon();
        }

        // The customer reacts when the food lands, not when the logical layer decided it would.
        private void ApplyServeResult(Vector2Int coord, RuntimeCustomerState.ConsumeResult serveResult)
        {
            switch (serveResult)
            {
                case RuntimeCustomerState.ConsumeResult.Reduced:
                    RefreshCustomerVisual(coord);
                    break;

                case RuntimeCustomerState.ConsumeResult.Promoted:
                    RefreshCustomerVisual(coord);
                    _customerSpawner.PlayServeReaction(coord, hasSecondOrderRemaining: true);
                    break;

                case RuntimeCustomerState.ConsumeResult.Served:
                    // Order UI goes seamless-instant; the reaction animation is just the character's
                    // exit performance before despawn, not something the UI should wait on.
                    _customerSpawner.HideOrderUI(coord);
                    _customerSpawner.PlayServeReaction(coord, hasSecondOrderRemaining: false);
                    break;
            }
        }

        private void RefreshCustomerVisual(Vector2Int coord)
        {
            if (!_customerState.TryGetCustomer(coord, out var customer)) return;
            _customerSpawner.RefreshCustomerVisual(coord, customer);
        }

        private Vector3 ResolveCustomerHandoffPosition(Vector2Int coord, Vector3 fallback)
        {
            if (!_customerSpawner.TryGetVisual(coord, out var customerVisual) || customerVisual == null) return fallback;

            var anchor = customerVisual.FoodHandoffAnchor;
            return anchor != null ? anchor.position : customerVisual.transform.position;
        }

        private void OnBeltCleared()
        {
            _deferred.Clear();
            _flightService.CancelAll();
            _beltVisuals.Clear();
            DespawnOwnedVisuals();
        }

        private void DespawnOwnedVisuals()
        {
            foreach (var visual in _ownedVisuals)
            {
                if (visual == null) continue;
                _foodPool.Despawn(visual);
            }

            _ownedVisuals.Clear();
        }
    }
}
