using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Audio;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Visual side of a consume: flies the taken food from the belt to the customer and, when it lands,
    // plays the customer's reaction and reports the delivery. The serve itself was already decided.
    public class CustomerServeVisuals
    {
        private class ServeFlight
        {
            public Transform Transform;
            public Func<Vector3> Target;
            public Action Arrive;
        }

        private readonly SignalBus _signalBus;
        private readonly ConveyorFeelSettings _feel;
        private readonly FoodFlightService _flightService;
        private readonly FoodItemController.Pool _foodPool;
        private readonly CustomerSpawner _customerSpawner;
        private readonly RuntimeCustomerState _customerState;
        private readonly CustomerConsumeService _consumeService;
        private readonly AudioService _audioService;
        private readonly AudioLibrary _audioLibrary;

        // Flyers handed over by the belt (or spawned for a partial take); despawned here.
        private readonly HashSet<FoodItemController> _ownedVisuals = new();

        // Flights still in the air. The serve already counted logically, so a belt clear lands them instantly.
        private readonly List<ServeFlight> _flights = new();

        public CustomerServeVisuals(
            SignalBus signalBus,
            ConveyorFeelSettings feel,
            FoodFlightService flightService,
            FoodItemController.Pool foodPool,
            CustomerSpawner customerSpawner,
            RuntimeCustomerState customerState,
            CustomerConsumeService consumeService,
            AudioService audioService,
            AudioLibrary audioLibrary)
        {
            _signalBus = signalBus;
            _feel = feel;
            _flightService = flightService;
            _foodPool = foodPool;
            _customerSpawner = customerSpawner;
            _customerState = customerState;
            _consumeService = consumeService;
            _audioService = audioService;
            _audioLibrary = audioLibrary;
        }

        public void Serve(FoodConsumedSignal signal)
        {
            var sourceVisual = signal.Visual;
            bool fullyConsumed = signal.ItemFullyConsumed;
            var coord = signal.CustomerCoord;
            var takenStack = signal.TakenStack;
            var serveResult = signal.ServeResult;
            bool levelWon = signal.LevelWon;

            if (sourceVisual.UsesFixedChildSlots)
            {
                ServeFixedSlotChild(sourceVisual, fullyConsumed, coord, takenStack, serveResult, levelWon);
                return;
            }

            var flyer = ResolveFlyer(sourceVisual, takenStack, fullyConsumed);
            if (flyer == null) return;

            flyer.SetAmountBadgeVisible(false);
            _ownedVisuals.Add(flyer);

            LaunchFlight(
                flyer.transform,
                () => ResolveCustomerHandoffPosition(coord, flyer.transform.position),
                () => OnArrived(flyer, flyer.transform.position, coord, takenStack, serveResult, levelWon, fullyConsumed));
        }

        // Lands every flight in the air right away; its arrival is what updates the customer and reports a win.
        public void LandAllFlights()
        {
            var landingNow = _flights.ToArray();
            foreach (var flight in landingNow)
            {
                if (flight.Transform != null)
                {
                    _flightService.Cancel(flight.Transform);
                    flight.Transform.position = flight.Target();
                }
                flight.Arrive();
            }
        }

        public void CancelOwnedFlights()
        {
            foreach (var visual in _ownedVisuals)
            {
                if (visual != null) _flightService.Cancel(visual.transform);
            }
        }

        public void DespawnOwnedVisuals()
        {
            foreach (var visual in _ownedVisuals)
            {
                if (visual == null) continue;
                _foodPool.Despawn(visual);
            }

            _ownedVisuals.Clear();
        }

        // Drops in-flight bookkeeping without landing anything (scene teardown).
        public void ForgetFlights() => _flights.Clear();

        private void LaunchFlight(Transform target, Func<Vector3> targetPosition, Action onArrive)
        {
            var flight = new ServeFlight { Transform = target, Target = targetPosition };
            flight.Arrive = () =>
            {
                if (!_flights.Remove(flight)) return; // already landed by LandAllFlights
                onArrive();
            };

            _flights.Add(flight);
            _flightService.Launch(target, targetPosition, _feel.ConsumeToCustomer, flight.Arrive);
        }

        // Beverage rack: fixed base with pre-placed single drinks. Only the taken drink flies; the rack stays.
        private void ServeFixedSlotChild(
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
                _flightService.Cancel(sourceVisual.transform);
                _foodPool.Despawn(sourceVisual);
            }

            if (child == null)
            {
                OnArrived(null, sourceVisual != null ? sourceVisual.transform.position : Vector3.zero,
                    coord, takenStack, serveResult, levelWon, fullyConsumed);
                return;
            }

            LaunchFlight(
                child,
                () => ResolveCustomerHandoffPosition(coord, child.position),
                () =>
                {
                    var worldPosition = child.position;
                    UnityEngine.Object.Destroy(child.gameObject);
                    OnArrived(null, worldPosition, coord, takenStack, serveResult, levelWon, fullyConsumed);
                });
        }

        // Full take: the belt item's own visual becomes the flyer. Partial take: a detached copy carries
        // just the taken amount while the belt item stays.
        private FoodItemController ResolveFlyer(FoodItemController sourceVisual, FoodStackData takenStack, bool fullyConsumed)
        {
            if (sourceVisual == null) return null;

            if (fullyConsumed)
            {
                _flightService.Cancel(sourceVisual.transform);
                return sourceVisual;
            }

            var flyer = _foodPool.Spawn(sourceVisual.TopUnitWorldPosition, takenStack, FoodVisualState.Opened);
            flyer.SetLocation(sourceVisual.Location);
            return flyer;
        }

        private void OnArrived(
            FoodItemController flyer,
            Vector3 worldPosition,
            Vector2Int coord,
            FoodStackData takenStack,
            RuntimeCustomerState.ConsumeResult serveResult,
            bool levelWon,
            bool fullyConsumed)
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
                    // The order UI hides instantly; the reaction is just the exit performance before despawn.
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
    }
}
