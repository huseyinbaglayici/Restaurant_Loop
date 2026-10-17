using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Order bubble above a customer. Pooled under one shared world-space canvas instead of a Canvas per customer.
    public class CustomerOrderWidget : MonoBehaviour, IPoolable<Vector3, Quaternion>
    {
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private Transform _firstOrderHolder;
        [SerializeField] private Image _foodImage;
        [SerializeField] private TMP_Text _amountText;
        [SerializeField] private GameObject _secondOrderHolder;
        [SerializeField] private Image _secondFoodImage;
        [SerializeField] private TMP_Text _secondAmountText;

        // uGUI only batches consecutive same-material siblings, so every widget's icons share one parent and
        // texts another. Reparented out on spawn, back under this widget on despawn.
        [Inject(Id = "OrderIconsGroup")] private Transform _iconsGroup;
        [Inject(Id = "OrderTextsGroup")] private Transform _textsGroup;

        [Inject] private FoodMeshSettings _foodMeshSettings;
        [Inject(Optional = true)] private CameraController _cameraController;

        private float _secondFoodDimAlpha = 1f;
        private float _secondAmountDimAlpha = 1f;
        private bool _dimAlphaCaptured;
        private bool _hasSecondOrder;
        private bool _isHidden;
        private bool _wasHidden;

        private Tween _glowTween;
        private Tween _glowColorTween;

        private float GetBaseScale()
        {
            float scale = _foodMeshSettings != null && _foodMeshSettings.CustomerBubbleScale > 0f
                ? _foodMeshSettings.CustomerBubbleScale
                : 1f;

            if (_foodMeshSettings != null && _foodMeshSettings.ScaleBubbleWithCamera && _cameraController != null)
            {
                float zoom = _cameraController.CameraZoomFactor;
                scale *= (1f + (zoom - 1f) * _foodMeshSettings.BubbleCameraScaleMultiplier);
            }

            return scale;
        }

        public void OnSpawned(Vector3 position, Quaternion rotation)
        {
            _rectTransform.SetPositionAndRotation(position, rotation);
            gameObject.SetActive(true);

            _foodImage.transform.SetParent(_iconsGroup, true);
            _amountText.transform.SetParent(_textsGroup, true);
            _secondFoodImage.transform.SetParent(_iconsGroup, true);
            _secondAmountText.transform.SetParent(_textsGroup, true);

            float scale = GetBaseScale();
            _rectTransform.localScale = Vector3.one * scale;
            _foodImage.transform.localScale = Vector3.one * scale;
            if (_amountText != null) _amountText.transform.localScale = Vector3.one * scale;
            _secondFoodImage.transform.localScale = Vector3.one * (scale * 0.75f);
            if (_secondAmountText != null) _secondAmountText.transform.localScale = Vector3.one * (scale * 0.75f);
        }

        // Active widgets die with GameplayScene without a despawn; the looping pulse must not outlive them.
        private void OnDestroy()
        {
            _glowTween?.Kill();
            _glowColorTween?.Kill();
            if (_foodImage != null) { _foodImage.transform.DOKill(); _foodImage.DOKill(); }
            if (_amountText != null) _amountText.transform.DOKill();
        }

        public void OnDespawned()
        {
            if (this == null || gameObject == null) return;

            StopBubblePulsing();

            if (_foodImage != null)
            {
                _foodImage.transform.DOKill(true);
                if (_firstOrderHolder != null) _foodImage.transform.SetParent(_firstOrderHolder, false);
                _foodImage.transform.localScale = Vector3.one;
                _foodImage.color = new Color(1f, 1f, 1f, _foodImage.color.a);
            }

            if (_amountText != null)
            {
                _amountText.transform.DOKill(true);
                if (_firstOrderHolder != null) _amountText.transform.SetParent(_firstOrderHolder, false);
                _amountText.transform.localScale = Vector3.one;
            }

            if (_secondFoodImage != null && _secondOrderHolder != null)
            {
                _secondFoodImage.transform.SetParent(_secondOrderHolder.transform, false);
            }

            if (_secondAmountText != null && _secondOrderHolder != null)
            {
                _secondAmountText.transform.SetParent(_secondOrderHolder.transform, false);
            }

            gameObject.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            _foodImage.gameObject.SetActive(visible);
            _amountText.gameObject.SetActive(visible && !_isHidden);
            _secondFoodImage.gameObject.SetActive(visible && _hasSecondOrder && !_isHidden);
            _secondAmountText.gameObject.SetActive(visible && _hasSecondOrder && !_isHidden);

            if (!visible)
            {
                StopBubblePulsing();
            }
            else if (!_isHidden)
            {
                StartBubblePulsing();
            }
        }

        public void ApplyOrder(FoodStackData order, bool isHidden = false)
        {
            bool isRevealing = _wasHidden && !isHidden;
            _isHidden = isHidden;
            _wasHidden = isHidden;

            if (isHidden)
            {
                StopBubblePulsing();

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
            else
            {
                StartBubblePulsing();
            }
        }

        public void PlayRevealAnimation()
        {
            float scale = GetBaseScale();
            _foodImage.transform.DOKill(true);
            _foodImage.transform.localScale = Vector3.one * (scale * 0.3f);
            _foodImage.transform.DOScale(scale, 0.4f).SetEase(Ease.OutBack).OnComplete(() =>
            {
                if (!_isHidden)
                {
                    StartBubblePulsing();
                }
            });

            if (_amountText != null)
            {
                _amountText.transform.DOKill(true);
                _amountText.transform.localScale = Vector3.one * (scale * 0.3f);
                _amountText.transform.DOScale(scale, 0.4f).SetEase(Ease.OutBack);
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
                SetAlpha(_secondFoodImage, _secondFoodDimAlpha);
                SetAlpha(_secondAmountText, _secondAmountDimAlpha);

                ApplyFoodSprite(_secondFoodImage, secondOrder.Type);

                if (_secondAmountText != null)
                    _secondAmountText.text = secondOrder.Amount.ToString();

                if (!_isHidden)
                {
                    StartBubblePulsing();
                }
            }
            else
            {
                SetAlpha(_secondFoodImage, 1f);
                SetAlpha(_secondAmountText, 1f);

                if (!_isHidden)
                {
                    StartBubblePulsing();
                }
            }
        }

        private void StartBubblePulsing()
        {
            if (_foodImage == null) return;
            if (_foodMeshSettings != null && !_foodMeshSettings.EnableBubblePulsing) return;

            StopBubblePulsing();

            float baseScale = GetBaseScale();
            float pulseScaleFactor = _foodMeshSettings != null ? _foodMeshSettings.BubblePulseScale : 1.1f;
            float duration = _foodMeshSettings != null ? _foodMeshSettings.BubblePulseDuration : 0.7f;
            Color pulseColor = _foodMeshSettings != null ? _foodMeshSettings.BubblePulseColor : new Color(1.12f, 1.12f, 1.12f, 1f);

            _foodImage.transform.localScale = Vector3.one * baseScale;

            _glowTween = _foodImage.transform.DOScale(Vector3.one * (baseScale * pulseScaleFactor), duration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);

            _glowColorTween = _foodImage.DOColor(pulseColor, duration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void StopBubblePulsing()
        {
            if (_glowTween != null)
            {
                _glowTween.Kill();
                _glowTween = null;
            }

            if (_glowColorTween != null)
            {
                _glowColorTween.Kill();
                _glowColorTween = null;
            }

            if (_foodImage != null)
            {
                float baseScale = GetBaseScale();
                _foodImage.transform.localScale = Vector3.one * baseScale;
                _foodImage.color = new Color(1f, 1f, 1f, _foodImage.color.a);
            }
        }

        // The food sprite carries the full look; without an icon the image falls back to the food color.
        private void ApplyFoodSprite(Image image, FoodType type)
        {
            if (image == null) return;

            if (_foodMeshSettings != null && _foodMeshSettings.TryGetCustomerIcon(type, out var icon))
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
