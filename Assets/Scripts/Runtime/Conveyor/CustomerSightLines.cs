using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Straight grid lines from every belt cell outward in the four directions, precomputed once. A customer
    // can take food from a cell if it's the first customer on one of those lines and no belt item between
    // them blocks the view.
    public class CustomerSightLines
    {
        private struct RayEntry
        {
            public bool IsCustomer;
            public Vector2Int CustomerCoord;
            public int PathIndex;
        }

        public static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorManager _conveyorManager;
        private readonly RuntimeCustomerState _customerState;

        // [pathIndex][direction] -> ray, in outward order.
        private readonly List<RayEntry>[][] _raysByPathIndex;

        public CustomerSightLines(ConveyorLevelDataSO level, ConveyorManager conveyorManager, RuntimeCustomerState customerState)
        {
            _level = level;
            _conveyorManager = conveyorManager;
            _customerState = customerState;

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

        // First visible customer (checked up, down, left, right) at this belt cell who wants this type.
        public bool TryFindCustomer(int pathIndex, FoodType type, out Vector2Int coord)
        {
            var rays = _raysByPathIndex[pathIndex];
            for (int d = 0; d < rays.Length; d++)
                if (TryFindCustomer(rays[d], type, out coord)) return true;

            coord = default;
            return false;
        }

        // Could this type, entering the belt right now, reach a visible matching customer anywhere?
        public bool CanReachAnyCustomer(FoodType type)
        {
            // The last cell never gets a consume check before the item docks.
            int limit = _raysByPathIndex.Length - 1;
            for (int i = 0; i < limit; i++)
                if (TryFindCustomer(i, type, out _)) return true;

            return false;
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
    }
}
