using System;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Logical consume and level end: matches belt crossings to customers, reveals hidden neighbours on a
    // serve, and fails the level when the exit stack overflows or deadlocks.
    public class CustomerConsumeService : IInitializable, ITickable, IDisposable
    {
        private readonly ConveyorManager _conveyorManager;
        private readonly CustomerSightLines _sightLines;
        private readonly RuntimeCustomerState _customerState;
        private readonly CustomerSpawner _customerSpawner;
        private readonly ExitStackAnchor _exitStackAnchor;
        private readonly SignalBus _signalBus;
        private readonly RestaurantLoop.Runtime.Audio.AudioService _audioService;
        private readonly AudioLibrary _audioLibrary;

        private bool _wasStackFull;

        // The item that docked last, so a deadlock fail can wait until its flight into the slot has landed.
        private int? _lastDockedItemId;
        private int? _pendingDeadlockItemId;

        // OnLevelEnded fires at most once until Keep Playing resumes the level.
        private bool _levelEnded;

        public event Action<bool> OnLevelEnded;

        public CustomerConsumeService(
            ConveyorManager conveyorManager,
            CustomerSightLines sightLines,
            RuntimeCustomerState customerState,
            CustomerSpawner customerSpawner,
            ExitStackAnchor exitStackAnchor,
            SignalBus signalBus,
            RestaurantLoop.Runtime.Audio.AudioService audioService,
            AudioLibrary audioLibrary)
        {
            _conveyorManager = conveyorManager;
            _sightLines = sightLines;
            _customerState = customerState;
            _customerSpawner = customerSpawner;
            _exitStackAnchor = exitStackAnchor;
            _signalBus = signalBus;
            _audioService = audioService;
            _audioLibrary = audioLibrary;
        }

        public void Initialize()
        {
            _signalBus.Subscribe<FoodDockedSignal>(OnFoodDocked);
            _signalBus.Subscribe<FoodDockLandedSignal>(OnFoodDockLanded);
            _signalBus.Subscribe<ExitStackOverflowSignal>(OnExitStackOverflow);
            _signalBus.Subscribe<BeltClearedSignal>(OnBeltCleared);
            _signalBus.Subscribe<LevelResumedSignal>(OnLevelResumed);
        }

        public void Dispose()
        {
            _signalBus.TryUnsubscribe<FoodDockedSignal>(OnFoodDocked);
            _signalBus.TryUnsubscribe<FoodDockLandedSignal>(OnFoodDockLanded);
            _signalBus.TryUnsubscribe<ExitStackOverflowSignal>(OnExitStackOverflow);
            _signalBus.TryUnsubscribe<BeltClearedSignal>(OnBeltCleared);
            _signalBus.TryUnsubscribe<LevelResumedSignal>(OnLevelResumed);
        }

        public void NotifyLevelWon() => EndLevel(true);

        public void Tick()
        {
            var crossings = _conveyorManager.CrossingsThisFrame;
            for (int c = 0; c < crossings.Count; c++)
            {
                var (pathIndex, visual) = crossings[c];
                if (visual == null || visual.Stack.Amount <= 0) continue;
                if (!_sightLines.TryFindCustomer(pathIndex, visual.Stack.Type, out var coord)) continue;

                int amountTaken = _conveyorManager.ConsumeFromItem(
                    visual, 1, out int itemId, out bool itemFullyConsumed,
                    out int? partnerItemId, out FoodItemController partnerVisual);

                if (amountTaken > 0) OnMatched(coord, amountTaken, itemId, visual, itemFullyConsumed, partnerItemId, partnerVisual);
            }

            _conveyorManager.ClearCrossings();

            bool isStackFull = _exitStackAnchor.OccupiedCount == _exitStackAnchor.SlotCount;
            if (!isStackFull) _pendingDeadlockItemId = null;
            if (isStackFull && !_wasStackFull) CheckDeadlock();
            _wasStackFull = isStackFull;
        }

        // Decides and mutates state immediately; the food flying to the customer is the visual layer's job.
        private void OnMatched(
            Vector2Int coord,
            int amountTaken,
            int itemId,
            FoodItemController visual,
            bool itemFullyConsumed,
            int? partnerItemId,
            FoodItemController partnerVisual)
        {
            var result = _customerState.Consume(coord, amountTaken);
            if (result == RuntimeCustomerState.ConsumeResult.NotFound) return;

            bool levelWon = result == RuntimeCustomerState.ConsumeResult.Served && _customerState.RemainingCount == 0;

            _signalBus.Fire(new FoodConsumedSignal
            {
                ItemId = itemId,
                Visual = visual,
                TakenStack = new FoodStackData { Type = visual.Stack.Type, Amount = amountTaken },
                ItemFullyConsumed = itemFullyConsumed,
                PartnerItemId = partnerItemId,
                PartnerVisual = partnerVisual,
                CustomerCoord = coord,
                ServeResult = result,
                LevelWon = levelWon
            });

            if (result == RuntimeCustomerState.ConsumeResult.Served) RevealHiddenNeighbors(coord);
        }

        private void RevealHiddenNeighbors(Vector2Int coord)
        {
            bool anyRevealed = false;
            var directions = CustomerSightLines.Directions;
            for (int d = 0; d < directions.Length; d++)
            {
                var neighborCoord = coord + directions[d];
                if (!_customerState.TryGetCustomer(neighborCoord, out var neighbor) || !neighbor.IsHidden) continue;

                _customerState.SetHidden(neighborCoord, false);
                neighbor.IsHidden = false;
                _customerSpawner.RefreshCustomerVisual(neighborCoord, neighbor);
                anyRevealed = true;
            }

            if (anyRevealed && _audioService != null && _audioLibrary != null && _audioLibrary.CustomerRevealSfx != null)
            {
                _audioService.PlaySfx(_audioLibrary.CustomerRevealSfx);
            }
        }

        private void OnFoodDocked(FoodDockedSignal signal) => _lastDockedItemId = signal.ItemId;

        private void OnFoodDockLanded(FoodDockLandedSignal signal)
        {
            if (_lastDockedItemId == signal.ItemId) _lastDockedItemId = null;
            if (_pendingDeadlockItemId != signal.ItemId) return;

            _pendingDeadlockItemId = null;

            // Re-checked on landing: a consume, reveal or freed slot during the flight can break the deadlock.
            if (IsDeadlocked()) EndLevel(false);
        }

        // No free slot at all for this item: an unconditional loss.
        private void OnExitStackOverflow(ExitStackOverflowSignal signal)
        {
            _pendingDeadlockItemId = null;
            EndLevel(false);
        }

        private void OnBeltCleared() => _pendingDeadlockItemId = null;

        private void OnLevelResumed()
        {
            _levelEnded = false;
            _pendingDeadlockItemId = null;

            // Keep Playing grows the stack, so filling it up again has to count as a new full edge.
            _wasStackFull = false;
        }

        private void CheckDeadlock()
        {
            if (_levelEnded || !IsDeadlocked()) return;

            // Fail only once the item that filled the stack has visibly landed in its slot.
            if (_lastDockedItemId.HasValue) _pendingDeadlockItemId = _lastDockedItemId;
            else EndLevel(false);
        }

        private bool IsDeadlocked()
        {
            if (_exitStackAnchor.OccupiedCount < _exitStackAnchor.SlotCount) return false;

            for (int i = 0; i < _exitStackAnchor.SlotCount; i++)
            {
                if (!_exitStackAnchor.TryGetOccupant(i, out var occupant)) continue;
                if (_sightLines.CanReachAnyCustomer(occupant.Stack.Type)) return false;
            }

            return true;
        }

        private void EndLevel(bool won)
        {
            if (_levelEnded) return;

            // Every order is already served: the win arrives with the last consume flight, never fail over it.
            if (!won && _customerState.RemainingCount == 0) return;

            _levelEnded = true;
            OnLevelEnded?.Invoke(won);
        }
    }
}
