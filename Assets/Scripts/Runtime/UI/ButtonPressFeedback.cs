using System.Collections;
using RestaurantLoop.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    // Elastic press/punch scale feedback for UI buttons and clickable 3D objects (with a collider).
    public class ButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        private const float PunchOutDuration = 0.1f;
        private const float SettleDuration = 0.15f;

        [Header("Press Scale Settings")]
        [SerializeField] private float pressedScaleMultiplier = 0.88f;
        [SerializeField] private float punchScaleMultiplier = 1.12f;
        [SerializeField] private float animationSpeed = 16f;

        [Header("Audio Settings")]
        [SerializeField] private AudioClip clickSound;

        private IAudioService _audioService;
        private Vector3 _originalScale;
        private Vector3 _targetScale;
        private Coroutine _activeScaleRoutine;

        [Inject]
        public void Construct(IAudioService audioService)
        {
            _audioService = audioService;
        }

        private void Awake()
        {
            _originalScale = transform.localScale;
            _targetScale = _originalScale;

            // The scale animation replaces Unity's color tint transition.
            var button = GetComponent<Button>();
            if (button != null && button.transition == Selectable.Transition.ColorTint)
                button.transition = Selectable.Transition.None;
        }

        private void OnEnable()
        {
            transform.localScale = _originalScale;
            _targetScale = _originalScale;
        }

        public void OnPointerDown(PointerEventData eventData) => SetTargetScale(_originalScale * pressedScaleMultiplier);

        public void OnPointerUp(PointerEventData eventData) => SetTargetScale(_originalScale);

        public void OnPointerClick(PointerEventData eventData) => StartPunchEffect();

        private void OnMouseDown() => SetTargetScale(_originalScale * pressedScaleMultiplier);

        private void OnMouseUp() => StartPunchEffect();

        private void SetTargetScale(Vector3 scale)
        {
            _targetScale = scale;
            if (_activeScaleRoutine == null && gameObject.activeInHierarchy)
                _activeScaleRoutine = StartCoroutine(AnimateScale());
        }

        private void StartPunchEffect()
        {
            if (clickSound != null) _audioService?.PlaySfx(clickSound);

            if (!gameObject.activeInHierarchy) return;

            if (_activeScaleRoutine != null) StopCoroutine(_activeScaleRoutine);
            _activeScaleRoutine = StartCoroutine(PunchRoutine());
        }

        private IEnumerator AnimateScale()
        {
            while (Vector3.Distance(transform.localScale, _targetScale) > 0.005f)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.unscaledDeltaTime * animationSpeed);
                yield return null;
            }

            transform.localScale = _targetScale;
            _activeScaleRoutine = null;
        }

        private IEnumerator PunchRoutine()
        {
            var punchScale = _originalScale * punchScaleMultiplier;
            float t = 0f;
            while (t < PunchOutDuration)
            {
                t += Time.unscaledDeltaTime;
                transform.localScale = Vector3.Lerp(transform.localScale, punchScale, t / PunchOutDuration);
                yield return null;
            }

            t = 0f;
            var startScale = transform.localScale;
            while (t < SettleDuration)
            {
                t += Time.unscaledDeltaTime;
                transform.localScale = Vector3.Lerp(startScale, _originalScale, t / SettleDuration);
                yield return null;
            }

            transform.localScale = _originalScale;
            _activeScaleRoutine = null;
        }
    }
}
