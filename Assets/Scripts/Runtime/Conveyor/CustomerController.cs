using System;
using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class CustomerController : MonoBehaviour, IPoolable<Vector3, float, FoodStackData>
    {
        // World-space handoff point the consumed FoodItemController flies to (ConveyorVisualService).
        // Should sit on the character's hand bone, not the UI canvas.
        [SerializeField] private Transform _foodHandoffAnchor;

        [SerializeField] private CustomerPlayableAnimator _playableAnimator;

        [Inject] private CustomerOrderWidget.Pool _orderWidgetPool;

        // Matches the old per-customer Canvas's anchoredPosition.y — the bubble always sat this far
        // above the customer's pivot, unaffected by the customer's own facing (see ApplySecondOrder
        // history: canvasRoot's local rotation canceled the customer's Y rotation, net world rotation
        // was always identity).
        private const float OrderBubbleHeight = 3f;

        private CustomerOrderWidget _orderWidget;
        private Tween _consumePunchTween;

        public Transform FoodHandoffAnchor => _foodHandoffAnchor;

        public void OnSpawned(Vector3 position, float worldYRotation, FoodStackData order)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, worldYRotation, 0f));

            var widgetPosition = position + Vector3.up * OrderBubbleHeight;
            _orderWidget = _orderWidgetPool.Spawn(widgetPosition, Quaternion.identity);
            ApplyOrder(order);
            ApplySecondOrder(false, default);
            gameObject.SetActive(true);

            if (_playableAnimator != null) _playableAnimator.PlayIdleWithRandomOffset();
        }

        public void OnDespawned()
        {
            _consumePunchTween?.Kill();

            if (_orderWidget != null)
            {
                _orderWidgetPool.Despawn(_orderWidget);
                _orderWidget = null;
            }

            gameObject.SetActive(false);
        }

        // Very small scale punch on the model root each time a food unit lands on this customer.
        public void PlayConsumePunch(Vector3 strength, float duration, int vibrato, float elasticity)
        {
            if (_playableAnimator == null) return;

            var target = _playableAnimator.transform;
            _consumePunchTween?.Kill(true);
            _consumePunchTween = target.DOPunchScale(strength, duration, vibrato, elasticity);
        }

        // hasSecondOrderRemaining == true: plays the "first order served" reaction, then returns to Idle.
        // hasSecondOrderRemaining == false: plays a happy reaction, then invokes onComplete (caller despawns).
        public void PlayServeReaction(bool hasSecondOrderRemaining, Action onComplete)
        {
            if (_playableAnimator == null)
            {
                onComplete?.Invoke();
                return;
            }

            _playableAnimator.PlayServeReaction(hasSecondOrderRemaining, onComplete);
        }

        // Fully served: the order UI should read as "done" the instant it happens, not linger through
        // the reaction animation until the whole customer despawns.
        public void HideOrderUI() => _orderWidget?.SetVisible(false);

        public void ApplyOrder(FoodStackData order) => _orderWidget?.ApplyOrder(order);

        public void ApplySecondOrder(bool hasSecondOrder, FoodStackData secondOrder) =>
            _orderWidget?.ApplySecondOrder(hasSecondOrder, secondOrder);

        public class Pool : MonoPoolableMemoryPool<Vector3, float, FoodStackData, CustomerController>
        {
        }
    }
}