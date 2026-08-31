using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RestaurantLoop.Runtime.Tutorial
{
    // Dumb display — TutorialController owns all sequencing/trigger logic.
    public class TutorialView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private RectTransform _finger;
        [SerializeField] private Button _advanceBlocker;
        [SerializeField] private RectTransform _invisibleBlocker;
        [SerializeField] private RectTransform _bubbleContainer;
        [SerializeField] private RectTransform _highlightImage;
        [SerializeField] private float _fontSize = 50f;

        private Vector2 _defaultBubblePos;
        private Vector2 _defaultHighlightPos;
        private Vector2 _defaultBlockerPos;
        private Vector2 _currentFingerOffset;

        public event Action OnAdvance;

        private void Awake()
        {
            transform.SetAsLastSibling();
            if (_text != null)
            {
                _text.enableAutoSizing = false;
                _text.fontSize = _fontSize;
                _text.enableWordWrapping = true;
                _text.textWrappingMode = TextWrappingModes.Normal;
            }
            if (_invisibleBlocker != null)
            {
                // Invisible blocker is the full-screen tap-to-advance trigger for SceneLoad steps
                var invisButton = _invisibleBlocker.GetComponent<Button>();
                if (invisButton != null)
                {
                    invisButton.onClick.AddListener(() => OnAdvance?.Invoke());
                }
                else
                {
                    var btn = _invisibleBlocker.gameObject.AddComponent<Button>();
                    btn.onClick.AddListener(() => OnAdvance?.Invoke());
                }

                _invisibleBlocker.anchorMin = Vector2.zero;
                _invisibleBlocker.anchorMax = Vector2.one;
                _invisibleBlocker.sizeDelta = Vector2.zero;
                _invisibleBlocker.anchoredPosition = Vector2.zero;
            }
            else if (_advanceBlocker != null)
            {
                _advanceBlocker.onClick.AddListener(() => OnAdvance?.Invoke());
            }

            if (_advanceBlocker != null)
            {
                var advanceRect = _advanceBlocker.transform as RectTransform;
                if (advanceRect != null)
                {
                    _defaultBlockerPos = advanceRect.anchoredPosition;
                }
            }

            if (_bubbleContainer == null && _text != null && _text.transform.parent != null)
            {
                _bubbleContainer = _text.transform.parent as RectTransform;
            }

            if (_bubbleContainer != null)
            {
                _defaultBubblePos = _bubbleContainer.anchoredPosition;
            }

            DisableFingerRaycasts();
            DisableNonBlockerRaycasts();
            _root.SetActive(false);
        }

        private void DisableNonBlockerRaycasts()
        {
            // Ensure only _advanceBlocker can block clicks when tapToAdvance is true.
            // Everything else (bubble background image, root image, finger) should not block UI button clicks.
            if (_bubbleContainer != null)
            {
                var bubbleImg = _bubbleContainer.GetComponent<Graphic>();
                if (bubbleImg != null) bubbleImg.raycastTarget = false;
            }
            if (_highlightImage != null)
            {
                var highlightImg = _highlightImage.GetComponent<Graphic>();
                if (highlightImg != null) highlightImg.raycastTarget = false;
            }
            if (_text != null)
            {
                _text.raycastTarget = false;
            }
            if (_root != null)
            {
                var rootImg = _root.GetComponent<Graphic>();
                if (rootImg != null) rootImg.raycastTarget = false;
            }
        }

        public void Show(
            string text, 
            Vector3? screenPos, 
            bool tapToAdvance, 
            Vector2? customPanelPos = null,
            Vector2? fingerOffset = null,
            Vector2? blockerOffset = null)
        {
            transform.SetAsLastSibling();
            _root.SetActive(true);
            if (_text != null)
            {
                _text.enableAutoSizing = false;
                _text.fontSize = _fontSize;
                _text.enableWordWrapping = true;
                _text.textWrappingMode = TextWrappingModes.Normal;
                _text.text = text;
            }

            // Apply custom panel position to the bubble container if supplied (or keep default)
            if (_bubbleContainer != null)
            {
                if (_defaultBubblePos == Vector2.zero)
                {
                    _defaultBubblePos = _bubbleContainer.anchoredPosition;
                }
                Vector2 targetPos = customPanelPos.HasValue ? customPanelPos.Value : _defaultBubblePos;
                _bubbleContainer.anchoredPosition = targetPos;
            }

            // If a custom highlight image is specifically assigned (separate from bubble container), apply offset
            if (_highlightImage != null && _highlightImage != _bubbleContainer)
            {
                if (_defaultHighlightPos == Vector2.zero)
                {
                    _defaultHighlightPos = _highlightImage.anchoredPosition;
                }
                Vector2 targetHighlightPos = customPanelPos.HasValue ? (_defaultHighlightPos + customPanelPos.Value) : _defaultHighlightPos;
                _highlightImage.anchoredPosition = targetHighlightPos;
            }

            DisableFingerRaycasts();
            DisableNonBlockerRaycasts();

            _currentFingerOffset = fingerOffset ?? Vector2.zero;
            bool hasTarget = screenPos.HasValue;
            _finger.gameObject.SetActive(hasTarget);
            if (hasTarget)
            {
                ApplyFingerPosition(screenPos.Value);
            }

            // 1) SceneLoad / Info step: _invisibleBlocker is active (fullscreen tap-to-advance)
            if (_invisibleBlocker != null)
            {
                _invisibleBlocker.gameObject.SetActive(tapToAdvance);
                var invisImg = _invisibleBlocker.GetComponent<Graphic>();
                if (invisImg != null)
                {
                    invisImg.raycastTarget = tapToAdvance;
                }
            }

            // 2) Hover step (with finger): _advanceBlocker is active only here, with custom offset from PowerUpSettings
            if (_advanceBlocker != null)
            {
                _advanceBlocker.gameObject.SetActive(hasTarget);
                var advanceRect = _advanceBlocker.transform as RectTransform;
                if (advanceRect != null)
                {
                    Vector2 targetBlockerPos = blockerOffset.HasValue ? (_defaultBlockerPos + blockerOffset.Value) : _defaultBlockerPos;
                    advanceRect.anchoredPosition = targetBlockerPos;
                }
            }
        }

        public void UpdateFingerPosition(Vector3 screenPos)
        {
            if (_finger != null)
            {
                if (!_finger.gameObject.activeSelf) _finger.gameObject.SetActive(true);
                ApplyFingerPosition(screenPos);
            }
        }

        private void ApplyFingerPosition(Vector3 screenOrWorldPos)
        {
            if (_finger == null) return;

            // Check if parent canvas is ScreenSpaceOverlay or ScreenSpaceCamera
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null)
            {
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_finger.parent as RectTransform ?? _finger, screenOrWorldPos, canvas.worldCamera, out Vector3 worldPos))
                {
                    _finger.position = worldPos + (Vector3)_currentFingerOffset;
                    return;
                }
            }

            _finger.position = screenOrWorldPos + (Vector3)_currentFingerOffset;
        }

        public void Hide() => _root.SetActive(false);

        private void DisableFingerRaycasts()
        {
            if (_finger == null) return;
            var graphics = _finger.GetComponentsInChildren<Graphic>(true);
            foreach (var g in graphics)
            {
                g.raycastTarget = false;
            }
        }
    }
}

