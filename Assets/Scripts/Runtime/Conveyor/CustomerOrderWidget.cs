using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Lives under the single shared world-space order canvas (see ConveyorInstaller.BindCustomerPool)
    // instead of each customer owning its own Canvas. Pooled and repositioned per spawn.
    public class CustomerOrderWidget : MonoBehaviour, IPoolable<Vector3, Quaternion>
    {
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private Transform _firstOrderHolder;
        [SerializeField] private Image _foodImage;
        [SerializeField] private TMP_Text _amountText;
        [SerializeField] private GameObject _secondOrderHolder;
        [SerializeField] private Image _secondFoodImage;
        [SerializeField] private TMP_Text _secondAmountText;

        // All Image graphics across every pooled widget share this one parent, and all TMP_Text graphics
        // share the other — uGUI only batches consecutive same-material siblings, so this is what makes
        // the icons batch with each other and the texts batch with each other, instead of each widget's
        // Image/Text/Image/Text interleaving breaking every batch. Reparented out on Spawn (after this
        // widget's own transform hierarchy has positioned them correctly), reparented back on Despawn.
        [Inject(Id = "OrderIconsGroup")] private Transform _iconsGroup;
        [Inject(Id = "OrderTextsGroup")] private Transform _textsGroup;

        [Inject(Optional = true)] private FoodMeshSettings _foodMeshSettings;

        private float _secondFoodDimAlpha = 1f;
        private float _secondAmountDimAlpha = 1f;
        private bool _dimAlphaCaptured;
        private bool _hasSecondOrder;

        public void OnSpawned(Vector3 position, Quaternion rotation)
        {
            _rectTransform.SetPositionAndRotation(position, rotation);
            gameObject.SetActive(true);

            _foodImage.transform.SetParent(_iconsGroup, true);
            _amountText.transform.SetParent(_textsGroup, true);
            _secondFoodImage.transform.SetParent(_iconsGroup, true);
            _secondAmountText.transform.SetParent(_textsGroup, true);

            _foodImage.transform.localScale = Vector3.one;
            if (_amountText != null) _amountText.transform.localScale = Vector3.one;
        }

        public void OnDespawned()
        {
            _foodImage.transform.DOKill(true);
            if (_amountText != null) _amountText.transform.DOKill(true);

            _foodImage.transform.SetParent(_firstOrderHolder, false);
            _amountText.transform.SetParent(_firstOrderHolder, false);
            _secondFoodImage.transform.SetParent(_secondOrderHolder.transform, false);
            _secondAmountText.transform.SetParent(_secondOrderHolder.transform, false);

            _foodImage.transform.localScale = Vector3.one;
            if (_amountText != null) _amountText.transform.localScale = Vector3.one;

            gameObject.SetActive(false);
        }

        private bool _isHidden;
        private bool _wasHidden;

        public void SetVisible(bool visible)
        {
            _foodImage.gameObject.SetActive(visible);
            _amountText.gameObject.SetActive(visible && !_isHidden);
            _secondFoodImage.gameObject.SetActive(visible && _hasSecondOrder && !_isHidden);
            _secondAmountText.gameObject.SetActive(visible && _hasSecondOrder && !_isHidden);
        }

        public void ApplyOrder(FoodStackData order, bool isHidden = false)
        {
            bool isRevealing = _wasHidden && !isHidden;
            _isHidden = isHidden;
            _wasHidden = isHidden;

            if (isHidden)
            {
                if (_foodMeshSettings != null && _foodMeshSettings.HiddenOrderBubble != null)
                {
                    _foodImage.sprite = _foodMeshSettings.HiddenOrderBubble;
                    _foodImage.color = new Color(1f, 1f, 1f, _foodImage.color.a);
                }
                else
                {
                    _foodImage.sprite = null;
                    _foodImage.color = new Color(0.3f, 0.3f, 0.3f, _foodImage.color.a);
                }

                if (_amountText != null)
                {
                    _amountText.gameObject.SetActive(true);
                    _amountText.text = "?";
                }
                if (_secondFoodImage != null) _secondFoodImage.gameObject.SetActive(false);
                if (_secondAmountText != null) _secondAmountText.gameObject.SetActive(false);
                return;
            }

            if (_amountText != null)
            {
                _amountText.gameObject.SetActive(true);
                _amountText.text = order.Amount.ToString();
            }
            ApplyFoodSprite(_foodImage, order.Type);

            if (isRevealing)
            {
                PlayRevealAnimation();
            }
        }

        public void PlayRevealAnimation()
        {
            _foodImage.transform.DOKill(true);
            _foodImage.transform.localScale = Vector3.one * 0.3f;
            _foodImage.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);

            if (_amountText != null)
            {
                _amountText.transform.DOKill(true);
                _amountText.transform.localScale = Vector3.one * 0.3f;
                _amountText.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
            }
        }

        public void ApplySecondOrder(bool hasSecondOrder, FoodStackData secondOrder)
        {
            CaptureDimAlphaOnce();

            _hasSecondOrder = hasSecondOrder;
            if (_secondFoodImage != null) _secondFoodImage.gameObject.SetActive(hasSecondOrder && !_isHidden);
            if (_secondAmountText != null) _secondAmountText.gameObject.SetActive(hasSecondOrder && !_isHidden);

            if (hasSecondOrder)
            {
                // dimmed
                SetAlpha(_secondFoodImage, _secondFoodDimAlpha);
                SetAlpha(_secondAmountText, _secondAmountDimAlpha);

                ApplyFoodSprite(_secondFoodImage, secondOrder.Type);

                if (_secondAmountText != null)
                    _secondAmountText.text = secondOrder.Amount.ToString();
            }
            else
            {
                // promoted
                SetAlpha(_secondFoodImage, 1f);
                SetAlpha(_secondAmountText, 1f);
            }
        }

        // Sprite-only visual: no separate background image, the food sprite carries the full look.
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

        public class Pool : MonoPoolableMemoryPool<Vector3, Quaternion, CustomerOrderWidget>
        {
        }
    }
}
