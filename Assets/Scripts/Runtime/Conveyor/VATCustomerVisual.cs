using System;
using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // The customer rig is baked into a Vertex Animation
    // Texture (see VATBaker) instead of skinned + Animator Controller. Customer.prefab uses two
    // different shaders across its parts (body/eyes vs pants/shoes/tshirt/Eyebrow), so the bake is
    // split into two renderers sharing one VAT texture/clip layout — both driven by the same
    // playhead so they stay in sync. Single fixed look — no hair, no per-instance variation. Only
    // the idle phase/speed is randomized per spawn, driven from OnEnable rather than Start().
    // Start() only ever fires once per pooled instance's lifetime, which would have re-synced idle
    // phase on every respawn after the first; OnEnable fires on every SetActive(true), matching
    // CustomerController.OnSpawned.
    //
    // Sole owner of this transform's scale/position tweens (consume-punch AND serve-reaction both
    // live here, not split across CustomerController + this class) — that split previously let both
    // fire near-simultaneously and fight over the same localScale, leaving some pooled customers
    // stuck at the wrong size. Every entry point below kills+resets before starting its own tween.
    public class VATCustomerVisual : MonoBehaviour
    {
        private static readonly int VATFrameAId = Shader.PropertyToID("_VATFrameA");
        private static readonly int VATFrameBId = Shader.PropertyToID("_VATFrameB");
        private static readonly int VATBlendId = Shader.PropertyToID("_VATBlend");

        [Header("Body (skin/eyes — RestaurantSoftMatte)")]
        [SerializeField] private MeshRenderer _bodyRenderer;

        [Header("Outfit (pants/shoes/tshirt/Eyebrow — Soft_Matte_2)")]
        [SerializeField] private MeshRenderer _outfitRenderer;

        [Header("Shared clip layout (one texture, both renderers)")]
        [SerializeField] private VATClipSet _clips;

        private MaterialPropertyBlock _block;
        private float _time;
        private float _speed = 1f;

        private Vector3 _restLocalScale;
        private Vector3 _restLocalPosition;
        private Quaternion _restLocalRotation;
        private Tween _scaleTween;

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

        // Very small scale punch each time a food unit lands on this customer. Multi-item orders
        // can hand off several units on the same flight duration, so their arrivals land within a
        // frame or two of each other — killing+restarting a fresh punch at the same base strength
        // for each one meant the earlier hits got cancelled before ever rendering a frame, so only
        // the last of a burst was ever visible. Retriggering while a punch is still running instead
        // escalates it (bigger combo pop) rather than resetting it, so a burst of near-simultaneous
        // bites always reads as one satisfying hit instead of N silent ones.
        private const float ConsumeComboStep = 0.35f;
        private const float ConsumeComboMax = 2.2f;
        private float _consumeComboScale = 1f;
        private bool _isExiting;

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

        // hasSecondOrderRemaining == true: small punch, customer stays and returns to idle.
        // hasSecondOrderRemaining == false: pop + float-up shrink, then onComplete right as the
        // character hits zero scale — the caller despawns from that same callback, so the pool-hide
        // and the visual vanish land on the same frame instead of a scale pop-then-snap.
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
                // Anticipation squash, elastic overshoot pop + hop, a snappy landing bounce, then
                // float up while shrinking away with a little spin — classic juicy mobile "collected!"
                // exit. Punch amounts are relative multipliers of the rest scale, not absolute
                // values, so the character's real size is never lost.
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
            // No SetLink here — OnDisable already kills+resets deterministically on despawn, and
            // DOTween's own GameObject-state tracking (used by SetLink) has a known history of
            // occasionally killing a tween early on pooled/reactivated objects, unrelated to actual
            // despawn — exactly the "plays fine, sometimes just doesn't" symptom this was causing.
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
