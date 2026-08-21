using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class RuntimeTableState
    {
        private readonly Dictionary<Vector2Int, List<FoodStackData>> _orders = new();

        public RuntimeTableState(ConveyorLevelDataSO level)
        {
            foreach (var table in level.Tables)
                _orders[table.Coord] = new List<FoodStackData>(table.Orders);
        }

        public bool TryGetFrontOrder(Vector2Int tableCoord, out FoodStackData order)
        {
            if (_orders.TryGetValue(tableCoord, out var queue) && queue.Count > 0)
            {
                order = queue[0];
                return true;
            }

            order = default;
            return false;
        }

        public void ConsumeFront(Vector2Int tableCoord)
        {
            if (_orders.TryGetValue(tableCoord, out var queue) && queue.Count > 0)
                queue.RemoveAt(0);
        }
    }
}