using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Builds and lays out the 3D visuals of one food item: either a vertical pile of unit copies or a
    // single rack prefab whose slot children toggle with the amount. Owned by FoodItemController.
    public class FoodStackBuilder
    {
        // Above this, extra units still count toward Stack.Amount (consumed normally) but stop
        // rendering — an uncapped stack reads as a tower instead of a pile of food.
        private const int MaxVisibleUnits = 5;

        // Only reached when a unit's prefab has no MeshFilter bounds to measure a height from — should never
        // happen for authored art, but keeps stacking sane instead of collapsing units on top of each other.
        private const float FallbackStackSpacing = 1f;

        private readonly Transform _owner;
        private readonly GameObject _unitPrefab;
        private readonly FoodMeshSettings _foodMeshSettings;
        private readonly FoodPlacementSettings _placement;
        private readonly TableSettings _tableSettings;
        private readonly CameraController _cameraController;

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

        public FoodStackBuilder(
            Transform owner,
            GameObject unitPrefab,
            FoodMeshSettings foodMeshSettings,
            FoodPlacementSettings placement,
            TableSettings tableSettings,
            CameraController cameraController)
        {
            _owner = owner;
            _unitPrefab = unitPrefab;
            _foodMeshSettings = foodMeshSettings;
            _placement = placement;
            _tableSettings = tableSettings;
            _cameraController = cameraController;
        }

        public bool UsesFixedChildSlots => _usesFixedChildSlots;

        // Middle of the visible pile (or the rack root) — used for tap picking.
        public Vector3 GetVisualCenter(FoodStackData stack, FoodLocation location)
        {
            return _usesFixedChildSlots
                ? _owner.position
                : _owner.position + Vector3.up * (Mathf.Min(stack.Amount, MaxVisibleUnits) * ResolveSpacing(stack.Type, location) * 0.5f);
        }

        // World position of the top unit — the one that renders Opened while this item is Opened.
        public Vector3 TopUnitWorldPosition
        {
            get
            {
                if (!_usesFixedChildSlots && _units.Count > 0)
                {
                    float highestY = float.MinValue;
                    float centerX = _owner.position.x;
                    float centerZ = _owner.position.z;
                    bool found = false;

                    for (int i = 0; i < _units.Count; i++)
                    {
                        if (_units[i] != null && _units[i].activeInHierarchy)
                        {
                            var r = _units[i].GetComponentInChildren<Renderer>();
                            if (r != null)
                            {
                                highestY = Mathf.Max(highestY, r.bounds.max.y);
                                found = true;
                            }
                        }
                    }
                    if (found)
                    {
                        return new Vector3(centerX, highestY, centerZ);
                    }
                }
                else if (_usesFixedChildSlots && _fixedSlotInstance != null)
                {
                    var renderers = _fixedSlotInstance.GetComponentsInChildren<Renderer>();
                    if (renderers != null && renderers.Length > 0)
                    {
                        float maxY = float.MinValue;
                        bool found = false;
                        for (int i = 0; i < renderers.Length; i++)
                        {
                            if (renderers[i] != null && renderers[i].gameObject.activeInHierarchy)
                            {
                                maxY = Mathf.Max(maxY, renderers[i].bounds.max.y);
                                found = true;
                            }
                        }
                        if (found) return new Vector3(_owner.position.x, maxY, _owner.position.z);
                    }
                }
                return _owner.position;
            }
        }

        public void Build(FoodStackData stack, FoodVisualState visualState, FoodLocation location)
        {
            FoodMeshSettings.MeshVisual closedVisual = default, openedVisual = default;
            bool hasClosed = _foodMeshSettings != null &&
                              _foodMeshSettings.TryGetVisual(stack.Type, FoodVisualState.Closed, out closedVisual);
            bool hasOpened = _foodMeshSettings != null &&
                              _foodMeshSettings.TryGetVisual(stack.Type, FoodVisualState.Opened, out openedVisual);

            bool hasStateVisual = visualState == FoodVisualState.Opened ? hasOpened : hasClosed;
            var stateVisual = visualState == FoodVisualState.Opened ? openedVisual : closedVisual;
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
                        Object.Destroy(_fixedSlotInstance);
                        _fixedSlotInstance = null;
                    }

                    _fixedSlotSourcePrefab = sourcePrefab;
                    _fixedSlotBaseScale = sourcePrefab != null ? sourcePrefab.transform.localScale : Vector3.one;
                    _fixedSlotBaseRotation = sourcePrefab != null ? sourcePrefab.transform.localRotation : Quaternion.identity;
                }

                var correction = Quaternion.Euler(stateVisual.RotationEuler);
                ApplyFixedChildSlots(stack, visualState, location, sourcePrefab, _fixedSlotBaseRotation * correction);
            }
            else
            {
                ApplyStackedUnits(stack, visualState, location, closedVisual, openedVisual, hasClosed, hasOpened);
            }
        }

        public void SetActive(bool active)
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

        // Re-applies scale / spacing only when the placement asset (or camera zoom) changed since the last apply.
        public void RefreshLayout(FoodType type, FoodVisualState visualState, FoodLocation location)
        {
            if (_usesFixedChildSlots)
            {
                float scale = ResolveScale(type, visualState, location);
                if (Mathf.Approximately(scale, _appliedFixedSlotScale)) return;

                if (_fixedSlotInstance != null) _fixedSlotInstance.transform.localScale = _fixedSlotBaseScale * scale;
                _appliedFixedSlotScale = scale;
                return;
            }

            float closedScale = ResolveScale(type, FoodVisualState.Closed, location);
            float openedScale = ResolveScale(type, FoodVisualState.Opened, location);
            float spacing = ResolveSpacing(type, location);

            if (Mathf.Approximately(closedScale, _appliedClosedScale) &&
                Mathf.Approximately(openedScale, _appliedOpenedScale) &&
                Mathf.Approximately(spacing, _appliedSpacing))
                return;

            ApplyUnitLayout(type, location);
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

        private void ClearVisuals()
        {
            for (int i = 0; i < _units.Count; i++)
                if (_units[i] != null) Object.Destroy(_units[i]);

            _units.Clear();
            _unitIsOpened.Clear();
            _unitSourcePrefab.Clear();
            _unitBaseScale.Clear();
            _unitAppliedRotation.Clear();

            if (_fixedSlotInstance != null)
            {
                Object.Destroy(_fixedSlotInstance);
                _fixedSlotInstance = null;
            }

            _fixedSlotSourcePrefab = null;
        }

        private void ApplyStackedUnits(
            FoodStackData stack,
            FoodVisualState visualState,
            FoodLocation location,
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
                if (_units[last] != null) Object.Destroy(_units[last]);
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
                // the unit below it as the new top — it picks up Opened here on the very next Build.
                bool isTop = i == visibleCount - 1;
                bool wantOpened = isTop && visualState == FoodVisualState.Opened && hasOpened;
                _unitIsOpened[i] = wantOpened;

                var visual = wantOpened ? openedVisual : closedVisual;
                bool hasThisVisual = wantOpened ? hasOpened : hasClosed;
                var sourcePrefab = hasThisVisual ? visual.Prefab : _unitPrefab;

                if (_units[i] == null || sourcePrefab != _unitSourcePrefab[i])
                {
                    if (_units[i] != null) Object.Destroy(_units[i]);

                    _units[i] = Object.Instantiate(sourcePrefab, _owner);
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

            ApplyUnitLayout(stack.Type, location);
        }

        // A rack with pre-placed items instead of a vertical stack of copies: one instance total, and
        // however many slot children the amount allows stay active — the rest hide. The rack's own mesh
        // sits on whichever node actually branches (e.g. icecek_stack: a single-child pivot wrapper down
        // to the base holding the 4 icecek_tekli children) — ResolveFixedSlotContainer walks down through
        // any such single-child wrapper levels to find it; that node's children (all of them) are the slots.
        private void ApplyFixedChildSlots(
            FoodStackData stack,
            FoodVisualState visualState,
            FoodLocation location,
            GameObject sourcePrefab,
            Quaternion unitRotation)
        {
            if (_fixedSlotInstance == null)
            {
                _fixedSlotInstance = Object.Instantiate(sourcePrefab, _owner);
                _fixedSlotInstance.transform.localPosition = Vector3.zero;
            }

            _fixedSlotInstance.transform.localRotation = unitRotation;
            float scale = ResolveScale(stack.Type, visualState, location);
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

        // Scale and stack spacing both come from the placement asset, so they are re-applied together.
        private void ApplyUnitLayout(FoodType type, FoodLocation location)
        {
            float spacing = ResolveSpacing(type, location);
            _appliedClosedScale = ResolveScale(type, FoodVisualState.Closed, location);
            _appliedOpenedScale = ResolveScale(type, FoodVisualState.Opened, location);
            _appliedSpacing = spacing;

            bool isDrink = type == FoodType.Drink;

            for (int i = 0; i < _units.Count; i++)
            {
                float unitScale = ResolveScale(type, _unitIsOpened[i] ? FoodVisualState.Opened : FoodVisualState.Closed, location);
                // For Drink specifically, the bottom model is shifted so it renders in front/on top
                float yOffset = isDrink ? (_units.Count - 1 - i) * spacing : i * spacing;
                _units[i].transform.localPosition = new Vector3(0f, yOffset, 0f);
                _units[i].transform.localScale = _unitBaseScale[i] * unitScale;
            }
        }

        // Units sit exactly one rendered height apart, so a short packaged mesh and a tall opened one
        // both stack tightly without hand-tuning a constant per mesh. Measured off the bottom unit —
        // always Closed unless the stack is down to its last (Opened) piece — so the pile's spacing
        // doesn't jump around as the top unit's mesh changes.
        private float ResolveSpacing(FoodType type, FoodLocation location)
        {
            var placement = _placement;
            if (_units.Count == 0 || placement == null) return FallbackStackSpacing;

            float scale = ResolveScale(type, _unitIsOpened[0] ? FoodVisualState.Opened : FoodVisualState.Closed, location);
            float unitHeight = ResolveUnitHeight(_unitSourcePrefab[0], _unitAppliedRotation[0], _unitBaseScale[0] * scale);
            if (unitHeight <= 0.0001f) return FallbackStackSpacing;

            return unitHeight * placement.ResolveStackSpacingFactor(type, location);
        }

        // Height along world Y after the unit's fixed orientation and scale are applied — a prefab laid
        // on its side stacks by its new height, not the one it was authored with.
        private static float ResolveUnitHeight(GameObject prefab, Quaternion appliedRotation, Vector3 scale)
        {
            var bounds = CombinedLocalBounds(prefab);
            if (!bounds.HasValue) return 0.25f;

            var size = appliedRotation * Vector3.Scale(bounds.Value.size, scale);
            float h = Mathf.Abs(size.y);
            // Uninstantiated 2D sprite prefabs (PF_Burger etc.) can report bounds.size.y around 20.48 units;
            // clamp the layer height to a realistic thickness in that case.
            if (h > 0.6f) h = 0.25f;
            return h > 0.0001f ? h : 0.25f;
        }

        // Encapsulates every child mesh's bounds in the prefab root's local space — covers composite
        // visuals (e.g. a food item still sitting in its opened wrapper) with more than one MeshFilter.
        private static Bounds? CombinedLocalBounds(GameObject prefab)
        {
            if (prefab == null) return null;

            var meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
            var spriteRenderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
            if (meshFilters.Length == 0 && spriteRenderers.Length == 0) return null;

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

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                var sr = spriteRenderers[i];
                if (sr.sprite == null) continue;

                var srToRoot = worldToRoot * sr.transform.localToWorldMatrix;
                var spriteBounds = sr.sprite.bounds;
                var extents = spriteBounds.extents;

                var localBounds = new Bounds(srToRoot.MultiplyPoint3x4(spriteBounds.center), Vector3.zero);
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var corner = spriteBounds.center + Vector3.Scale(extents, new Vector3(x, y, z));
                    localBounds.Encapsulate(srToRoot.MultiplyPoint3x4(corner));
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

        private float ResolveScale(FoodType type, FoodVisualState state, FoodLocation location)
        {
            var placement = _placement;
            float baseScale = placement == null ? 1f : placement.ResolveScale(type, location, state);
            if (location == FoodLocation.Queue && _tableSettings != null && _tableSettings.ScaleWithCamera && _cameraController != null)
            {
                float zoom = _cameraController.CameraZoomFactor;
                float multiplier = _tableSettings.FoodQueueScaleMultiplier;
                float factor = 1f + (zoom - 1f) * multiplier;
                baseScale *= factor;
            }
            return baseScale;
        }
    }
}
