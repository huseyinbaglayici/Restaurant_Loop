using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Builds and lays out the 3D visuals of one food item: either a vertical pile of unit copies or a
    // single rack prefab (FoodRackVisual) whose slot children toggle with the amount. Owned by FoodItemController.
    public class FoodStackBuilder
    {
        // Extra units still count toward Stack.Amount but stop rendering, so a big stack reads as a pile, not a tower.
        private const int MaxVisibleUnits = 5;

        // Used when a unit has no measurable bounds, so units never collapse onto each other.
        private const float FallbackStackSpacing = 1f;

        // One visible copy in the pile. Closed/Opened are often different meshes, so each unit tracks its own.
        private class StackUnit
        {
            public GameObject Instance;
            public GameObject SourcePrefab;
            public bool IsOpened;
            public Vector3 BaseScale = Vector3.one;
            public Quaternion AppliedRotation = Quaternion.identity;
        }

        private readonly Transform _owner;
        private readonly GameObject _unitPrefab;
        private readonly FoodMeshSettings _foodMeshSettings;
        private readonly FoodPlacementSettings _placement;
        private readonly TableSettings _tableSettings;
        private readonly CameraController _cameraController;
        private readonly FoodRackVisual _rack;

        // Only the top unit is ever Opened (and only while the whole item is Opened); the rest stay packaged.
        private readonly List<StackUnit> _units = new();

        private bool _usesFixedChildSlots;
        private bool _appliedUsesFixedChildSlots;

        // Last values pushed onto the units, so live placement tweaks are picked up while playing.
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
            _rack = new FoodRackVisual(owner);
        }

        public bool UsesFixedChildSlots => _usesFixedChildSlots;

        // Middle of the visible pile (or the rack root), used for tap picking.
        public Vector3 GetVisualCenter(FoodStackData stack, FoodLocation location)
        {
            return _usesFixedChildSlots
                ? _owner.position
                : _owner.position + Vector3.up * (Mathf.Min(stack.Amount, MaxVisibleUnits) * ResolveSpacing(stack.Type, location) * 0.5f);
        }

        // Top of the highest visible renderer, centered on the item.
        public Vector3 TopUnitWorldPosition
        {
            get
            {
                float topY;
                bool found = _usesFixedChildSlots ? _rack.TryGetTopY(out topY) : TryGetUnitsTopY(out topY);
                return found ? new Vector3(_owner.position.x, topY, _owner.position.z) : _owner.position;
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

            // One mode can't reuse the other mode's objects (a pooled item may change food type).
            if (_usesFixedChildSlots != _appliedUsesFixedChildSlots)
            {
                ClearVisuals();
                _appliedUsesFixedChildSlots = _usesFixedChildSlots;
            }

            if (_usesFixedChildSlots)
            {
                float scale = ResolveScale(stack.Type, visualState, location);
                _rack.Apply(stateVisual.Prefab, stateVisual.RotationEuler, stack.Amount, scale);
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
                if (_units[i].Instance != null) _units[i].Instance.SetActive(active);
            }

            _rack.SetActive(active);
        }

        // Re-applies scale / spacing only when the placement asset (or camera zoom) changed since the last apply.
        public void RefreshLayout(FoodType type, FoodVisualState visualState, FoodLocation location)
        {
            if (_usesFixedChildSlots)
            {
                _rack.RefreshScale(ResolveScale(type, visualState, location));
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

        // Detaches the rack child hidden by the latest ReduceAmount so it can fly to a customer on its own.
        public Transform TakeFixedSlotChild()
        {
            return _usesFixedChildSlots ? _rack.TakeSlotChild() : null;
        }

        private bool TryGetUnitsTopY(out float topY)
        {
            topY = float.MinValue;
            bool found = false;

            for (int i = 0; i < _units.Count; i++)
            {
                var instance = _units[i].Instance;
                if (instance == null || !instance.activeInHierarchy) continue;

                var renderer = instance.GetComponentInChildren<Renderer>();
                if (renderer == null) continue;

                topY = Mathf.Max(topY, renderer.bounds.max.y);
                found = true;
            }

            return found;
        }

        private void ClearVisuals()
        {
            for (int i = 0; i < _units.Count; i++)
                if (_units[i].Instance != null) Object.Destroy(_units[i].Instance);

            _units.Clear();
            _rack.Clear();
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
            // Clamped to 1: an authored stack can sit at Amount 0 (e.g. the decoy half of a linked pair) and
            // must stay visible and clickable. A fully eaten belt item is removed before this ever runs.
            int visibleCount = Mathf.Clamp(stack.Amount, 1, MaxVisibleUnits);

            while (_units.Count > visibleCount)
            {
                int last = _units.Count - 1;
                if (_units[last].Instance != null) Object.Destroy(_units[last].Instance);
                _units.RemoveAt(last);
            }

            while (_units.Count < visibleCount)
                _units.Add(new StackUnit());

            var color = FoodTypeUtil.ToColor(stack.Type);

            for (int i = 0; i < visibleCount; i++)
            {
                var unit = _units[i];

                // Eating the top unit shrinks Amount, so the unit below becomes the new Opened top on the next Build.
                bool isTop = i == visibleCount - 1;
                bool wantOpened = isTop && visualState == FoodVisualState.Opened && hasOpened;
                unit.IsOpened = wantOpened;

                var visual = wantOpened ? openedVisual : closedVisual;
                bool hasThisVisual = wantOpened ? hasOpened : hasClosed;
                var sourcePrefab = hasThisVisual ? visual.Prefab : _unitPrefab;

                if (unit.Instance != null && sourcePrefab == unit.SourcePrefab) continue;

                if (unit.Instance != null) Object.Destroy(unit.Instance);

                unit.Instance = Object.Instantiate(sourcePrefab, _owner);
                unit.SourcePrefab = sourcePrefab;
                unit.BaseScale = sourcePrefab != null ? sourcePrefab.transform.localScale : Vector3.one;
                var baseRotation = sourcePrefab != null ? sourcePrefab.transform.localRotation : Quaternion.identity;

                // Fixed correction on top of the prefab's own orientation, the same in queue, belt and exit
                // stack, so an item never appears to turn while it travels.
                var correction = hasThisVisual ? Quaternion.Euler(visual.RotationEuler) : Quaternion.identity;
                unit.AppliedRotation = baseRotation * correction;
                unit.Instance.transform.localRotation = unit.AppliedRotation;

                if (!hasThisVisual)
                {
                    var renderer = unit.Instance.GetComponentInChildren<Renderer>();
                    if (renderer != null) renderer.material.color = color;
                }
            }

            ApplyUnitLayout(stack.Type, location);
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
                var unit = _units[i];
                float unitScale = ResolveScale(type, unit.IsOpened ? FoodVisualState.Opened : FoodVisualState.Closed, location);
                // Drinks stack in reverse so the bottom model renders in front.
                float yOffset = isDrink ? (_units.Count - 1 - i) * spacing : i * spacing;
                unit.Instance.transform.localPosition = new Vector3(0f, yOffset, 0f);
                unit.Instance.transform.localScale = unit.BaseScale * unitScale;
            }
        }

        // Units sit one rendered height apart, measured off the bottom unit (Closed unless it is the last
        // piece) so the pile's spacing doesn't jump as the top unit's mesh changes.
        private float ResolveSpacing(FoodType type, FoodLocation location)
        {
            if (_units.Count == 0 || _placement == null) return FallbackStackSpacing;

            var bottom = _units[0];
            float scale = ResolveScale(type, bottom.IsOpened ? FoodVisualState.Opened : FoodVisualState.Closed, location);
            float unitHeight = FoodUnitBounds.ResolveUnitHeight(bottom.SourcePrefab, bottom.AppliedRotation, bottom.BaseScale * scale);
            if (unitHeight <= 0.0001f) return FallbackStackSpacing;

            return unitHeight * _placement.ResolveStackSpacingFactor(type, location);
        }

        private float ResolveScale(FoodType type, FoodVisualState state, FoodLocation location)
        {
            float baseScale = _placement == null ? 1f : _placement.ResolveScale(type, location, state);
            if (location == FoodLocation.Queue && _tableSettings != null && _tableSettings.ScaleWithCamera && _cameraController != null)
            {
                float zoom = _cameraController.CameraZoomFactor;
                float multiplier = _tableSettings.FoodQueueScaleMultiplier;
                baseScale *= 1f + (zoom - 1f) * multiplier;
            }
            return baseScale;
        }
    }
}
