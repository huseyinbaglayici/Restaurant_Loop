using System;
using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Customer baked into a Vertex Animation Texture (VATBaker): two renderers (one per shader) share one
    // clip layout and playhead. The idle phase is randomized in OnEnable so every pooled respawn differs.
    // Sole owner of this transform's punch/serve tweens; every entry point kills and resets first.
    public class VATCustomerVisual : MonoBehaviour
    {
        private static readonly int VATFrameAId = Shader.PropertyToID("_VATFrameA");
        private static readonly int VATFrameBId = Shader.PropertyToID("_VATFrameB");
        private static readonly int VATBlendId = Shader.PropertyToID("_VATBlend");

        // Punches retriggered while one is still running grow (up to the max) instead of restarting, so a
        // burst of near-simultaneous bites reads as one bigger hit.
        private const float ConsumeComboStep = 0.35f;
        private const float ConsumeComboMax = 2.2f;

        [Header("Body (skin, eyes)")]
        [SerializeField] private MeshRenderer _bodyRenderer;

        [Header("Outfit (clothes, eyebrows)")]
        [SerializeField] private MeshRenderer _outfitRenderer;

        [Header("Clips (shared by both renderers)")]
        [SerializeField] private VATClipSet _clips;

        private MaterialPropertyBlock _block;
        private float _time;
        private float _speed = 1f;

        private Vector3 _restLocalScale;
        private Vector3 _restLocalPosition;
        private Quaternion _restLocalRotation;
        private Tween _scaleTween;
        private float _consumeComboScale = 1f;
        private bool _isExiting;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _restLocalScale = transform.localScale;
            _restLocalPosition = transform.localPosition;
            _restLocalRotation = transform.localRotation;
        }

        private void OnEnable()
        {
            InitializeIdleAnimation();
        }

        private void OnDisable()
        {
            // A pooled respawn must never inherit a mid-tween scale/position from a previous customer.
            _isExiting = false;
            ResetTransform();
        }

        [ContextMenu("Initialize Idle (Random Offset)")]
        public void InitializeIdleAnimation()
        {
            if (_clips == null || !_clips.Idle.IsValid) return;

            _speed = UnityEngine.Random.Range(0.95f, 1.05f);
            _time = UnityEngine.Random.Range(0f, _clips.Idle.Duration(_clips.SampleFps));
            ApplyFrame();
        }

        private void Update()
        {
            if (_clips == null || !_clips.Idle.IsValid) return;

            _time += Time.deltaTime * _speed;
            float duration = _clips.Idle.Duration(_clips.SampleFps);
            if (duration > 0f) _time %= duration;

            ApplyFrame();
        }

        private void ApplyFrame()
        {
            if (_clips == null || !_clips.Idle.IsValid) return;

            var entry = _clips.Idle;
            int frame = Mathf.Clamp(Mathf.RoundToInt(_time * _clips.SampleFps), 0, entry.FrameCount - 1);
            float frameIndex = entry.StartFrame + frame;

            ApplyFrameToRenderer(_bodyRenderer, frameIndex);
            ApplyFrameToRenderer(_outfitRenderer, frameIndex);
        }

        private void ApplyFrameToRenderer(MeshRenderer renderer, float frameIndex)
        {
            if (renderer == null) return;

            renderer.GetPropertyBlock(_block);
            _block.SetFloat(VATFrameAId, frameIndex);
            _block.SetFloat(VATFrameBId, frameIndex);
            _block.SetFloat(VATBlendId, 0f);
            renderer.SetPropertyBlock(_block);
        }

        public void PlayConsumePunch(Vector3 strength, float duration, int vibrato, float elasticity)
        {
            if (_isExiting) return;

            bool punchAlreadyRunning = _scaleTween != null && _scaleTween.active && _scaleTween.IsPlaying();
            _consumeComboScale = punchAlreadyRunning
                ? Mathf.Min(_consumeComboScale + ConsumeComboStep, ConsumeComboMax)
                : 1f;

            _scaleTween?.Kill();
            transform.localScale = _restLocalScale;

            _scaleTween = transform.DOPunchScale(strength * _consumeComboScale, duration, vibrato, elasticity)
                .OnComplete(() => transform.localScale = _restLocalScale);
        }

        // Second order left: small punch. Final order: pop and float-up shrink, with onComplete (the
        // despawn) firing exactly at zero scale so the hide and the vanish land on the same frame.
        public void PlayServeReaction(bool hasSecondOrderRemaining, Action onComplete)
        {
            if (_isExiting) return;
            if (!hasSecondOrderRemaining) _isExiting = true;

            ResetTransform();

            var seq = DOTween.Sequence();

            if (hasSecondOrderRemaining)
            {
                seq.Append(transform.DOPunchScale(new Vector3(0.18f, 0.18f, 0.18f), 0.3f, 6, 0.6f));
            }
            else
            {
                // Squash, pop + hop, landing bounce, then float up and shrink with a spin. All scales are
                // relative to the rest scale.
                Vector3 rest = _restLocalScale;
                float baseY = _restLocalPosition.y;

                seq.Append(transform.DOScale(new Vector3(rest.x * 1.2f, rest.y * 0.8f, rest.z * 1.2f), 0.08f).SetEase(Ease.OutQuad));

                seq.Append(transform.DOScale(new Vector3(rest.x * 0.85f, rest.y * 1.25f, rest.z * 0.85f), 0.12f).SetEase(Ease.OutBack, 3f));
                seq.Join(transform.DOLocalMoveY(baseY + 0.16f, 0.12f).SetEase(Ease.OutQuad));

                seq.Append(transform.DOScale(rest, 0.1f).SetEase(Ease.OutBack, 2f));

                seq.Append(transform.DOScale(Vector3.zero, 0.22f).SetEase(Ease.InBack, 1.2f));
                seq.Join(transform.DOLocalMoveY(baseY + 0.55f, 0.22f).SetEase(Ease.OutQuad));
                seq.Join(transform.DOLocalRotate(new Vector3(0f, 25f, 0f), 0.22f, RotateMode.LocalAxisAdd).SetEase(Ease.OutQuad));
            }

            seq.OnComplete(() =>
            {
                _isExiting = false;
                transform.localScale = _restLocalScale;
                transform.localPosition = _restLocalPosition;
                transform.localRotation = _restLocalRotation;
                onComplete?.Invoke();
            });
            // No SetLink: OnDisable already resets on despawn, and SetLink can kill tweens early on pooled objects.
            _scaleTween = seq;
        }

        private void ResetTransform()
        {
            _scaleTween?.Kill();
            transform.localScale = _restLocalScale;
            transform.localPosition = _restLocalPosition;
            transform.localRotation = _restLocalRotation;
            _consumeComboScale = 1f;
        }
    }
}
