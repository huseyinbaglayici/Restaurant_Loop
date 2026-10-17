using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Audio;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Visual half of the conveyor: reacts the same frame to the logical layer's signals (entry, travel,
    // dock, loop, consume) and only moves, spawns and despawns visuals. Never changes gameplay state.
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
        private readonly ConveyorFeelSettings _feel;
        private readonly FoodFlightService _flightService;
        private readonly FoodItemController.Pool _foodPool;
        private readonly ExitStackAnchor _exitStackAnchor;
        private readonly CustomerServeVisuals _serveVisuals;
        private readonly AudioService _audioService;
        private readonly AudioLibrary _audioLibrary;

        // Keyed by the logical item id.
        private readonly Dictionary<int, BeltVisual> _beltVisuals = new();

        // ConveyorAudio keeps the belt loop playing while this is true.
        public bool HasBeltVisuals => _beltVisuals.Count > 0;

        public ConveyorVisualService(
            SignalBus signalBus,
            ConveyorManager conveyorManager,
            ConveyorFeelSettings feel,
            FoodFlightService flightService,
            FoodItemController.Pool foodPool,
            ExitStackAnchor exitStackAnchor,
            CustomerServeVisuals serveVisuals,
            AudioService audioService,
            AudioLibrary audioLibrary)
        {
            _signalBus = signalBus;
            _conveyorManager = conveyorManager;
            _feel = feel;
            _flightService = flightService;
            _foodPool = foodPool;
            _exitStackAnchor = exitStackAnchor;
            _serveVisuals = serveVisuals;
            _audioService = audioService;
            _audioLibrary = audioLibrary;
        }

        public void Initialize()
        {
            _signalBus.Subscribe<FoodQueuedForEntrySignal>(OnFoodQueuedForEntry);
            _signalBus.Subscribe<FoodEnteredBeltSignal>(OnFoodEnteredBelt);
            _signalBus.Subscribe<FoodDockedSignal>(OnFoodDocked);
            _signalBus.Subscribe<FoodLoopedSignal>(OnFoodLooped);
            _signalBus.Subscribe<FoodConsumedSignal>(OnFoodConsumed);
            _signalBus.Subscribe<BeltClearedSignal>(OnBeltCleared);
        }

        public void Dispose()
        {
            _signalBus.TryUnsubscribe<FoodQueuedForEntrySignal>(OnFoodQueuedForEntry);
            _signalBus.TryUnsubscribe<FoodEnteredBeltSignal>(OnFoodEnteredBelt);
            _signalBus.TryUnsubscribe<FoodDockedSignal>(OnFoodDocked);
            _signalBus.TryUnsubscribe<FoodLoopedSignal>(OnFoodLooped);
            _signalBus.TryUnsubscribe<FoodConsumedSignal>(OnFoodConsumed);
            _signalBus.TryUnsubscribe<BeltClearedSignal>(OnBeltCleared);

            _flightService.CancelAll();
            _beltVisuals.Clear();
            _serveVisuals.ForgetFlights();
            _serveVisuals.DespawnOwnedVisuals();
            ConveyorPieceController.GlobalSpeedMultiplier = 1f;
        }

        public void Tick()
        {
            ConveyorPieceController.GlobalSpeedMultiplier = _conveyorManager.SpeedMultiplier;
            AdvanceBeltVisuals();
        }

        private void AdvanceBeltVisuals()
        {
            if (_beltVisuals.Count == 0) return;

            float deltaTime = Time.deltaTime;

            foreach (var kvp in _beltVisuals)
            {
                int id = kvp.Key;
                var beltVisual = kvp.Value;

                // The entry flight owns the transform until it lands.
                if (beltVisual.IsEnteringBelt) continue;
                if (beltVisual.Visual == null) continue;

                float progress = _conveyorManager.GetProgressById(id);
                beltVisual.Progress = progress;
                beltVisual.Visual.transform.position = _conveyorManager.SampleWorldPosition(progress);

                if (!Mathf.Approximately(_feel.BeltSpinYDegreesPerSecond, 0f))
                    beltVisual.Visual.transform.Rotate(0f, _feel.BeltSpinYDegreesPerSecond * deltaTime, 0f, Space.World);
            }
        }

        // The entry is busy: fly the picked item up to hover above it. Not a belt visual yet, so belt travel
        // and the tray follow ignore it until it actually enters.
        private void OnFoodQueuedForEntry(FoodQueuedForEntrySignal signal)
        {
            var visual = signal.Visual;
            if (visual == null) return;

            visual.transform.position = signal.SourceWorldPosition;
            visual.SetVisualState(FoodVisualState.Opened);
            visual.SetLocation(FoodLocation.Conveyor);

            _flightService.Launch(visual.transform, signal.HoverWorldPosition, _feel.QueueToBelt);
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

            // Progress is frozen at its entry value on both layers until this lands, so the flight targets
            // the entry cell itself and there's nothing to catch up on afterwards.
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
            var fallbackSlotPosition = signal.SlotWorldPosition;

            _beltVisuals.Remove(id);
            if (visual == null) return;

            visual.SetLocation(FoodLocation.FoodStack);

            Vector3 TargetPosition()
            {
                var pos = _exitStackAnchor.GetFoodPositionForOccupant(visual);
                return pos != Vector3.zero ? pos : fallbackSlotPosition;
            }

            _flightService.Launch(visual.transform, TargetPosition, _feel.BeltToStack, () =>
            {
                _audioService.PlaySfx(_audioLibrary.FoodDropToRackSfx);
                if (visual != null) SquashTween.Play(visual.transform, 0.22f);
                _signalBus.Fire(new FoodDockLandedSignal { ItemId = id, Visual = visual });
            });
        }

        // The FoodEnteredBeltSignal fired right after this (same visual, new id) takes over the transform;
        // the old entry must go first or both would drive it.
        private void OnFoodLooped(FoodLoopedSignal signal) => _beltVisuals.Remove(signal.ItemId);

        private void OnFoodConsumed(FoodConsumedSignal signal)
        {
            if (signal.Visual == null) return;

            if (signal.ItemFullyConsumed && signal.PartnerVisual != null)
                DespawnLinkedPartner(signal.PartnerVisual, signal.PartnerItemId);

            // A fully consumed item is no longer on the belt; its visual is the serve flyer now.
            if (signal.ItemFullyConsumed) _beltVisuals.Remove(signal.ItemId);

            _serveVisuals.Serve(signal);
        }

        private void DespawnLinkedPartner(FoodItemController partnerVisual, int? partnerItemId)
        {
            int removeKey = -1;
            foreach (var kvp in _beltVisuals)
            {
                if (kvp.Value != null && kvp.Value.Visual == partnerVisual)
                {
                    removeKey = kvp.Key;
                    break;
                }
            }
            if (removeKey != -1) _beltVisuals.Remove(removeKey);
            if (partnerItemId.HasValue) _beltVisuals.Remove(partnerItemId.Value);

            _flightService.Cancel(partnerVisual.transform);
            _foodPool.Despawn(partnerVisual);
        }

        private void OnBeltCleared()
        {
            _serveVisuals.LandAllFlights();

            // Scoped to belt/owned visuals only: a global CancelAll() would also freeze an already-docked
            // item's flight to the exit stack and strand its visual on the belt.
            foreach (var beltVisual in _beltVisuals.Values)
            {
                if (beltVisual.Visual != null) _flightService.Cancel(beltVisual.Visual.transform);
            }
            _serveVisuals.CancelOwnedFlights();

            _beltVisuals.Clear();
            _serveVisuals.DespawnOwnedVisuals();
        }
    }
}
