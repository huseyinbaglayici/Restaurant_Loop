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

        [SerializeField] private GameObject _secondOrderHolder;
        [SerializeField] private Image _secondFoodImage;
        [SerializeField] private Image _secondBackgroundImage;
        [SerializeField] private TMP_Text _secondAmountText;

        private float _secondFoodDimAlpha = 1f;
        private float _secondBackgroundDimAlpha = 1f;
        private float _secondAmountDimAlpha = 1f;
        private bool _dimAlphaCaptured;

        public void OnSpawned(Vector3 position, float worldYRotation, FoodStackData order)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, worldYRotation, 0f));
            if (_canvasRoot != null) _canvasRoot.localRotation = Quaternion.Euler(0f, -worldYRotation, 0f);
            ApplyOrder(order);
            ApplySecondOrder(false, default);
            gameObject.SetActive(true);
        }

        public void OnDespawned()
        {
            gameObject.SetActive(false);
        }

        public void ApplyOrder(FoodStackData order)
        {
            if (_foodImage != null) _foodImage.color = FoodTypeUtil.ToColor(order.Type);
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
                SetAlpha(_secondBackgroundImage, _secondBackgroundDimAlpha);
                SetAlpha(_secondAmountText, _secondAmountDimAlpha);

                if (_secondFoodImage != null)
                {
                    var c = FoodTypeUtil.ToColor(secondOrder.Type);
                    c.a = _secondFoodImage.color.a;
                    _secondFoodImage.color = c;
                }

                if (_secondAmountText != null)
                    _secondAmountText.text = "x" + secondOrder.Amount;
            }
            else
            {
                // promoted
                SetAlpha(_secondFoodImage, 1f);
                SetAlpha(_secondBackgroundImage, 1f);
                SetAlpha(_secondAmountText, 1f);
            }
        }

        private void CaptureDimAlphaOnce()
        {
            if (_dimAlphaCaptured) return;
            _dimAlphaCaptured = true;

            if (_secondFoodImage != null) _secondFoodDimAlpha = _secondFoodImage.color.a;
            if (_secondBackgroundImage != null) _secondBackgroundDimAlpha = _secondBackgroundImage.color.a;
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