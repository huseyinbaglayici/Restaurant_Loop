using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Replaces TestCustomerController now that the customer rig is baked into a Vertex Animation
    // Texture (see VATBaker) instead of skinned + Animator Controller. Customer.prefab uses two
    // different shaders across its parts (body/eyes vs pants/shoes/tshirt/Eyebrow), so the bake is
    // split into two renderers sharing one VAT texture/clip layout — both driven by the same
    // playhead so they stay in sync. Single fixed look — no hair, no per-instance variation. Only
    // the idle phase/speed is randomized per spawn, driven from OnEnable rather than Start().
    // Start() only ever fires once per pooled instance's lifetime, which would have re-synced idle
    // phase on every respawn after the first; OnEnable fires on every SetActive(true), matching
    // CustomerController.OnSpawned.
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

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            InitializeIdleAnimation();
        }

        [ContextMenu("Initialize Idle (Random Offset)")]
        public void InitializeIdleAnimation()
        {
            if (_clips == null || !_clips.Idle.IsValid) return;

            _speed = Random.Range(0.95f, 1.05f);
            _time = Random.Range(0f, _clips.Idle.Duration(_clips.SampleFps));
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

        // Reaction hooks — no-ops for now. happy_idle_beko.controller has no reaction clips today
        // (TestCustomerController's happy/first-order/fail triggers already reference parameters
        // that don't exist on it), so this preserves current behavior rather than faking one.
        public void OnOrderServed(bool hasSecondOrderRemaining) { }
        public void PlayEatSuccess() { }
        public void PlayFirstOrderServed() { }
        public void PlayEatFail() { }
    }
}
