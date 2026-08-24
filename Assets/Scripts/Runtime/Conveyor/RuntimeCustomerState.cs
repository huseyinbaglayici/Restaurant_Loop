using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class RuntimeCustomerState
    {
        private readonly Dictionary<Vector2Int, CustomerEntity> _customers = new();

        public RuntimeCustomerState(ConveyorLevelDataSO level)
        {
            foreach (var customer in level.Customers)
                _customers[customer.Coord] = customer;
        }

        public int RemainingCount => _customers.Count;

        public bool TryGetCustomer(Vector2Int coord, out CustomerEntity customer) =>
            _customers.TryGetValue(coord, out customer);

        public void Remove(Vector2Int coord) => _customers.Remove(coord);

        public enum ConsumeResult { NotFound, Reduced, Promoted, Served }

        public ConsumeResult Consume(Vector2Int coord, int amountTaken)
        {
            if (!_customers.TryGetValue(coord, out var customer)) return ConsumeResult.NotFound;

            var order = customer.Order;
            order.Amount -= amountTaken;

            if (order.Amount > 0)
            {
                customer.Order = order;
                _customers[coord] = customer;
                return ConsumeResult.Reduced;
            }

            if (customer.HasSecondOrder)
            {
                customer.Order = customer.SecondOrder;
                customer.HasSecondOrder = false;
                customer.SecondOrder = default;
                _customers[coord] = customer;
                return ConsumeResult.Promoted;
            }

            _customers.Remove(coord);
            return ConsumeResult.Served;
        }

        public void SetHidden(Vector2Int coord, bool hidden)
        {
            if (!_customers.TryGetValue(coord, out var customer)) return;
            customer.IsHidden = hidden;
            _customers[coord] = customer;
        }
    }
}