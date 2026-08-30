using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Replaces TestCustomerController now that the customer rig is baked into a Vertex Animation
    // Texture (see VATBaker) instead of skinned + Animator Controller — same jobs (idle desync,
    // hair randomize/tint), driven from OnEnable rather than Start(). Start() only ever fires once
    // per pooled instance's lifetime, which would have re-synced idle phase on every respawn after
    // the first; OnEnable fires on every SetActive(true), matching CustomerController.OnSpawned.
    public class VATCustomerVisual : MonoBehaviour
    {
        private static readonly int VATFrameAId = Shader.PropertyToID("_VATFrameA");
        private static readonly int VATFrameBId = Shader.PropertyToID("_VATFrameB");
        private static readonly int VATBlendId = Shader.PropertyToID("_VATBlend");
        private static readonly int InstanceColorId = Shader.PropertyToID("_InstanceColor");

        [Header("Body (merged skinned parts + Eyebrow)")]
        [SerializeField] private MeshRenderer _bodyRenderer;
        [SerializeField] private VATClipSet _bodyClips;

        [Header("Hair Variants (exactly one active at a time)")]
        [SerializeField] private MeshRenderer[] _hairRenderers;
        [SerializeField] private VATClipSet[] _hairClips;

        [Header("Hair Colors")]
        [SerializeField] private Color[] _hairColors =
        {
            new Color(0x1D / 255f, 0x14 / 255f, 0x0E / 255f, 1f),
            new Color(0x56 / 255f, 0x3C / 255f, 0x2B / 255f, 1f),
            new Color(0xDA / 255f, 0x6B / 255f, 0x2D / 255f, 1f),
            new Color(0xFD / 255f, 0xBA / 255f, 0x50 / 255f, 1f)
        };

        private MaterialPropertyBlock _block;
        private int _activeHairIndex = -1;
        private float _time;
        private float _speed = 1f;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            SetInstanceColor(_bodyRenderer, Color.white);
        }

        private void OnEnable()
        {
            RandomizeHair();
            InitializeIdleAnimation();
        }

        [ContextMenu("Randomize Hair")]
        public void RandomizeHair()
        {
            if (_hairRenderers == null || _hairRenderers.Length == 0) return;

            if (_activeHairIndex >= 0 && _activeHairIndex < _hairRenderers.Length && _hairRenderers[_activeHairIndex] != null)
                _hairRenderers[_activeHairIndex].gameObject.SetActive(false);

            _activeHairIndex = Random.Range(0, _hairRenderers.Length);
            var renderer = _hairRenderers[_activeHairIndex];
            if (renderer == null) return;

            renderer.gameObject.SetActive(true);

            Color tint = (_hairColors != null && _hairColors.Length > 0)
                ? _hairColors[Random.Range(0, _hairColors.Length)]
                : Color.white;
            SetInstanceColor(renderer, tint);
        }

        [ContextMenu("Initialize Idle (Random Offset)")]
        public void InitializeIdleAnimation()
        {
            if (_bodyClips == null || !_bodyClips.Idle.IsValid) return;

            _speed = Random.Range(0.95f, 1.05f);
            _time = Random.Range(0f, _bodyClips.Idle.Duration(_bodyClips.SampleFps));
            ApplyFrame();
        }

        private void Update()
        {
            if (_bodyClips == null || !_bodyClips.Idle.IsValid) return;

            _time += Time.deltaTime * _speed;
            float duration = _bodyClips.Idle.Duration(_bodyClips.SampleFps);
            if (duration > 0f) _time %= duration;

            ApplyFrame();
        }

        private void ApplyFrame()
        {
            ApplyFrameToRenderer(_bodyRenderer, _bodyClips);

            if (_activeHairIndex >= 0 && _hairRenderers != null && _activeHairIndex < _hairRenderers.Length)
            {
                var hairClips = (_hairClips != null && _activeHairIndex < _hairClips.Length) ? _hairClips[_activeHairIndex] : null;
                ApplyFrameToRenderer(_hairRenderers[_activeHairIndex], hairClips);
            }
        }

        private void ApplyFrameToRenderer(MeshRenderer renderer, VATClipSet clips)
        {
            if (renderer == null || clips == null || !clips.Idle.IsValid) return;

            var entry = clips.Idle;
            int frame = Mathf.Clamp(Mathf.RoundToInt(_time * clips.SampleFps), 0, entry.FrameCount - 1);
            float frameIndex = entry.StartFrame + frame;

            renderer.GetPropertyBlock(_block);
            _block.SetFloat(VATFrameAId, frameIndex);
            _block.SetFloat(VATFrameBId, frameIndex);
            _block.SetFloat(VATBlendId, 0f);
            renderer.SetPropertyBlock(_block);
        }

        private void SetInstanceColor(MeshRenderer renderer, Color color)
        {
            if (renderer == null) return;

            renderer.GetPropertyBlock(_block);
            _block.SetColor(InstanceColorId, color);
            renderer.SetPropertyBlock(_block);
        }

        // Reaction hooks — no-ops for now. happy_idle_beko.controller has no reaction clips today
        // (TestCustomerController's happy/first-order/fail triggers already reference parameters
        // that don't exist on it), so this preserves current behavior rather than faking one.
        public void OnOrderServed(bool hasSecondOrderRemaining) { }
        public void PlayEatSuccess() { }
        public void PlayFirstOrderServed() { }
        public void PlayEatFail() { }
    }
}
