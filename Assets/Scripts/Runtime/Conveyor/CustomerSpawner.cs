using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class CustomerSpawner : IInitializable, IDisposable
    {
        // Matches the old per-customer Canvas's anchoredPosition.y — the bubble always sat this far
        // above the customer's pivot, unaffected by the customer's own facing (see ApplySecondOrder
        // history: canvasRoot's local rotation canceled the customer's Y rotation, net world rotation
        // was always identity).
        private const float OrderBubbleHeight = 3f;

        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ConveyorTuning _tuning;
        private readonly CustomerController.Pool _customerPool;
        private readonly CustomerOrderWidget.Pool _orderWidgetPool;

        // keyed by coord
        private readonly Dictionary<Vector2Int, CustomerController> _spawnedCustomers = new();
        private readonly Dictionary<Vector2Int, CustomerOrderWidget> _spawnedWidgets = new();

        public CustomerSpawner(ConveyorLevelDataSO level, ConveyorPrefabSettings prefabSettings, ConveyorTuning tuning,
            CustomerController.Pool customerPool, CustomerOrderWidget.Pool orderWidgetPool)
        {
            _level = level;
            _prefabSettings = prefabSettings;
            _tuning = tuning;
            _customerPool = customerPool;
            _orderWidgetPool = orderWidgetPool;
        }

        public void Initialize()
        {
            float cellSize = _prefabSettings.CellWorldSize;
            var beltOrigin = ConveyorWorldUtil.ComputeBeltOrigin(_level, _tuning, cellSize);

            foreach (var customer in _level.Customers)
            {
                var position = ConveyorWorldUtil.ToWorldPosition(customer.Coord, cellSize, _tuning.CustomerHeight, beltOrigin);

                var visual = _customerPool.Spawn(position, 0f);

                var widget = _orderWidgetPool.Spawn(position + Vector3.up * OrderBubbleHeight, Quaternion.identity);
                widget.ApplyOrder(customer.Order, customer.IsHidden);
                widget.ApplySecondOrder(customer.HasSecondOrder, customer.SecondOrder);

                _spawnedCustomers[customer.Coord] = visual;
                _spawnedWidgets[customer.Coord] = widget;
            }
        }

        // peek-only
        public bool TryGetVisual(Vector2Int coord, out CustomerController visual) =>
            _spawnedCustomers.TryGetValue(coord, out visual);

        public void RefreshCustomerVisual(Vector2Int coord, CustomerEntity customer)
        {
            if (!_spawnedWidgets.TryGetValue(coord, out var widget)) return;
            widget.ApplyOrder(customer.Order, customer.IsHidden);
            widget.ApplySecondOrder(customer.HasSecondOrder, customer.SecondOrder);
        }

        public void HideOrderUI(Vector2Int coord)
        {
            if (!_spawnedWidgets.TryGetValue(coord, out var widget)) return;
            widget.SetVisible(false);
        }

        public void PlayConsumePunch(Vector2Int coord, Vector3 strength, float duration, int vibrato, float elasticity)
        {
            if (!_spawnedCustomers.TryGetValue(coord, out var visual)) return;
            visual.PlayConsumePunch(strength, duration, vibrato, elasticity);
        }

        public void DespawnCustomer(Vector2Int coord)
        {
            if (_spawnedCustomers.TryGetValue(coord, out var visual))
            {
                _customerPool.Despawn(visual);
                _spawnedCustomers.Remove(coord);
            }

            if (_spawnedWidgets.TryGetValue(coord, out var widget))
            {
                _orderWidgetPool.Despawn(widget);
                _spawnedWidgets.Remove(coord);
            }
        }

        // hasSecondOrderRemaining == true: customer stays (second order now active), just reacts.
        // hasSecondOrderRemaining == false: customer is fully served — reacts, then despawns itself.
        public void PlayServeReaction(Vector2Int coord, bool hasSecondOrderRemaining)
        {
            if (!_spawnedCustomers.TryGetValue(coord, out var visual)) return;

            Action onComplete = hasSecondOrderRemaining ? null : () => DespawnCustomer(coord);
            visual.PlayServeReaction(hasSecondOrderRemaining, onComplete);
        }

        public void Dispose()
        {
            foreach (var customer in _spawnedCustomers.Values) _customerPool.Despawn(customer);
            foreach (var widget in _spawnedWidgets.Values) _orderWidgetPool.Despawn(widget);
            _spawnedCustomers.Clear();
            _spawnedWidgets.Clear();
        }
    }
}
