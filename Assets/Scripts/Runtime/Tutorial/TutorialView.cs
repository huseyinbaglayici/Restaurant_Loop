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
        [SerializeField] private RectTransform _bubbleContainer;
        [SerializeField] private float _fontSize = 50f;

        private Vector2 _defaultBubblePos;

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
            if (_advanceBlocker != null)
            {
                _advanceBlocker.onClick.AddListener(() => OnAdvance?.Invoke());
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
            _root.SetActive(false);
        }

        public void Show(string text, Vector3? screenPos, bool tapToAdvance, Vector2? customPanelPos = null)
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

            if (_bubbleContainer == null && _text != null && _text.transform.parent != null)
            {
                _bubbleContainer = _text.transform.parent as RectTransform;
                if (_bubbleContainer != null && _defaultBubblePos == Vector2.zero)
                {
                    _defaultBubblePos = _bubbleContainer.anchoredPosition;
                }
            }

            // Apply custom panel position (e.g. from PowerUpSettings for powerup tutorial), or revert to original default
            if (_bubbleContainer != null)
            {
                Vector2 targetPos = customPanelPos.HasValue ? customPanelPos.Value : _defaultBubblePos;
                _bubbleContainer.anchoredPosition = targetPos;
            }

            // Blocker is only active for tap-anywhere info steps, never for targeted interactions
            if (_advanceBlocker != null)
            {
                _advanceBlocker.gameObject.SetActive(tapToAdvance);
                if (_advanceBlocker.image != null)
                {
                    _advanceBlocker.image.raycastTarget = tapToAdvance;
                }
            }

            DisableFingerRaycasts();

            _finger.gameObject.SetActive(screenPos.HasValue);
            if (screenPos.HasValue) _finger.position = screenPos.Value;
        }

        public void UpdateFingerPosition(Vector3 screenPos)
        {
            if (_finger != null)
            {
                if (!_finger.gameObject.activeSelf) _finger.gameObject.SetActive(true);
                _finger.position = screenPos;
            }
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

