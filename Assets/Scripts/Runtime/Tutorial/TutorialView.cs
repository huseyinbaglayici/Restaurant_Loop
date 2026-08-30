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

            // Eğer hover seçilmemişse (sadece bilgilendirme metni) blocker görünür ve ekrana basarak ilerlenir.
            // Eğer bir hedef seçilmişse (hoverKind != None) blocker tamamen gizlenir, sadece el ikonu (finger) gözükür.
            if (_advanceBlocker != null)
            {
                _advanceBlocker.gameObject.SetActive(tapToAdvance);
                if (_advanceBlocker.image != null)
                {
                    _advanceBlocker.image.raycastTarget = tapToAdvance;
                }
            }

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
    }
}
