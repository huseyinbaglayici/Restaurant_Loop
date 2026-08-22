using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // item-centric, event-driven
    public class CustomerConsumeService : IInitializable, ITickable
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

        // [pathIndex][direction] -> ray, in outward order.
        private List<RayEntry>[][] _raysByPathIndex;

        private bool _wasStackFull;

        public event Action OnLevelFailed;

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        public CustomerConsumeService(
            ConveyorLevelDataSO level,
            ConveyorManager conveyorManager,
            RuntimeCustomerState customerState,
            CustomerSpawner customerSpawner,
            ExitStackAnchor exitStackAnchor)
        {
            _level = level;
            _conveyorManager = conveyorManager;
            _customerState = customerState;
            _customerSpawner = customerSpawner;
            _exitStackAnchor = exitStackAnchor;
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
                var type = visual.Stack.Type;
                var rays = _raysByPathIndex[pathIndex];

                for (int d = 0; d < rays.Length; d++)
                {
                    if (!TryFindCustomer(rays[d], type, out var coord)) continue;
                    if (!_customerState.TryGetCustomer(coord, out var customer)) break;

                    int amountTaken = _conveyorManager.ConsumeFromItem(visual, customer.Order.Amount);
                    if (amountTaken > 0) OnMatched(coord, amountTaken);
                    break;
                }
            }

            _conveyorManager.ClearCrossings();

            bool isStackFull = _exitStackAnchor.OccupiedCount == _exitStackAnchor.SlotCount;
            if (isStackFull && !_wasStackFull) CheckDeadlock();
            _wasStackFull = isStackFull;
        }

        private void CheckDeadlock()
        {
            for (int i = 0; i < _exitStackAnchor.SlotCount; i++)
            {
                if (!_exitStackAnchor.TryGetOccupant(i, out var occupant)) continue;
                if (WouldReach(occupant.Stack.Type)) return;
            }

            Debug.Log("[CustomerConsumeService] Exit stack full and no occupant is reachable — level failed.");
            OnLevelFailed?.Invoke();
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

        private void OnMatched(Vector2Int coord, int amountTaken)
        {
            var result = _customerState.Consume(coord, amountTaken);
            switch (result)
            {
                case RuntimeCustomerState.ConsumeResult.Reduced:
                case RuntimeCustomerState.ConsumeResult.Promoted:
                    _customerState.TryGetCustomer(coord, out var updated);
                    _customerSpawner.RefreshCustomerVisual(coord, updated);
                    break;

                case RuntimeCustomerState.ConsumeResult.Served:
                    _customerSpawner.DespawnCustomer(coord);
                    RevealHiddenNeighbors(coord);
                    break;
            }
        }

        private void RevealHiddenNeighbors(Vector2Int coord)
        {
            for (int d = 0; d < Directions.Length; d++)
            {
                var neighborCoord = coord + Directions[d];
                if (!_customerState.TryGetCustomer(neighborCoord, out var neighbor) || !neighbor.IsHidden) continue;

                _customerState.SetHidden(neighborCoord, false);
                neighbor.IsHidden = false;
                _customerSpawner.RefreshCustomerVisual(neighborCoord, neighbor);
            }
        }
    }
}
