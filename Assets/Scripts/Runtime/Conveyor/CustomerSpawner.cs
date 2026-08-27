using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class CustomerSpawner : IInitializable, IDisposable
    {
        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ConveyorTuning _tuning;
        private readonly CustomerController.Pool _customerPool;

        // keyed by coord
        private readonly Dictionary<Vector2Int, CustomerController> _spawned = new();

        public CustomerSpawner(ConveyorLevelDataSO level, ConveyorPrefabSettings prefabSettings, ConveyorTuning tuning, CustomerController.Pool customerPool)
        {
            _level = level;
            _prefabSettings = prefabSettings;
            _tuning = tuning;
            _customerPool = customerPool;
        }

        public void Initialize()
        {
            float cellSize = _prefabSettings.CellWorldSize;

            foreach (var customer in _level.Customers)
            {
                var position = ConveyorWorldUtil.ToWorldPosition(customer.Coord, cellSize, _tuning.CustomerHeight, _tuning.WorldOrigin);
                var visual = _customerPool.Spawn(position, 0f, customer.Order);
                visual.ApplyOrder(customer.Order, customer.IsHidden);
                visual.ApplySecondOrder(customer.HasSecondOrder, customer.SecondOrder);
                _spawned[customer.Coord] = visual;
            }
        }

        // peek-only
        public bool TryGetVisual(Vector2Int coord, out CustomerController visual) =>
            _spawned.TryGetValue(coord, out visual);

        public void RefreshCustomerVisual(Vector2Int coord, CustomerEntity customer)
        {
            if (!_spawned.TryGetValue(coord, out var visual)) return;
            visual.ApplyOrder(customer.Order, customer.IsHidden);
            visual.ApplySecondOrder(customer.HasSecondOrder, customer.SecondOrder);
        }

        public void HideOrderUI(Vector2Int coord)
        {
            if (!_spawned.TryGetValue(coord, out var visual)) return;
            visual.HideOrderUI();
        }

        public void PlayConsumePunch(Vector2Int coord, Vector3 strength, float duration, int vibrato, float elasticity)
        {
            if (!_spawned.TryGetValue(coord, out var visual)) return;
            visual.PlayConsumePunch(strength, duration, vibrato, elasticity);
        }

        public void DespawnCustomer(Vector2Int coord)
        {
            if (!_spawned.TryGetValue(coord, out var visual)) return;
            _customerPool.Despawn(visual);
            _spawned.Remove(coord);
        }

        // hasSecondOrderRemaining == true: customer stays (second order now active), just reacts.
        // hasSecondOrderRemaining == false: customer is fully served — reacts, then despawns itself.
        public void PlayServeReaction(Vector2Int coord, bool hasSecondOrderRemaining)
        {
            if (!_spawned.TryGetValue(coord, out var visual)) return;

            Action onComplete = hasSecondOrderRemaining ? null : () => DespawnCustomer(coord);
            visual.PlayServeReaction(hasSecondOrderRemaining, onComplete);
        }

        public void Dispose()
        {
            foreach (var customer in _spawned.Values) _customerPool.Despawn(customer);
            _spawned.Clear();
        }
    }
}
