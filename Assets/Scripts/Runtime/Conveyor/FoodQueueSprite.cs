using RestaurantLoop.Runtime.Data;
using TMPro;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Flat 2D icon + amount text shown instead of the 3D stack while an item waits in the queue.
    // Created lazily under the food item the first time it is needed. Owned by FoodItemController.
    public class FoodQueueSprite
    {
        private readonly Transform _owner;
        private readonly FoodMeshSettings _foodMeshSettings;
        private readonly FoodPlacementSettings _placement;
        private readonly TableSettings _tableSettings;
        private readonly CameraController _cameraController;

        private GameObject _holder;
        private SpriteRenderer _spriteRenderer;
        private TextMeshPro _amountText;

        public FoodQueueSprite(
            Transform owner,
            FoodMeshSettings foodMeshSettings,
            FoodPlacementSettings placement,
            TableSettings tableSettings,
            CameraController cameraController)
        {
            _owner = owner;
            _foodMeshSettings = foodMeshSettings;
            _placement = placement;
            _tableSettings = tableSettings;
            _cameraController = cameraController;
        }

        public bool Exists => _holder != null;

        public Vector3 Position => _holder.transform.position;

        public void Show(FoodStackData stack)
        {
            EnsureHolder();

            _holder.SetActive(true);

            if (_foodMeshSettings != null && _foodMeshSettings.TryGetQueueIcon(stack.Type, out var icon))
            {
                _spriteRenderer.sprite = icon;
            }
            else
            {
                _spriteRenderer.sprite = null;
            }

            if (_amountText != null)
            {
                if (_foodMeshSettings != null && _foodMeshSettings.StackAmountFont != null && _amountText.font != _foodMeshSettings.StackAmountFont)
                {
                    _amountText.font = _foodMeshSettings.StackAmountFont;
                }
                _amountText.text = stack.Amount > 0 ? stack.Amount.ToString() : "";
            }

            var tableSettings = _tableSettings;
            _holder.transform.localScale = Vector3.one * GetScaleMultiplier();
            _holder.transform.localPosition = tableSettings != null ? tableSettings.Queue2DOffset : new Vector3(0f, 0.5f, 0f);

            AlignWithCamera();
        }

        public void Hide()
        {
            if (_holder != null) _holder.SetActive(false);
        }

        // Per-frame: keep facing the camera and follow live icon-scale / zoom changes.
        public void Refresh()
        {
            AlignWithCamera();

            if (_holder != null)
            {
                _holder.transform.localScale = Vector3.one * GetScaleMultiplier();
            }
        }

        private void EnsureHolder()
        {
            if (_holder != null) return;

            _holder = new GameObject("Queue2D_Visual");
            _holder.transform.SetParent(_owner, false);

            _spriteRenderer = _holder.AddComponent<SpriteRenderer>();
            _spriteRenderer.sortingOrder = 10;

            var textObj = new GameObject("AmountText");
            textObj.transform.SetParent(_holder.transform, false);
            // Position at top-right corner of the 2D sprite
            textObj.transform.localPosition = new Vector3(6f, 6f, -0.2f);
            textObj.transform.localRotation = Quaternion.identity;
            textObj.transform.localScale = Vector3.one * 12f;

            var rt = textObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(3f, 3f);

            _amountText = textObj.AddComponent<TextMeshPro>();
            var meshSettings = _foodMeshSettings;
            if (meshSettings != null && meshSettings.StackAmountFont != null)
            {
                _amountText.font = meshSettings.StackAmountFont;
            }
            _amountText.alignment = TextAlignmentOptions.Center;
            _amountText.fontSize = 6.5f;
            _amountText.color = Color.white;
            _amountText.fontStyle = FontStyles.Bold;
            _amountText.sortingOrder = 50; // Higher sorting order so it always appears above the sprite

            var meshRenderer = textObj.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = 50;
            }
        }

        private float GetScaleMultiplier()
        {
            var tableSettings = _tableSettings;
            if (tableSettings == null) return 1f;

            float baseIconScale = tableSettings.Queue2DIconScale;
            float globalQueueScale = _placement != null ? _placement.GlobalQueueScale : 1f;
            float zoomMultiplier = 1f;

            if (tableSettings.ScaleWithCamera)
            {
                var camCtrl = _cameraController;
                float zoom = camCtrl != null ? camCtrl.CameraZoomFactor : 1f;

                // Fallback to Main Camera orthographic ratio if CameraController wasn't resolved yet
                if (camCtrl == null)
                {
                    var cam = Camera.main;
                    if (cam != null && cam.orthographic)
                    {
                        // Standard base ortho size is around 8.5
                        float baseOrtho = 8.5f;
                        if (cam.orthographicSize > baseOrtho)
                        {
                            zoom = cam.orthographicSize / baseOrtho;
                        }
                    }
                }

                float multiplier = tableSettings.FoodQueueScaleMultiplier > 0f
                    ? tableSettings.FoodQueueScaleMultiplier
                    : tableSettings.TableScaleMultiplier;
                zoomMultiplier = 1f + (zoom - 1f) * multiplier;
            }

            return baseIconScale * globalQueueScale * zoomMultiplier;
        }

        private void AlignWithCamera()
        {
            if (_holder == null || !_holder.activeSelf) return;

            var cam = Camera.main;
            if (cam != null)
            {
                _holder.transform.rotation = cam.transform.rotation;
            }
        }
    }
}
