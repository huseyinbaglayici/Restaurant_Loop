using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using TMPro;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class FoodItemController : MonoBehaviour, IPoolable<Vector3, FoodStackData, FoodVisualState>
    {
        // Above this, extra units still count toward Stack.Amount (consumed normally) but stop
        // rendering — an uncapped stack reads as a tower instead of a pile of food.
        private const int MaxVisibleUnits = 5;

        // Only reached when a unit's prefab has no MeshFilter bounds to measure a height from — should never
        // happen for authored art, but keeps stacking sane instead of collapsing units on top of each other.
        private const float FallbackStackSpacing = 1f;

        [SerializeField] private GameObject _unitPrefab;

        [Inject(Optional = true)] private FoodMeshSettings _foodMeshSettings;
        [Inject(Optional = true)] private FoodPlacementSettings _placement;
        [Inject(Optional = true)] private TableSettings _tableSettings;
        [Inject(Optional = true)] private CameraController _cameraController;

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

        // 2D Queue Visuals (Sprite Icon + Amount Text)
        private GameObject _queue2DHolder;
        private SpriteRenderer _queue2DSpriteRenderer;
        private TextMeshPro _queue2DAmountText;

        // 3D Belt/Stack Amount Badge (World-Space Text Billboard)
        private GameObject _conveyorAmountBadge;
        private TextMeshPro _conveyorAmountText;

        // Last multiplier / spacing pushed onto the stacked units — used to pick up live tweaks while playing.
        private float _appliedClosedScale = -1f;
        private float _appliedOpenedScale = -1f;
        private float _appliedSpacing;

        // The rotation this item rests at. Captured from the prefab, restored on every spawn so a pooled
        // object never inherits a spin left over from its previous life.
        private Quaternion _baseRotation = Quaternion.identity;
        private bool _baseRotationCaptured;

        private FoodVisualState _visualState;
        private FoodLocation _location;

        public FoodStackData Stack { get; private set; }

        public FoodLocation Location => _location;

        // e.g. a drink rack: several pre-placed single-item children under one shared base, instead of a
        // vertical stack of copies. Consume flights fly one of those children directly (see TakeFixedSlotChild)
        // rather than a duplicate of the whole rack.
        public bool UsesFixedChildSlots => _usesFixedChildSlots;

        public Vector3 VisualCenter
        {
            get
            {
                if (ShouldUse2DQueue() && _queue2DHolder != null)
                {
                    return _queue2DHolder.transform.position;
                }

                return _usesFixedChildSlots
                    ? transform.position
                    : transform.position + Vector3.up * (Mathf.Min(Stack.Amount, MaxVisibleUnits) * ResolveSpacing(Stack.Type) * 0.5f);
            }
        }

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
            _location = FoodLocation.Queue;
            ApplyStack(stack);
            gameObject.SetActive(true);
        }

        public void OnDespawned()
        {
            if (_queue2DHolder != null) _queue2DHolder.SetActive(false);
            if (_conveyorAmountBadge != null) _conveyorAmountBadge.SetActive(false);
            gameObject.SetActive(false);
        }

        public void SetVisualState(FoodVisualState visualState)
        {
            if (_visualState == visualState) return;

            _visualState = visualState;
            ApplyStack(Stack);
        }

        // Where this item currently sits (queue / exit stack / belt) — drives which of FoodPlacementSettings'
        // per-location scales applies. Called by the systems that move a visual between those places.
        public void SetLocation(FoodLocation location)
        {
            if (_location == location) return;

            _location = location;
            ApplyStack(Stack);
        }

        public void ReduceAmount(int amount)
        {
            var stack = Stack;
            stack.Amount -= amount;
            Stack = stack;
            ApplyStack(stack);
        }

        private bool ShouldUse2DQueue()
        {
            return _location == FoodLocation.Queue && (_tableSettings == null || _tableSettings.Enable2DQueueVisuals);
        }

        private void Ensure2DVisualHolder()
        {
            if (_queue2DHolder != null) return;

            _queue2DHolder = new GameObject("Queue2D_Visual");
            _queue2DHolder.transform.SetParent(transform, false);

            _queue2DSpriteRenderer = _queue2DHolder.AddComponent<SpriteRenderer>();
            _queue2DSpriteRenderer.sortingOrder = 10;

            var textObj = new GameObject("AmountText");
            textObj.transform.SetParent(_queue2DHolder.transform, false);
            // Position at top-right corner of the 2D sprite
            textObj.transform.localPosition = new Vector3(6f, 6f, -0.2f);
            textObj.transform.localRotation = Quaternion.identity;
            textObj.transform.localScale = Vector3.one * 12f;

            var rt = textObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(3f, 3f);

            _queue2DAmountText = textObj.AddComponent<TextMeshPro>();
            if (_foodMeshSettings != null && _foodMeshSettings.StackAmountFont != null)
            {
                _queue2DAmountText.font = _foodMeshSettings.StackAmountFont;
            }
            _queue2DAmountText.alignment = TextAlignmentOptions.Center;
            _queue2DAmountText.fontSize = 6.5f;
            _queue2DAmountText.color = Color.white;
            _queue2DAmountText.fontStyle = FontStyles.Bold;
            _queue2DAmountText.sortingOrder = 50; // Higher sorting order so it always appears above the sprite
            
            var meshRenderer = textObj.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = 50;
            }
        }

        private void EnsureConveyorAmountBadge()
        {
            if (_conveyorAmountBadge != null) return;

            _conveyorAmountBadge = new GameObject("Conveyor3D_AmountBadge");
            _conveyorAmountBadge.transform.SetParent(transform, false);

            var textObj = new GameObject("AmountText");
            textObj.transform.SetParent(_conveyorAmountBadge.transform, false);
            textObj.transform.localPosition = Vector3.zero;
            textObj.transform.localRotation = Quaternion.identity;
            textObj.transform.localScale = Vector3.one;

            var rt = textObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(2f, 2f);

            _conveyorAmountText = textObj.AddComponent<TextMeshPro>();
            if (_foodMeshSettings != null && _foodMeshSettings.StackAmountFont != null)
            {
                _conveyorAmountText.font = _foodMeshSettings.StackAmountFont;
            }
            _conveyorAmountText.alignment = TextAlignmentOptions.Center;
            _conveyorAmountText.fontSize = 5.5f;
            _conveyorAmountText.color = Color.white;
            _conveyorAmountText.fontStyle = FontStyles.Bold;
            _conveyorAmountText.sortingOrder = 20;
        }

        public void SetAmountBadgeVisible(bool visible)
        {
            if (_conveyorAmountBadge != null)
            {
                _conveyorAmountBadge.SetActive(visible);
            }
        }

        private void ApplyConveyorAmountBadge(FoodStackData stack)
        {
            if (stack.Amount <= 0)
            {
                if (_conveyorAmountBadge != null) _conveyorAmountBadge.SetActive(false);
                return;
            }

            EnsureConveyorAmountBadge();
            _conveyorAmountBadge.SetActive(true);
            _conveyorAmountText.text = stack.Amount.ToString();

            // Place slightly to the top right and above the top unit
            float height = _usesFixedChildSlots ? 0.7f : (Mathf.Min(stack.Amount, MaxVisibleUnits) * ResolveSpacing(stack.Type) + 0.3f);
            _conveyorAmountBadge.transform.localPosition = new Vector3(0.4f, height, 0f);

            AlignConveyorBadgeWithCamera();
        }

        private void AlignConveyorBadgeWithCamera()
        {
            if (_conveyorAmountBadge == null || !_conveyorAmountBadge.activeSelf) return;

            var cam = Camera.main;
            if (cam != null)
            {
                _conveyorAmountBadge.transform.rotation = cam.transform.rotation;
            }
        }

        private void ApplyQueue2DVisual(FoodStackData stack)
        {
            Ensure2DVisualHolder();

            _queue2DHolder.SetActive(true);

            if (_foodMeshSettings != null && _foodMeshSettings.TryGetQueueIcon(stack.Type, out var icon))
            {
                _queue2DSpriteRenderer.sprite = icon;
            }
            else
            {
                _queue2DSpriteRenderer.sprite = null;
            }

            if (_queue2DAmountText != null)
            {
                if (_foodMeshSettings != null && _foodMeshSettings.StackAmountFont != null && _queue2DAmountText.font != _foodMeshSettings.StackAmountFont)
                {
                    _queue2DAmountText.font = _foodMeshSettings.StackAmountFont;
                }
                _queue2DAmountText.text = stack.Amount > 0 ? stack.Amount.ToString() : "";
            }

            float baseIconScale = _tableSettings != null ? _tableSettings.Queue2DIconScale : 1.5f;
            float zoomMultiplier = 1f;
            if (_tableSettings != null && _tableSettings.ScaleWithCamera && _cameraController != null)
            {
                float zoom = _cameraController.CameraZoomFactor;
                float multiplier = _tableSettings.FoodQueueScaleMultiplier;
                zoomMultiplier = 1f + (zoom - 1f) * multiplier;
            }

            _queue2DHolder.transform.localScale = Vector3.one * (baseIconScale * zoomMultiplier);
            _queue2DHolder.transform.localPosition = _tableSettings != null ? _tableSettings.Queue2DOffset : new Vector3(0f, 0.5f, 0f);

            Align2DWithCamera();
        }

        private void Align2DWithCamera()
        {
            if (_queue2DHolder == null || !_queue2DHolder.activeSelf) return;

            var cam = Camera.main;
            if (cam != null)
            {
                _queue2DHolder.transform.rotation = cam.transform.rotation;
            }
        }

        private void ApplyStack(FoodStackData stack)
        {
            bool is2D = ShouldUse2DQueue();

            if (is2D)
            {
                ApplyQueue2DVisual(stack);
                Set3DVisualsActive(false);
                if (_conveyorAmountBadge != null) _conveyorAmountBadge.SetActive(false);
                return;
            }

            if (_queue2DHolder != null) _queue2DHolder.SetActive(false);
            Set3DVisualsActive(true);
            ApplyConveyorAmountBadge(stack);

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

        private void Set3DVisualsActive(bool active)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i] != null) _units[i].SetActive(active);
            }

            if (_fixedSlotInstance != null)
            {
                _fixedSlotInstance.SetActive(active);
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
            // Clamped to at least 1: an authored/linked stack can sit at Amount 0 (e.g. a decoy half of a
            // linked pair) while still occupying a real slot — it must stay visible and clickable there,
            // never silently disappear. Genuine consumption never leaves an in-place item at exactly 0
            // (ConsumeFromItem removes a fully-eaten belt item outright before this runs), so this only
            // ever affects that authored-zero case.
            int visibleCount = Mathf.Clamp(stack.Amount, 1, MaxVisibleUnits);

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
        // however many slot children the amount allows stay active — the rest hide. The rack's own mesh
        // sits on whichever node actually branches (e.g. icecek_stack: a single-child pivot wrapper down
        // to the base holding the 4 icecek_tekli children) — ResolveFixedSlotContainer walks down through
        // any such single-child wrapper levels to find it; that node's children (all of them) are the slots.
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

            var container = ResolveFixedSlotContainer();
            int slotCount = container != null ? container.childCount : 0;
            // Same Amount-0-stays-visible rule as ApplyStackedUnits — see comment there.
            int visibleCount = slotCount == 0 ? 0 : Mathf.Clamp(stack.Amount, 1, slotCount);

            for (int i = 0; i < slotCount; i++)
                container.GetChild(i).gameObject.SetActive(i < visibleCount);
        }

        // Descends through wrapper levels that only exist to fix a pivot/orientation (exactly one child)
        // until reaching the node that actually holds the slot children side by side.
        private Transform ResolveFixedSlotContainer()
        {
            if (_fixedSlotInstance == null) return null;

            var node = _fixedSlotInstance.transform;
            while (node.childCount == 1) node = node.GetChild(0);
            return node;
        }

        // Hands the caller the single drink that was just hidden by the latest ReduceAmount, detached from
        // the rack so it can fly to a customer on its own instead of a duplicate of the whole rack. Always
        // the container's current last child: ReduceAmount only ever hides slots from the top down, so
        // that's always the one that just went invisible. The rack (base + remaining drinks) is left untouched.
        public Transform TakeFixedSlotChild()
        {
            if (!_usesFixedChildSlots) return null;

            var container = ResolveFixedSlotContainer();
            if (container == null || container.childCount == 0) return null;

            var child = container.GetChild(container.childCount - 1);
            child.gameObject.SetActive(true);
            child.SetParent(null, worldPositionStays: true);
            return child;
        }

        // Scale and stack spacing both come from the placement asset, so they are re-applied together.
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
            if (_units.Count == 0 || _placement == null) return FallbackStackSpacing;

            float scale = ResolveScale(type, _unitIsOpened[0] ? FoodVisualState.Opened : FoodVisualState.Closed);
            float unitHeight = ResolveUnitHeight(_unitSourcePrefab[0], _unitAppliedRotation[0], _unitBaseScale[0] * scale);
            if (unitHeight <= 0.0001f) return FallbackStackSpacing;

            return unitHeight * _placement.ResolveStackSpacingFactor(type);
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

        private float ResolveScale(FoodType type, FoodVisualState state)
        {
            float baseScale = _placement == null ? 1f : _placement.ResolveScale(type, _location, state);
            if (_location == FoodLocation.Queue && _tableSettings != null && _tableSettings.ScaleWithCamera && _cameraController != null)
            {
                float zoom = _cameraController.CameraZoomFactor;
                float multiplier = _tableSettings.FoodQueueScaleMultiplier;
                float factor = 1f + (zoom - 1f) * multiplier;
                baseScale *= factor;
            }
            return baseScale;
        }

        // Picks up scale / spacing tweaks made in the placement asset while the game is running.
        private void LateUpdate()
        {
            if (ShouldUse2DQueue())
            {
                Align2DWithCamera();
                return;
            }

            AlignConveyorBadgeWithCamera();

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
