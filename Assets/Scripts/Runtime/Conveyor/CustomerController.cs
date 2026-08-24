using System;
using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class CustomerController : MonoBehaviour, IPoolable<Vector3, float, FoodStackData>
    {
        [SerializeField] private Transform _canvasRoot;
        [SerializeField] private Image _foodImage;
        [SerializeField] private TMP_Text _amountText;

        // World-space handoff point the consumed FoodItemController flies to (ConveyorVisualService).
        // Should sit on the character's hand bone, not the UI canvas.
        [SerializeField] private Transform _foodHandoffAnchor;

        [SerializeField] private GameObject _secondOrderHolder;
        [SerializeField] private Image _secondFoodImage;
        [SerializeField] private TMP_Text _secondAmountText;

        [SerializeField] private CustomerVisualRandomizer _visualRandomizer;
        [SerializeField] private CustomerPlayableAnimator _playableAnimator;

        [Inject(Optional = true)] private FoodMeshSettings _foodMeshSettings;

        private float _secondFoodDimAlpha = 1f;
        private float _secondAmountDimAlpha = 1f;
        private bool _dimAlphaCaptured;

        private Tween _consumePunchTween;

        public Transform FoodHandoffAnchor => _foodHandoffAnchor;

        public void OnSpawned(Vector3 position, float worldYRotation, FoodStackData order)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, worldYRotation, 0f));
            if (_canvasRoot != null) _canvasRoot.localRotation = Quaternion.Euler(0f, -worldYRotation, 0f);
            ApplyOrder(order);
            ApplySecondOrder(false, default);
            gameObject.SetActive(true);

            if (_visualRandomizer != null) _visualRandomizer.Randomize();
            if (_playableAnimator != null) _playableAnimator.PlayIdleWithRandomOffset();
        }

        public void OnDespawned()
        {
            _consumePunchTween?.Kill();
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

        public void ApplyOrder(FoodStackData order)
        {
            ApplyFoodSprite(_foodImage, order.Type);
            if (_amountText != null) _amountText.text = "x" + order.Amount;
        }

        public void ApplySecondOrder(bool hasSecondOrder, FoodStackData secondOrder)
        {
            CaptureDimAlphaOnce();

            if (_secondOrderHolder != null) _secondOrderHolder.SetActive(hasSecondOrder);

            if (hasSecondOrder)
            {
                // dimmed
                SetAlpha(_secondFoodImage, _secondFoodDimAlpha);
                SetAlpha(_secondAmountText, _secondAmountDimAlpha);

                ApplyFoodSprite(_secondFoodImage, secondOrder.Type);

                if (_secondAmountText != null)
                    _secondAmountText.text = "x" + secondOrder.Amount;
            }
            else
            {
                // promoted
                SetAlpha(_secondFoodImage, 1f);
                SetAlpha(_secondAmountText, 1f);
            }
        }

        // Sprite-only visual: no separate background image anymore, the food sprite carries the full look.
        private void ApplyFoodSprite(Image image, FoodType type)
        {
            if (image == null) return;

            if (_foodMeshSettings != null && _foodMeshSettings.TryGetIcon(type, out var icon))
            {
                image.sprite = icon;
                image.color = new Color(1f, 1f, 1f, image.color.a);
            }
            else
            {
                image.sprite = null;
                var c = FoodTypeUtil.ToColor(type);
                c.a = image.color.a;
                image.color = c;
            }
        }

        private void CaptureDimAlphaOnce()
        {
            if (_dimAlphaCaptured) return;
            _dimAlphaCaptured = true;

            if (_secondFoodImage != null) _secondFoodDimAlpha = _secondFoodImage.color.a;
            if (_secondAmountText != null) _secondAmountDimAlpha = _secondAmountText.color.a;
        }

        private static void SetAlpha(Image image, float alpha)
        {
            if (image == null) return;
            var c = image.color;
            c.a = alpha;
            image.color = c;
        }

        private static void SetAlpha(TMP_Text text, float alpha)
        {
            if (text == null) return;
            var c = text.color;
            c.a = alpha;
            text.color = c;
        }

        public class Pool : MonoPoolableMemoryPool<Vector3, float, FoodStackData, CustomerController>
        {
        }
    }
}