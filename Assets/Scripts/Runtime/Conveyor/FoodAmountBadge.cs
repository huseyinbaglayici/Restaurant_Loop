using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using TMPro;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // World-space amount text hovering above the 3D stack (belt / exit stack). Created lazily under the
    // food item the first time it is needed. Owned by FoodItemController.
    public class FoodAmountBadge
    {
        private readonly Transform _owner;
        private readonly FoodStackBuilder _stackBuilder;
        private readonly FoodMeshSettings _foodMeshSettings;
        private readonly CameraController _cameraController;

        private GameObject _badge;
        private TextMeshPro _text;

        public FoodAmountBadge(
            Transform owner,
            FoodStackBuilder stackBuilder,
            FoodMeshSettings foodMeshSettings,
            CameraController cameraController)
        {
            _owner = owner;
            _stackBuilder = stackBuilder;
            _foodMeshSettings = foodMeshSettings;
            _cameraController = cameraController;
        }

        // Only toggles an already created badge — never creates one.
        public void SetVisible(bool visible)
        {
            if (_badge != null)
            {
                _badge.SetActive(visible);
            }
        }

        public void Hide()
        {
            if (_badge != null) _badge.SetActive(false);
        }

        public void Show(FoodStackData stack, FoodLocation location)
        {
            if (stack.Amount <= 0 && stack.LinkId == 0)
            {
                Hide();
                return;
            }

            ShowAmount(stack.Amount, location);
        }

        public void Pop(FoodStackData stack, FoodLocation location)
        {
            if (stack.Amount <= 0 && stack.LinkId == 0) return;

            ShowAmount(stack.Amount, location);

            _badge.transform.DOKill(true);
            _badge.transform.localScale = Vector3.zero;
            _badge.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);
        }

        public void AlignWithCamera(FoodLocation location)
        {
            if (_badge == null || !_badge.activeSelf) return;

            var cam = Camera.main;
            if (cam == null && _cameraController != null && _cameraController.Camera != null)
            {
                cam = _cameraController.Camera;
            }
            if (cam == null) return;

            // World-space facing, so the food's own rotation doesn't affect it.
            _badge.transform.rotation = cam.transform.rotation;

            // Cancels the parent's scale so the text size stays the same for every food model.
            Vector3 lossy = _owner.lossyScale;
            _badge.transform.localScale = new Vector3(
                lossy.x > 0.001f ? 1f / lossy.x : 1f,
                lossy.y > 0.001f ? 1f / lossy.y : 1f,
                lossy.z > 0.001f ? 1f / lossy.z : 1f);

            float extraOffset = _foodMeshSettings != null ? _foodMeshSettings.ConveyorBadgeHeightOffset : 0.85f;
            Vector3 topPosition = _stackBuilder.TopUnitWorldPosition;

            // Hovers just above and slightly right of the stack's highest rendered point.
            Vector3 worldOffset = Vector3.up * (extraOffset + 0.35f) + cam.transform.right * 0.35f;

            if (location == FoodLocation.FoodStack)
            {
                worldOffset.z += 1f;
                worldOffset.y -= 2.2f;
            }

            _badge.transform.position = topPosition + worldOffset;
        }

        private void ShowAmount(int amount, FoodLocation location)
        {
            EnsureBadge();
            _badge.SetActive(true);
            if (_foodMeshSettings != null && _foodMeshSettings.StackAmountFont != null && _text.font != _foodMeshSettings.StackAmountFont)
            {
                _text.font = _foodMeshSettings.StackAmountFont;
            }
            _text.text = amount.ToString();

            AlignWithCamera(location);
        }

        private void EnsureBadge()
        {
            if (_badge != null) return;

            _badge = new GameObject("Conveyor3D_AmountBadge");
            _badge.transform.SetParent(_owner, false);

            var textObj = new GameObject("AmountText");
            textObj.transform.SetParent(_badge.transform, false);
            textObj.transform.localPosition = Vector3.zero;
            textObj.transform.localRotation = Quaternion.identity;
            textObj.transform.localScale = Vector3.one;

            var rt = textObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(2f, 2f);

            _text = textObj.AddComponent<TextMeshPro>();
            if (_foodMeshSettings != null && _foodMeshSettings.StackAmountFont != null)
            {
                _text.font = _foodMeshSettings.StackAmountFont;
            }
            _text.alignment = TextAlignmentOptions.Center;
            _text.fontSize = 5.5f;
            _text.color = Color.white;
            _text.fontStyle = FontStyles.Bold;
            _text.sortingOrder = 2000;
            _text.isOverlay = true; // draws over 3D geometry

            var meshRenderer = textObj.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = 2000;
            }
        }
    }
}
