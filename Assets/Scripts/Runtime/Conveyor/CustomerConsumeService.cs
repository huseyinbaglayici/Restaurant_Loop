using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // item-centric, event-driven
    public class CustomerConsumeService : IInitializable, ITickable, IDisposable
    {
        private struct RayEntry
        {
            public bool IsCustomer;
            public Vector2Int CustomerCoord;
            public int PathIndex;
        }

        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorManager _conveyorManager;
        private readonly RuntimeCustomerState _customerState;
        private readonly CustomerSpawner _customerSpawner;
        private readonly ExitStackAnchor _exitStackAnchor;
        private readonly SignalBus _signalBus;
        private readonly RestaurantLoop.Runtime.Audio.AudioService _audioService;
        private readonly AudioLibrary _audioLibrary;

        // [pathIndex][direction] -> ray, in outward order.
        private List<RayEntry>[][] _raysByPathIndex;

        private bool _wasStackFull;

        // Tracks which belt item most recently docked (set from FoodDockedSignal, same frame the dock is
        // decided) so a deadlock fail can wait for that specific item's fly-to-slot flight to land instead
        // of firing the instant the stack goes full, before the player has actually seen it arrive.
        private int? _lastDockedItemId;
        private int? _pendingDeadlockItemId;

        // OnLevelEnded fires at most once until Keep Playing resumes the level (LevelResumedSignal).
        private bool _levelEnded;

        public event Action<bool> OnLevelEnded;

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        public CustomerConsumeService(
            ConveyorLevelDataSO level,
            ConveyorManager conveyorManager,
            RuntimeCustomerState customerState,
            CustomerSpawner customerSpawner,
            ExitStackAnchor exitStackAnchor,
            SignalBus signalBus,
            RestaurantLoop.Runtime.Audio.AudioService audioService,
            AudioLibrary audioLibrary)
        {
            _level = level;
            _conveyorManager = conveyorManager;
            _customerState = customerState;
            _customerSpawner = customerSpawner;
            _exitStackAnchor = exitStackAnchor;
            _signalBus = signalBus;
            _audioService = audioService;
            _audioLibrary = audioLibrary;
        }

        public void Initialize()
        {
            var orderedPath = _level.BakedPath.OrderedPath;
            var pathIndexByCoord = new Dictionary<Vector2Int, int>();
            for (int i = 0; i < orderedPath.Count; i++) pathIndexByCoord[orderedPath[i]] = i;

            var customerCoords = new HashSet<Vector2Int>();
            foreach (var customer in _level.Customers) customerCoords.Add(customer.Coord);

            _raysByPathIndex = new List<RayEntry>[orderedPath.Count][];
            for (int i = 0; i < orderedPath.Count; i++)
            {
                _raysByPathIndex[i] = new List<RayEntry>[Directions.Length];
                for (int d = 0; d < Directions.Length; d++)
                    _raysByPathIndex[i][d] = BuildRay(orderedPath[i], Directions[d], pathIndexByCoord, customerCoords);
            }

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

        private void OnFoodDocked(FoodDockedSignal signal) => _lastDockedItemId = signal.ItemId;

        private void OnFoodDockLanded(FoodDockLandedSignal signal)
        {
            if (_lastDockedItemId == signal.ItemId) _lastDockedItemId = null;
            if (_pendingDeadlockItemId != signal.ItemId) return;

            _pendingDeadlockItemId = null;

            // Re-checked on landing: a consume, reveal or freed slot during the flight can break the deadlock.
            if (!IsDeadlocked()) return;

            Debug.Log("[CustomerConsumeService] Deadlocked item landed in the exit stack — level failed.");
            EndLevel(false);
        }

        // Stack had no free slot at all for this item — unconditional loss, no WouldReach check needed
        // (there's nowhere left to put anything, reachable or not).
        private void OnExitStackOverflow(ExitStackOverflowSignal signal)
        {
            _pendingDeadlockItemId = null;
            Debug.Log("[CustomerConsumeService] Exit stack overflowed — level failed.");
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

        private void EndLevel(bool won)
        {
            if (_levelEnded) return;

            // Every order is already served — the win arrives with the last consume flight, never fail over it.
            if (!won && _customerState.RemainingCount == 0) return;

            _levelEnded = true;
            OnLevelEnded?.Invoke(won);
        }

        private List<RayEntry> BuildRay(
            Vector2Int origin, Vector2Int dir,
            Dictionary<Vector2Int, int> pathIndexByCoord, HashSet<Vector2Int> customerCoords)
        {
            var ray = new List<RayEntry>();
            var cursor = origin + dir;

            while (cursor.x >= 0 && cursor.x < _level.GridWidth && cursor.y >= 0 && cursor.y < _level.GridHeight)
            {
                if (customerCoords.Contains(cursor))
                    ray.Add(new RayEntry { IsCustomer = true, CustomerCoord = cursor });
                else if (pathIndexByCoord.TryGetValue(cursor, out int pathIndex))
                    ray.Add(new RayEntry { IsCustomer = false, PathIndex = pathIndex });

                cursor += dir;
            }

            return ray;
        }

        public void Tick()
        {
            var crossings = _conveyorManager.CrossingsThisFrame;
            for (int c = 0; c < crossings.Count; c++)
            {
                var (pathIndex, visual) = crossings[c];
                if (visual == null || visual.Stack.Amount <= 0) continue;

                var type = visual.Stack.Type;
                var rays = _raysByPathIndex[pathIndex];

                for (int d = 0; d < rays.Length; d++)
                {
                    if (!TryFindCustomer(rays[d], type, out var coord)) continue;
                    if (!_customerState.TryGetCustomer(coord, out var customer)) break;

                    int amountTaken = _conveyorManager.ConsumeFromItem(
                        visual, 1, out int itemId, out bool itemFullyConsumed,
                        out int? partnerItemId, out FoodItemController partnerVisual);

                    if (amountTaken > 0) OnMatched(coord, amountTaken, itemId, visual, itemFullyConsumed, partnerItemId, partnerVisual);
                    break;
                }
            }

            _conveyorManager.ClearCrossings();

            bool isStackFull = _exitStackAnchor.OccupiedCount == _exitStackAnchor.SlotCount;
            if (!isStackFull) _pendingDeadlockItemId = null;
            if (isStackFull && !_wasStackFull) CheckDeadlock();
            _wasStackFull = isStackFull;
        }

        private void CheckDeadlock()
        {
            if (_levelEnded || !IsDeadlocked()) return;

            Debug.Log("[CustomerConsumeService] Exit stack full and no occupant is reachable.");

            // Wait for the item that just filled the stack to actually finish flying into its slot before
            // telling the player the level failed — firing this the instant the dock is decided (same
            // frame, before FoodFlightService has moved it) shows the fail panel while the item is still
            // sitting back at the belt end. _lastDockedItemId is cleared once that flight lands.
            if (_lastDockedItemId.HasValue) _pendingDeadlockItemId = _lastDockedItemId;
            else EndLevel(false);
        }

        private bool IsDeadlocked()
        {
            if (_exitStackAnchor.OccupiedCount < _exitStackAnchor.SlotCount) return false;

            for (int i = 0; i < _exitStackAnchor.SlotCount; i++)
            {
                if (!_exitStackAnchor.TryGetOccupant(i, out var occupant)) continue;
                if (WouldReach(occupant.Stack.Type)) return false;
            }

            return true;
        }

        // could this type, injected at the belt entry right now, reach an unblocked matching customer?
        private bool WouldReach(FoodType type)
        {
            int limit = _raysByPathIndex.Length - 1; // last index never gets a consume check before docking (see ConveyorManager.Tick)
            for (int i = 0; i < limit; i++)
            {
                var rays = _raysByPathIndex[i];
                for (int d = 0; d < rays.Length; d++)
                    if (TryFindCustomer(rays[d], type, out _)) return true;
            }

            return false;
        }

        private bool TryFindCustomer(List<RayEntry> ray, FoodType itemType, out Vector2Int coord)
        {
            for (int i = 0; i < ray.Count; i++)
            {
                var entry = ray[i];

                if (entry.IsCustomer)
                {
                    if (!_customerState.TryGetCustomer(entry.CustomerCoord, out var customer)) continue;

                    if (!customer.IsHidden && customer.Order.Type == itemType)
                    {
                        coord = entry.CustomerCoord;
                        return true;
                    }

                    coord = default;
                    return false;
                }

                if (_conveyorManager.TryPeekTypeAt(entry.PathIndex, out _))
                {
                    coord = default;
                    return false;
                }
            }

            coord = default;
            return false;
        }

        // Decides and mutates state immediately; the cosmetic half (food flying to the customer, order UI
        // refresh, serve reaction, despawn) is ConveyorVisualService's job, driven off FoodConsumedSignal.
        private void OnMatched(
            Vector2Int coord,
            int amountTaken,
            int itemId,
            FoodItemController visual,
            bool itemFullyConsumed,
            int? partnerItemId = null,
            FoodItemController partnerVisual = null)
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

            if (result != RuntimeCustomerState.ConsumeResult.Served) return;

            // data is already gone from _customerState — the despawn happens once the happy reaction
            // finishes, which the visual layer kicks off when the food lands.
            RevealHiddenNeighbors(coord);
        }

        public void NotifyLevelWon() => EndLevel(true);

        private void RevealHiddenNeighbors(Vector2Int coord)
        {
            bool anyRevealed = false;
            for (int d = 0; d < Directions.Length; d++)
            {
                var neighborCoord = coord + Directions[d];
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
    }
}
