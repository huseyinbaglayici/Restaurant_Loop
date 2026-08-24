using System;
using System.Collections.Generic;
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
            CustomerConsumeService consumeService)
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
            DespawnOwnedVisuals();
        }

        public void Tick()
        {
            FlushDueCommands();
            AdvanceBeltVisuals();
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

            Defer(() =>
            {
                if (visual == null) return;

                var beltVisual = new BeltVisual { Visual = visual, Progress = startProgress };
                _beltVisuals[id] = beltVisual;

                if (!playEntryFlight)
                {
                    visual.transform.position = _conveyorManager.SampleWorldPosition(beltVisual.Progress);
                    visual.SetVisualState(FoodVisualState.Opened);
                    return;
                }

                beltVisual.IsEnteringBelt = true;
                visual.transform.position = sourcePosition;

                _flightService.Launch(
                    visual.transform,
                    () => _conveyorManager.SampleWorldPosition(beltVisual.Progress),
                    _feel.QueueToBelt,
                    () =>
                    {
                        beltVisual.IsEnteringBelt = false;
                        visual.SetVisualState(FoodVisualState.Opened);
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

                _flightService.Launch(visual.transform, slotPosition, _feel.BeltToStack);
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

            Defer(() =>
            {
                var flyer = ResolveConsumeFlyer(id, sourceVisual, takenStack, fullyConsumed);
                if (flyer == null) return;

                _ownedVisuals.Add(flyer);

                _flightService.Launch(
                    flyer.transform,
                    () => ResolveCustomerHandoffPosition(coord, flyer.transform.position),
                    _feel.ConsumeToCustomer,
                    () => OnConsumeFlightArrived(flyer, coord, takenStack, serveResult, levelWon));
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

            return _foodPool.Spawn(sourceVisual.transform.position, takenStack, FoodVisualState.Opened);
        }

        private void OnConsumeFlightArrived(
            FoodItemController flyer,
            Vector2Int coord,
            FoodStackData takenStack,
            RuntimeCustomerState.ConsumeResult serveResult,
            bool levelWon)
        {
            var worldPosition = flyer != null ? flyer.transform.position : Vector3.zero;

            if (flyer != null)
            {
                _ownedVisuals.Remove(flyer);
                _foodPool.Despawn(flyer);
            }

            _customerSpawner.PlayConsumePunch(
                coord, _feel.ConsumePunchStrength, _feel.ConsumePunchDuration,
                _feel.ConsumePunchVibrato, _feel.ConsumePunchElasticity);

            ApplyServeResult(coord, serveResult);

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
