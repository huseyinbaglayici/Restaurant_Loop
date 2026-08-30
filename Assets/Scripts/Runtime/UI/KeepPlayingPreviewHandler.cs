using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RestaurantLoop.Runtime.UI
{
    public class KeepPlayingPreviewHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private CanvasGroup _previewTarget;
        [SerializeField, Range(0f, 1f)] private float _previewAlpha = 0f;
        [SerializeField] private float _fadeOutDuration = 0.35f;
        [SerializeField] private float _fadeInDuration = 0.2f;

        private Coroutine _fadeRoutine;

        public void OnPointerDown(PointerEventData eventData)
        {
            StartFade(_previewAlpha, _fadeOutDuration);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            StartFade(1f, _fadeInDuration);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            StartFade(1f, _fadeInDuration);
        }

        private void OnDisable()
        {
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            if (_previewTarget != null)
            {
                _previewTarget.alpha = 1f;
            }
        }

        private void StartFade(float targetAlpha, float duration)
        {
            if (_previewTarget == null) return;

            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
            }

            _fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, duration));
        }

        private IEnumerator FadeRoutine(float targetAlpha, float duration)
        {
            float startAlpha = _previewTarget.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _previewTarget.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }

            _previewTarget.alpha = targetAlpha;
        }
    }
}
