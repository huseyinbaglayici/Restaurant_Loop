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

        public event Action OnAdvance;

        private void Awake()
        {
            _advanceBlocker.onClick.AddListener(() => OnAdvance?.Invoke());
            _root.SetActive(false);
        }

        public void Show(string text, Vector3? screenPos, bool tapToAdvance)
        {
            _root.SetActive(true);
            _text.text = text;

            // Steps driven by a real gameplay action (queue click, power-up button) must stop the
            // blocker from eating the click — otherwise the power-up Button's own EventSystem
            // raycast never sees it, and a plain queue click would double-fire the advance.
            _advanceBlocker.image.raycastTarget = tapToAdvance;

            _finger.gameObject.SetActive(screenPos.HasValue);
            if (screenPos.HasValue) _finger.position = screenPos.Value;
        }

        public void Hide() => _root.SetActive(false);
    }
}
