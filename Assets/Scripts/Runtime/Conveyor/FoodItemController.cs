using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class FoodItemController : MonoBehaviour, IPoolable<Vector3, FoodStackData, FoodVisualState>
    {
        // Above this, extra units still count toward Stack.Amount (consumed normally) but stop
        // rendering — an uncapped stack reads as a tower instead of a pile of food.
        private const int MaxVisibleUnits = 5;

        [SerializeField] private GameObject _unitPrefab;
        [SerializeField] private float _stackSpacing = 1.05f;

        [Inject(Optional = true)] private FoodMeshSettings _foodMeshSettings;
        [Inject(Optional = true)] private ConveyorFeelSettings _feel;

        // Stacked-units mode (most food types): one GameObject per visible unit. Only the top unit
        // (highest index) is ever Opened — and only while this whole item is Opened — everything below
        // it stays Closed, so the pile always reads as "one unpacked item about to be eaten, the rest
        // still packaged". Tracked per unit because Closed/Opened are often completely different meshes.
        private readonly List<GameObject> _units = new();
        private readonly List<bool> _unitIsOpened = new();
        private readonly List<GameObject> _unitSourcePrefab = new();
        private readonly List<Vector3> _unitBaseScale = new();
        private readonly List<Quaternion> _unitAppliedRotation = new();

        // Fixed-child-slots mode (e.g. a drink shelf holding several pre-placed bottles): one instance is
        // spawned and kept for the item's whole life; Amount toggles which of its slot children are visible
        // instead of stacking N separate copies. No per-unit Closed/Opened split applies here.
        private GameObject _fixedSlotInstance;
        private GameObject _fixedSlotSourcePrefab;
        private Vector3 _fixedSlotBaseScale = Vector3.one;
        private Quaternion _fixedSlotBaseRotation = Quaternion.identity;
        private float _appliedFixedSlotScale = -1f;

        private bool _usesFixedChildSlots;
        private bool _appliedUsesFixedChildSlots;

        // Last multiplier / spacing pushed onto the stacked units — used to pick up live tweaks while playing.
        private float _appliedClosedScale = -1f;
        private float _appliedOpenedScale = -1f;
        private float _appliedSpacing;

        // The rotation this item rests at. Captured from the prefab, restored on every spawn so a pooled
        // object never inherits a spin left over from its previous life.
        private Quaternion _baseRotation = Quaternion.identity;
        private bool _baseRotationCaptured;

        private FoodVisualState _visualState;

        public FoodStackData Stack { get; private set; }

        // e.g. a drink rack: several pre-placed single-item children under one shared base, instead of a
        // vertical stack of copies. Consume flights fly one of those children directly (see TakeFixedSlotChild)
        // rather than a duplicate of the whole rack.
        public bool UsesFixedChildSlots => _usesFixedChildSlots;

        public Vector3 VisualCenter => _usesFixedChildSlots
            ? transform.position
            : transform.position + Vector3.up * (Mathf.Min(Stack.Amount, MaxVisibleUnits) * ResolveSpacing(Stack.Type) * 0.5f);

        // World position of the top unit — the one that renders Opened while this item is Opened. Consume
        // flights should spawn here, not at the item root (which sits at the bottom of the stack).
        public Vector3 TopUnitWorldPosition => !_usesFixedChildSlots && _units.Count > 0 && _units[^1] != null
            ? _units[^1].transform.position
            : transform.position;

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
            FoodMeshSettings.MeshVisual closedVisual = default, openedVisual = default;
            bool hasClosed = _foodMeshSettings != null &&
                              _foodMeshSettings.TryGetVisual(stack.Type, FoodVisualState.Closed, out closedVisual);
            bool hasOpened = _foodMeshSettings != null &&
                              _foodMeshSettings.TryGetVisual(stack.Type, FoodVisualState.Opened, out openedVisual);

            bool hasStateVisual = _visualState == FoodVisualState.Opened ? hasOpened : hasClosed;
            var stateVisual = _visualState == FoodVisualState.Opened ? openedVisual : closedVisual;
            _usesFixedChildSlots = hasStateVisual && stateVisual.FixedChildSlots;

            // Switching between fixed-slot and stacked-units mode (e.g. a level mixing food types) can't
            // reuse the other mode's objects — clear whichever one was built before.
            if (_usesFixedChildSlots != _appliedUsesFixedChildSlots)
            {
                ClearVisuals();
                _appliedUsesFixedChildSlots = _usesFixedChildSlots;
            }

            if (_usesFixedChildSlots)
            {
                var sourcePrefab = stateVisual.Prefab;
                if (sourcePrefab != _fixedSlotSourcePrefab)
                {
                    if (_fixedSlotInstance != null)
                    {
                        Destroy(_fixedSlotInstance);
                        _fixedSlotInstance = null;
                    }

                    _fixedSlotSourcePrefab = sourcePrefab;
                    _fixedSlotBaseScale = sourcePrefab != null ? sourcePrefab.transform.localScale : Vector3.one;
                    _fixedSlotBaseRotation = sourcePrefab != null ? sourcePrefab.transform.localRotation : Quaternion.identity;
                }

                var correction = Quaternion.Euler(stateVisual.RotationEuler);
                ApplyFixedChildSlots(stack, sourcePrefab, _fixedSlotBaseRotation * correction);
            }
            else
            {
                ApplyStackedUnits(stack, closedVisual, openedVisual, hasClosed, hasOpened);
            }
        }

        private void ClearVisuals()
        {
            for (int i = 0; i < _units.Count; i++)
                if (_units[i] != null) Destroy(_units[i]);

            _units.Clear();
            _unitIsOpened.Clear();
            _unitSourcePrefab.Clear();
            _unitBaseScale.Clear();
            _unitAppliedRotation.Clear();

            if (_fixedSlotInstance != null)
            {
                Destroy(_fixedSlotInstance);
                _fixedSlotInstance = null;
            }

            _fixedSlotSourcePrefab = null;
        }

        private void ApplyStackedUnits(
            FoodStackData stack,
            FoodMeshSettings.MeshVisual closedVisual,
            FoodMeshSettings.MeshVisual openedVisual,
            bool hasClosed,
            bool hasOpened)
        {
            int visibleCount = Mathf.Min(stack.Amount, MaxVisibleUnits);

            while (_units.Count > visibleCount)
            {
                int last = _units.Count - 1;
                if (_units[last] != null) Destroy(_units[last]);
                _units.RemoveAt(last);
                _unitIsOpened.RemoveAt(last);
                _unitSourcePrefab.RemoveAt(last);
                _unitBaseScale.RemoveAt(last);
                _unitAppliedRotation.RemoveAt(last);
            }

            while (_units.Count < visibleCount)
            {
                _units.Add(null);
                _unitIsOpened.Add(false);
                _unitSourcePrefab.Add(null);
                _unitBaseScale.Add(Vector3.one);
                _unitAppliedRotation.Add(Quaternion.identity);
            }

            var color = FoodTypeUtil.ToColor(stack.Type);

            for (int i = 0; i < visibleCount; i++)
            {
                // Only the very top unit (highest index — see ApplyUnitLayout) shows unpacked, and only
                // while this whole item is Opened. Consuming the top unit shrinks Amount, which exposes
                // the unit below it as the new top — it picks up Opened here on the very next ApplyStack.
                bool isTop = i == visibleCount - 1;
                bool wantOpened = isTop && _visualState == FoodVisualState.Opened && hasOpened;
                _unitIsOpened[i] = wantOpened;

                var visual = wantOpened ? openedVisual : closedVisual;
                bool hasThisVisual = wantOpened ? hasOpened : hasClosed;
                var sourcePrefab = hasThisVisual ? visual.Prefab : _unitPrefab;

                if (_units[i] == null || sourcePrefab != _unitSourcePrefab[i])
                {
                    if (_units[i] != null) Destroy(_units[i]);

                    _units[i] = Instantiate(sourcePrefab, transform);
                    _unitSourcePrefab[i] = sourcePrefab;
                    _unitBaseScale[i] = sourcePrefab != null ? sourcePrefab.transform.localScale : Vector3.one;
                    var baseRotation = sourcePrefab != null ? sourcePrefab.transform.localRotation : Quaternion.identity;

                    // Extra correction on top of the prefab's own authored orientation, not a runtime
                    // rotation: it is identical in the queue, on the belt and in the exit stack, so an
                    // item never appears to turn while it travels.
                    var correction = hasThisVisual ? Quaternion.Euler(visual.RotationEuler) : Quaternion.identity;
                    _unitAppliedRotation[i] = baseRotation * correction;
                    _units[i].transform.localRotation = _unitAppliedRotation[i];

                    if (!hasThisVisual)
                    {
                        var renderer = _units[i].GetComponentInChildren<Renderer>();
                        if (renderer != null) renderer.material.color = color;
                    }
                }
            }

            ApplyUnitLayout(stack.Type);
        }

        // A rack with pre-placed items instead of a vertical stack of copies: one instance total, and
        // however many slot children the amount allows stay active — the rest hide. Slot children are
        // found by walking through any single-child wrapper levels the prefab has (e.g. a pivot-fix
        // wrapper around the artist's own rack asset) until reaching the node that actually branches.
        private void ApplyFixedChildSlots(FoodStackData stack, GameObject sourcePrefab, Quaternion unitRotation)
        {
            if (_fixedSlotInstance == null)
            {
                _fixedSlotInstance = Instantiate(sourcePrefab, transform);
                _fixedSlotInstance.transform.localPosition = Vector3.zero;
            }

            _fixedSlotInstance.transform.localRotation = unitRotation;
            float scale = ResolveScale(stack.Type, _visualState);
            _fixedSlotInstance.transform.localScale = _fixedSlotBaseScale * scale;
            _appliedFixedSlotScale = scale;

            // Convention: child[0] is the always-visible base/decoration (e.g. the tray), children[1..]
            // are the toggleable slots.
            var root = _fixedSlotInstance.transform;
            int slotCount = Mathf.Max(0, root.childCount - 1);
            int visibleCount = Mathf.Clamp(stack.Amount, 0, slotCount);

            for (int i = 0; i < slotCount; i++)
                root.GetChild(i + 1).gameObject.SetActive(i < visibleCount);
        }

        // Hands the caller the single drink that was just hidden by the latest ReduceAmount, detached from
        // the rack so it can fly to a customer on its own instead of a duplicate of the whole rack. Always
        // the rack's current last child: ReduceAmount only ever hides slots from the top down, so that's
        // always the one that just went invisible. The rack (base + remaining drinks) is left untouched.
        public Transform TakeFixedSlotChild()
        {
            if (!_usesFixedChildSlots || _fixedSlotInstance == null) return null;

            var root = _fixedSlotInstance.transform;
            if (root.childCount <= 1) return null; // only the base decoration is left

            var child = root.GetChild(root.childCount - 1);
            child.gameObject.SetActive(true);
            child.SetParent(null, worldPositionStays: true);
            return child;
        }

        // Scale and stack spacing both come from the feel asset, so they are re-applied together.
        private void ApplyUnitLayout(FoodType type)
        {
            float spacing = ResolveSpacing(type);
            _appliedClosedScale = ResolveScale(type, FoodVisualState.Closed);
            _appliedOpenedScale = ResolveScale(type, FoodVisualState.Opened);
            _appliedSpacing = spacing;

            for (int i = 0; i < _units.Count; i++)
            {
                float unitScale = ResolveScale(type, _unitIsOpened[i] ? FoodVisualState.Opened : FoodVisualState.Closed);
                _units[i].transform.localPosition = new Vector3(0f, i * spacing, 0f);
                _units[i].transform.localScale = _unitBaseScale[i] * unitScale;
            }
        }

        // Units sit exactly one rendered height apart, so a short packaged mesh and a tall opened one
        // both stack tightly without hand-tuning a constant per mesh. Measured off the bottom unit —
        // always Closed unless the stack is down to its last (Opened) piece — so the pile's spacing
        // doesn't jump around as the top unit's mesh changes.
        private float ResolveSpacing(FoodType type)
        {
            if (_feel == null || _feel.StackSpacingFactor <= 0f) return _stackSpacing;
            if (_units.Count == 0) return _stackSpacing;

            float scale = ResolveScale(type, _unitIsOpened[0] ? FoodVisualState.Opened : FoodVisualState.Closed);
            float unitHeight = ResolveUnitHeight(_unitSourcePrefab[0], _unitAppliedRotation[0], _unitBaseScale[0] * scale);
            if (unitHeight <= 0.0001f) return _stackSpacing;

            return unitHeight * _feel.StackSpacingFactor;
        }

        // Height along world Y after the unit's fixed orientation and scale are applied — a prefab laid
        // on its side stacks by its new height, not the one it was authored with.
        private static float ResolveUnitHeight(GameObject prefab, Quaternion appliedRotation, Vector3 scale)
        {
            var bounds = CombinedLocalBounds(prefab);
            if (!bounds.HasValue) return 0f;

            var size = appliedRotation * Vector3.Scale(bounds.Value.size, scale);
            return Mathf.Abs(size.y);
        }

        // Encapsulates every child mesh's bounds in the prefab root's local space — covers composite
        // visuals (e.g. a food item still sitting in its opened wrapper) with more than one MeshFilter.
        private static Bounds? CombinedLocalBounds(GameObject prefab)
        {
            if (prefab == null) return null;

            var meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
            if (meshFilters.Length == 0) return null;

            var worldToRoot = prefab.transform.worldToLocalMatrix;
            Bounds? combined = null;

            for (int i = 0; i < meshFilters.Length; i++)
            {
                var meshFilter = meshFilters[i];
                if (meshFilter.sharedMesh == null) continue;

                var meshToRoot = worldToRoot * meshFilter.transform.localToWorldMatrix;
                var meshBounds = meshFilter.sharedMesh.bounds;
                var extents = meshBounds.extents;

                var localBounds = new Bounds(meshToRoot.MultiplyPoint3x4(meshBounds.center), Vector3.zero);
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var corner = meshBounds.center + Vector3.Scale(extents, new Vector3(x, y, z));
                    localBounds.Encapsulate(meshToRoot.MultiplyPoint3x4(corner));
                }

                if (combined.HasValue)
                {
                    var c = combined.Value;
                    c.Encapsulate(localBounds);
                    combined = c;
                }
                else combined = localBounds;
            }

            return combined;
        }

        private float ResolveScale(FoodType type, FoodVisualState state) => _feel == null ? 1f : _feel.ResolveFoodScale(type, state);

        // Picks up scale / spacing tweaks made in the feel asset while the game is running.
        private void LateUpdate()
        {
            if (_usesFixedChildSlots)
            {
                float scale = ResolveScale(Stack.Type, _visualState);
                if (Mathf.Approximately(scale, _appliedFixedSlotScale)) return;

                if (_fixedSlotInstance != null) _fixedSlotInstance.transform.localScale = _fixedSlotBaseScale * scale;
                _appliedFixedSlotScale = scale;
                return;
            }

            float closedScale = ResolveScale(Stack.Type, FoodVisualState.Closed);
            float openedScale = ResolveScale(Stack.Type, FoodVisualState.Opened);
            float spacing = ResolveSpacing(Stack.Type);

            if (Mathf.Approximately(closedScale, _appliedClosedScale) &&
                Mathf.Approximately(openedScale, _appliedOpenedScale) &&
                Mathf.Approximately(spacing, _appliedSpacing))
                return;

            ApplyUnitLayout(Stack.Type);
        }

        private void CaptureBaseRotationOnce()
        {
            if (_baseRotationCaptured) return;

            _baseRotationCaptured = true;
            _baseRotation = transform.rotation;
        }

        public class Pool : MonoPoolableMemoryPool<Vector3, FoodStackData, FoodVisualState, FoodItemController>
        {
        }
    }
}
