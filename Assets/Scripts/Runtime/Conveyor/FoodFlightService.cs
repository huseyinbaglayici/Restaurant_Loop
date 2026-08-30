using System;
using System.Collections.Generic;
using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Pure visual helper: moves a transform from where it is now to a target that may keep moving,
    // with arc / Y-spin / easing taken from a ConveyorFeelSettings.FlightTuning block.
    // Knows nothing about belts, customers or gameplay rules.
    public class FoodFlightService : ITickable
    {
        private class Flight
        {
            public Transform Transform;
            public Vector3 Start;
            public Quaternion StartRotation;
            public Func<Vector3> TargetProvider;
            public ConveyorFeelSettings.FlightTuning Tuning;
            public float Duration;
            public float Elapsed;
            public Action OnArrive;
            public bool IsCancelled;
        }

        private readonly List<Flight> _flights = new();
        private readonly List<Flight> _completedThisTick = new();

        public bool IsFlying(Transform target)
        {
            for (int i = 0; i < _flights.Count; i++)
                if (_flights[i].Transform == target && !_flights[i].IsCancelled)
                    return true;

            return false;
        }

        // Re-launching on a transform that is already flying replaces the old flight (its OnArrive is dropped).
        public void Launch(
            Transform target,
            Func<Vector3> targetProvider,
            ConveyorFeelSettings.FlightTuning tuning,
            Action onArrive = null)
        {
            if (target == null || targetProvider == null || tuning == null)
            {
                onArrive?.Invoke();
                return;
            }

            Cancel(target);

            var start = target.position;
            float distance = Vector3.Distance(start, targetProvider());

            _flights.Add(new Flight
            {
                Transform = target,
                Start = start,
                StartRotation = target.rotation,
                TargetProvider = targetProvider,
                Tuning = tuning,
                Duration = Mathf.Max(0.0001f, tuning.ResolveDuration(distance)),
                Elapsed = 0f,
                OnArrive = onArrive
            });
        }

        public void Launch(
            Transform target,
            Vector3 targetPosition,
            ConveyorFeelSettings.FlightTuning tuning,
            Action onArrive = null)
            => Launch(target, () => targetPosition, tuning, onArrive);

        // Cancels silently — OnArrive is not invoked.
        public void Cancel(Transform target)
        {
            for (int i = 0; i < _flights.Count; i++)
                if (_flights[i].Transform == target)
                    _flights[i].IsCancelled = true;
        }

        public void CancelAll()
        {
            for (int i = 0; i < _flights.Count; i++) _flights[i].IsCancelled = true;
        }

        public void Tick()
        {
            if (_flights.Count == 0) return;

            float deltaTime = Time.deltaTime;

            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                var flight = _flights[i];

                if (flight.IsCancelled || flight.Transform == null)
                {
                    _flights.RemoveAt(i);
                    continue;
                }

                flight.Elapsed += deltaTime;
                float normalized = Mathf.Clamp01(flight.Elapsed / flight.Duration);
                float eased = DOVirtual.EasedValue(0f, 1f, normalized, flight.Tuning.Ease);

                var targetPosition = flight.TargetProvider();
                var position = Vector3.LerpUnclamped(flight.Start, targetPosition, eased);
                position.y += flight.Tuning.ArcHeight * Mathf.Sin(Mathf.PI * Mathf.Clamp01(eased));
                flight.Transform.position = position;

                // Spin is the only rotation this service is ever allowed to apply, and only around world Y.
                if (!Mathf.Approximately(flight.Tuning.SpinYDegreesPerSecond, 0f))
                    flight.Transform.Rotate(0f, flight.Tuning.SpinYDegreesPerSecond * deltaTime, 0f, Space.World);

                if (normalized < 1f) continue;

                flight.Transform.position = targetPosition;

                // Restores the rotation the item had before the flight — never identity, so a food whose
                // resting orientation isn't zero keeps it across queue -> belt -> stack.
                if (flight.Tuning.ResetRotationOnArrive) flight.Transform.rotation = flight.StartRotation;

                _flights.RemoveAt(i);
                _completedThisTick.Add(flight);
            }

            // Callbacks run after the list settles — an OnArrive is free to launch a new flight.
            for (int i = 0; i < _completedThisTick.Count; i++) _completedThisTick[i].OnArrive?.Invoke();
            _completedThisTick.Clear();
        }
    }
}
