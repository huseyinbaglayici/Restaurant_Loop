using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class FoodItemController : MonoBehaviour, IPoolable<Vector3, FoodStackData, FoodVisualState>
    {
        [SerializeField] private GameObject _unitPrefab;
        [SerializeField] private float _stackSpacing = 1.05f;

        [Inject(Optional = true)] private FoodMeshSettings _foodMeshSettings;
        [Inject(Optional = true)] private ConveyorFeelSettings _feel;

        private readonly List<GameObject> _units = new();

        // The prefab's authored scale. The feel multiplier is applied on top of it at runtime and never
        // written back, so the prefab and the source mesh keep whatever the artist set.
        private Vector3 _unitBaseScale = Vector3.one;
        private bool _unitBaseScaleCaptured;

        private Mesh _unitBaseMesh;
        private Material[] _unitBaseMaterials;
        private bool _unitBaseVisualCaptured;

        // Last multiplier / spacing pushed onto the units — used to pick up live tweaks while playing.
        private float _appliedUnitScale = -1f;
        private float _appliedSpacing;
        private Mesh _appliedMesh;
        private Quaternion _appliedUnitRotation = Quaternion.identity;

        // The rotation this item rests at. Captured from the prefab, restored on every spawn so a pooled
        // object never inherits a spin left over from its previous life.
        private Quaternion _baseRotation = Quaternion.identity;
        private bool _baseRotationCaptured;

        private FoodVisualState _visualState;

        public FoodStackData Stack { get; private set; }

        public Vector3 VisualCenter => transform.position + Vector3.up * (Stack.Amount * ResolveSpacing() * 0.5f);

        public void OnSpawned(Vector3 position, FoodStackData stack, FoodVisualState visualState)
        {
            CaptureBaseRotationOnce();

            transform.SetPositionAndRotation(position, _baseRotation);
            Stack = stack;
            _visualState = visualState;
            ApplyStack(stack);
            gameObject.SetActive(true);
        }

        public void OnDespawned()
        {
            gameObject.SetActive(false);
        }

        // Closed -> Opened when a queue item finishes flying onto the belt.
        public void SetVisualState(FoodVisualState visualState)
        {
            if (_visualState == visualState) return;

            _visualState = visualState;
            ApplyStack(Stack);
        }

        public void ReduceAmount(int amount)
        {
            var stack = Stack;
            stack.Amount -= amount;
            Stack = stack;
            ApplyStack(stack);
        }

        private void ApplyStack(FoodStackData stack)
        {
            CaptureUnitBaseScaleOnce();
            CaptureUnitBaseVisualOnce();

            while (_units.Count < stack.Amount)
                _units.Add(Instantiate(_unitPrefab, transform));

            while (_units.Count > stack.Amount)
            {
                var top = _units[^1];
                _units.RemoveAt(_units.Count - 1);
                Destroy(top);
            }

            var color = FoodTypeUtil.ToColor(stack.Type);
            FoodMeshSettings.MeshVisual meshVisual = default;
            bool hasMesh = _foodMeshSettings != null &&
                           _foodMeshSettings.TryGetVisual(stack.Type, _visualState, out meshVisual);
            _appliedMesh = hasMesh ? meshVisual.Mesh : null;

            // Fixed per-mesh orientation, not a runtime rotation: it is identical in the queue, on the belt
            // and in the exit stack, so an item never appears to turn while it travels.
            var unitRotation = hasMesh ? Quaternion.Euler(meshVisual.RotationEuler) : Quaternion.identity;
            _appliedUnitRotation = unitRotation;

            for (int i = 0; i < _units.Count; i++)
            {
                var renderer = _units[i].GetComponentInChildren<Renderer>();

                if (renderer != null)
                {
                    if (hasMesh && meshVisual.HasMaterials) renderer.sharedMaterials = meshVisual.Materials;
                    else
                    {
                        renderer.sharedMaterials = _unitBaseMaterials;
                        renderer.material.color = color;
                    }
                }

                _units[i].transform.localRotation = unitRotation;

                var meshFilter = _units[i].GetComponentInChildren<MeshFilter>();
                if (meshFilter != null) meshFilter.sharedMesh = hasMesh ? meshVisual.Mesh : _unitBaseMesh;
            }

            ApplyUnitLayout();
        }

        // Scale and stack spacing both come from the feel asset, so they are re-applied together.
        private void ApplyUnitLayout()
        {
            float unitScale = ResolveUnitScale();
            float spacing = ResolveSpacing();

            _appliedUnitScale = unitScale;
            _appliedSpacing = spacing;

            for (int i = 0; i < _units.Count; i++)
            {
                _units[i].transform.localPosition = new Vector3(0f, i * spacing, 0f);
                _units[i].transform.localScale = _unitBaseScale * unitScale;
            }
        }

        // Units sit exactly one rendered height apart, so a short packaged mesh and a tall opened one
        // both stack tightly without hand-tuning a constant per mesh.
        private float ResolveSpacing()
        {
            if (_feel == null || _feel.StackSpacingFactor <= 0f) return _stackSpacing;

            float unitHeight = ResolveUnitHeight() * ResolveUnitScale();
            if (unitHeight <= 0.0001f) return _stackSpacing;

            return unitHeight * _feel.StackSpacingFactor;
        }

        private float ResolveUnitHeight()
        {
            var mesh = _appliedMesh;
            if (mesh == null && _unitPrefab != null)
            {
                var prefabMeshFilter = _unitPrefab.GetComponentInChildren<MeshFilter>();
                if (prefabMeshFilter != null) mesh = prefabMeshFilter.sharedMesh;
            }

            if (mesh == null) return 0f;

            // Height along world Y after the fixed orientation is applied — a mesh laid on its side
            // stacks by its new height, not the one it was authored with.
            var size = _appliedUnitRotation * Vector3.Scale(mesh.bounds.size, _unitBaseScale);
            return Mathf.Abs(size.y);
        }

        private float ResolveUnitScale() => _feel == null ? 1f : _feel.ResolveFoodScale(Stack.Type, _visualState);

        // Picks up scale / spacing tweaks made in the feel asset while the game is running.
        private void LateUpdate()
        {
            if (Mathf.Approximately(ResolveUnitScale(), _appliedUnitScale) &&
                Mathf.Approximately(ResolveSpacing(), _appliedSpacing))
                return;

            ApplyUnitLayout();
        }

        private void CaptureBaseRotationOnce()
        {
            if (_baseRotationCaptured) return;

            _baseRotationCaptured = true;
            _baseRotation = transform.rotation;
        }

        private void CaptureUnitBaseScaleOnce()
        {
            if (_unitBaseScaleCaptured || _unitPrefab == null) return;

            _unitBaseScaleCaptured = true;
            _unitBaseScale = _unitPrefab.transform.localScale;
        }

        private void CaptureUnitBaseVisualOnce()
        {
            if (_unitBaseVisualCaptured || _unitPrefab == null) return;

            _unitBaseVisualCaptured = true;

            var meshFilter = _unitPrefab.GetComponentInChildren<MeshFilter>();
            _unitBaseMesh = meshFilter != null ? meshFilter.sharedMesh : null;

            var renderer = _unitPrefab.GetComponentInChildren<Renderer>();
            _unitBaseMaterials = renderer != null ? renderer.sharedMaterials : null;
        }

        public class Pool : MonoPoolableMemoryPool<Vector3, FoodStackData, FoodVisualState, FoodItemController>
        {
        }
    }
}