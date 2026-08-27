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
    // "what is where on the grid" and decides consumes/docks; this class reacts to those decisions the
    // instant they fire — entry, travel, dock and consume all run on the same immediate signal-driven
    // path, so the visual is never more than a frame away from what the logical layer just decided.
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

            _flightService.CancelAll();
            _beltVisuals.Clear();
            _audioService.StopConveyorLoop();
            DespawnOwnedVisuals();
        }

        public void Tick()
        {
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

        private void AdvanceBeltVisuals()
        {
            if (_beltVisuals.Count == 0) return;

            float deltaTime = Time.deltaTime;
            float delta = _tuning.BeltSpeed * deltaTime;
            float lastProgress = Mathf.Max(0, _conveyorManager.PathLength - 1);

            foreach (var beltVisual in _beltVisuals.Values)
            {
                // While flying in from the queue the flight owns the transform and targets the fixed
                // entry point (Progress held at its start value) — advancing Progress here too would
                // drag that point forward under the flight, landing the item a belt-speed's worth of
                // cells past the true entry instead of on it.
                if (beltVisual.IsEnteringBelt) continue;
                if (beltVisual.Visual == null) continue;

                beltVisual.Progress = Mathf.Min(beltVisual.Progress + delta, lastProgress);
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

            if (visual == null) return;

            var beltVisual = new BeltVisual { Visual = visual, Progress = startProgress };
            _beltVisuals[id] = beltVisual;

            if (!playEntryFlight)
            {
                visual.transform.position = _conveyorManager.SampleWorldPosition(beltVisual.Progress);
                visual.SetVisualState(FoodVisualState.Opened);
                visual.SetLocation(FoodLocation.Conveyor);
                _signalBus.Fire(new FoodBeltEntryLandedSignal { ItemId = id, Visual = visual, Progress = beltVisual.Progress });
                return;
            }

            beltVisual.IsEnteringBelt = true;
            visual.transform.position = sourcePosition;
            visual.SetVisualState(FoodVisualState.Opened);
            visual.SetLocation(FoodLocation.Conveyor);

            // Targets the entry cell itself, not a moving belt slot — AdvanceBeltVisuals holds
            // beltVisual.Progress at its start value for as long as IsEnteringBelt is set, so the item
            // actually lands on the entry cell instead of wherever the belt reached by the time the
            // flight finished.
            _flightService.Launch(
                visual.transform,
                () => _conveyorManager.SampleWorldPosition(beltVisual.Progress),
                _feel.QueueToBelt,
                () =>
                {
                    beltVisual.IsEnteringBelt = false;
                    _signalBus.Fire(new FoodBeltEntryLandedSignal { ItemId = id, Visual = visual, Progress = beltVisual.Progress });
                });
        }

        private void OnFoodDocked(FoodDockedSignal signal)
        {
            int id = signal.ItemId;
            var visual = signal.Visual;
            var slotPosition = signal.SlotWorldPosition;

            _beltVisuals.Remove(id);
            if (visual == null) return;

            visual.SetLocation(FoodLocation.FoodStack);
            _flightService.Launch(visual.transform, slotPosition, _feel.BeltToStack, () =>
            {
                _audioService.PlaySfx(_audioLibrary.FoodDropToRackSfx);
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

            // Fires and reacts the same frame the logical layer decides — the item's belt visual is
            // wherever AdvanceBeltVisuals put it this frame, which is exactly where the flight should
            // launch from, so there's nothing to wait on here.
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
