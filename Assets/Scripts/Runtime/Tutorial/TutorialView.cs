using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RestaurantLoop.Runtime.Tutorial
{
    // Dumb display: TutorialController owns all sequencing and trigger logic.
    public class TutorialView : MonoBehaviour
    {
        private const float AdvanceClickGuardSeconds = 0.25f;

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
        private Vector2 _currentFingerOffset;
        private Vector2 _currentBlockerOffset;
        private float _stepShownTime;

        public event Action OnAdvance;

        private void Awake()
        {
            transform.SetAsLastSibling();
            ApplyTextStyle();

            if (_invisibleBlocker != null)
            {
                // Full-screen tap-to-advance for steps without a hover target.
                var invisButton = _invisibleBlocker.GetComponent<Button>();
                if (invisButton == null) invisButton = _invisibleBlocker.gameObject.AddComponent<Button>();
                invisButton.onClick.AddListener(HandleAdvanceClicked);
                StretchFullScreen(_invisibleBlocker);
            }
            else if (_advanceBlocker != null)
            {
                _advanceBlocker.onClick.AddListener(HandleAdvanceClicked);
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

        public void Show(
            string text,
            Vector3? screenPos,
            bool tapToAdvance,
            Vector2? customPanelPos = null,
            Vector2? fingerOffset = null,
            Vector2? blockerOffset = null,
            bool isPowerUpStep = false)
        {
            transform.SetAsLastSibling();
            _root.SetActive(true);
            _stepShownTime = Time.unscaledTime;
            ApplyTextStyle();
            if (_text != null) _text.text = text;

            ApplyPanelPosition(customPanelPos);

            DisableFingerRaycasts();
            DisableNonBlockerRaycasts();

            _currentFingerOffset = fingerOffset ?? Vector2.zero;
            _currentBlockerOffset = (isPowerUpStep && blockerOffset.HasValue) ? blockerOffset.Value : Vector2.zero;

            bool hasTarget = screenPos.HasValue;

            _finger.gameObject.SetActive(hasTarget);
            if (hasTarget)
            {
                ApplyRectPosition(_finger, screenPos.Value, _currentFingerOffset);
                ShowTargetBlocker(screenPos.Value);
            }
            else
            {
                ShowFullScreenBlocker();
            }
        }

        public void UpdateFingerPosition(Vector3 screenPos)
        {
            if (_finger != null)
            {
                if (!_finger.gameObject.activeSelf) _finger.gameObject.SetActive(true);
                ApplyRectPosition(_finger, screenPos, _currentFingerOffset);
            }
            if (_advanceBlocker != null && _advanceBlocker.gameObject.activeSelf)
            {
                var advanceRect = _advanceBlocker.transform as RectTransform;
                if (advanceRect != null)
                {
                    ApplyRectPosition(advanceRect, screenPos, _currentBlockerOffset);
                }
            }
        }

        public void Hide() => _root.SetActive(false);

        private void ApplyTextStyle()
        {
            if (_text == null) return;

            _text.enableAutoSizing = false;
            _text.fontSize = _fontSize;
            _text.enableWordWrapping = true;
            _text.textWrappingMode = TextWrappingModes.Normal;
        }

        private void ApplyPanelPosition(Vector2? customPanelPos)
        {
            if (_bubbleContainer != null)
            {
                if (_defaultBubblePos == Vector2.zero)
                {
                    _defaultBubblePos = _bubbleContainer.anchoredPosition;
                }
                _bubbleContainer.anchoredPosition = customPanelPos ?? _defaultBubblePos;
            }

            // A separate highlight image moves by the custom position as an offset.
            if (_highlightImage != null && _highlightImage != _bubbleContainer)
            {
                if (_defaultHighlightPos == Vector2.zero)
                {
                    _defaultHighlightPos = _highlightImage.anchoredPosition;
                }
                _highlightImage.anchoredPosition = customPanelPos.HasValue
                    ? _defaultHighlightPos + customPanelPos.Value
                    : _defaultHighlightPos;
            }
        }

        // Info step: a tap anywhere advances.
        private void ShowFullScreenBlocker()
        {
            if (_advanceBlocker != null)
            {
                _advanceBlocker.gameObject.SetActive(false);
            }

            if (_invisibleBlocker != null)
            {
                _invisibleBlocker.gameObject.SetActive(true);
                var invisImg = _invisibleBlocker.GetComponent<Graphic>();
                if (invisImg != null) invisImg.raycastTarget = true;
            }
            else if (_advanceBlocker != null)
            {
                // No invisible blocker assigned: the advance blocker becomes the full-screen one.
                _advanceBlocker.gameObject.SetActive(true);
                if (_advanceBlocker.image != null) _advanceBlocker.image.raycastTarget = true;
                if (_advanceBlocker.transform is RectTransform advanceRect)
                {
                    StretchFullScreen(advanceRect);
                }
            }
        }

        // Hover step: input passes through to the target; the advance blocker sits on it.
        private void ShowTargetBlocker(Vector3 screenPos)
        {
            if (_invisibleBlocker != null)
            {
                _invisibleBlocker.gameObject.SetActive(false);
            }

            if (_advanceBlocker != null)
            {
                _advanceBlocker.gameObject.SetActive(true);
                if (_advanceBlocker.transform is RectTransform advanceRect)
                {
                    ApplyRectPosition(advanceRect, screenPos, _currentBlockerOffset);
                }
            }
        }

        private void ApplyRectPosition(RectTransform rect, Vector3 screenOrWorldPos, Vector2 offset)
        {
            if (rect == null) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null)
            {
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(rect.parent as RectTransform ?? rect, screenOrWorldPos, canvas.worldCamera, out Vector3 worldPos))
                {
                    rect.position = worldPos + (Vector3)offset;
                    return;
                }
            }

            rect.position = screenOrWorldPos + (Vector3)offset;
        }

        private static void StretchFullScreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        private void HandleAdvanceClicked()
        {
            // Ignores a tap still in flight from the previous step.
            if (Time.unscaledTime - _stepShownTime < AdvanceClickGuardSeconds) return;
            OnAdvance?.Invoke();
        }

        // Only the blockers may catch clicks; bubble, highlight, text and root must let UI buttons through.
        private void DisableNonBlockerRaycasts()
        {
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
