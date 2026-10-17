using System.Collections.Generic;
using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Furniture under the exit stack: the pooled base rack spanning the base slots and the decorative
    // extra rack added by the Extra Tray power-up, both themed per level. Owned by ExitStackAnchor.
    public class ExitRackVisuals
    {
        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorTuning _tuning;
        private readonly ExitSlotController.Pool _rackPool;
        private readonly ExitSlotPrefabSettings _exitSlotPrefabSettings;
        private readonly ThemeSettings _themeSettings;
        private readonly List<ExitSlotController> _spawnedRacks = new();
        private readonly List<GameObject> _extraRackVisuals = new();

        public ExitRackVisuals(
            ConveyorLevelDataSO level,
            ConveyorTuning tuning,
            ExitSlotController.Pool rackPool,
            ExitSlotPrefabSettings exitSlotPrefabSettings,
            ThemeSettings themeSettings)
        {
            _level = level;
            _tuning = tuning;
            _rackPool = rackPool;
            _exitSlotPrefabSettings = exitSlotPrefabSettings;
            _themeSettings = themeSettings;
        }

        // One rack piece spans every slot from firstSlot to lastSlot; food still docks per slot on top of it.
        public void SpawnBaseRack(Vector3 firstSlot, Vector3 lastSlot)
        {
            var center = (firstSlot + lastSlot) * 0.5f;
            var theme = _themeSettings != null && _level != null ? _themeSettings.GetTheme(_level.Theme) : null;

            var rack = _rackPool.Spawn(center);
            rack.transform.rotation = Quaternion.Euler(_tuning.ExtraRackVisualRotation);

            // Always applied: the pooled rack outlives the scene, null restores the base look.
            rack.ApplyThemeVisual(theme != null ? theme.RackPrefab : null);

            _spawnedRacks.Add(rack);
        }

        // The base rack is fixed-length, so an extra slot gets its own rack piece that pops in.
        public void SpawnExtraRack(Vector3 slotPosition)
        {
            var theme = _themeSettings != null && _level != null ? _themeSettings.GetTheme(_level.Theme) : null;
            var extraRackPrefab = theme != null && theme.ExtraRackPrefab != null
                ? theme.ExtraRackPrefab
                : _exitSlotPrefabSettings.ExtraRackPrefab;

            if (extraRackPrefab == null)
            {
                Debug.LogWarning("[ExitRackVisuals] ExtraRackPrefab is unassigned, the extra slot has no rack visual.");
                return;
            }

            var visual = Object.Instantiate(
                extraRackPrefab,
                slotPosition + _tuning.ExtraRackVisualOffset,
                Quaternion.Euler(_tuning.ExtraRackVisualRotation));
            _extraRackVisuals.Add(visual);

            var rackScale = visual.transform.localScale;
            visual.transform.localScale = Vector3.zero;
            visual.transform.DOScale(rackScale, 0.45f).SetEase(Ease.OutBack, 2.2f).SetTarget(visual.transform);
        }

        public void Clear()
        {
            foreach (var rack in _spawnedRacks) _rackPool.Despawn(rack);
            _spawnedRacks.Clear();

            foreach (var visual in _extraRackVisuals)
                if (visual != null) Object.Destroy(visual);
            _extraRackVisuals.Clear();
        }
    }
}
