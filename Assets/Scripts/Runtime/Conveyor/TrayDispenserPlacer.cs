using System.Linq;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Puts the tray dispenser prop on the level's Gate cell, swapping in the theme's own dispenser prefab
    // (and hiding the scene default) when the theme has one. Owned by TrayDispenserAnchor.
    public class TrayDispenserPlacer
    {
        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ConveyorTuning _tuning;
        private readonly DiContainer _container;
        private readonly ThemeSettings _themeSettings;

        public TrayDispenserPlacer(
            ConveyorLevelDataSO level,
            ConveyorPrefabSettings prefabSettings,
            ConveyorTuning tuning,
            DiContainer container,
            ThemeSettings themeSettings)
        {
            _level = level;
            _prefabSettings = prefabSettings;
            _tuning = tuning;
            _container = container;
            _themeSettings = themeSettings;
        }

        // Returns the dispenser root used for this level: the theme's instance or the scene default.
        public Transform Place(Transform sceneRoot)
        {
            var root = SpawnThemeDispenser(sceneRoot);
            var baseRotation = root != null ? root.rotation : Quaternion.identity;
            PositionOnGateCell(root, baseRotation);
            return root;
        }

        private Transform SpawnThemeDispenser(Transform sceneRoot)
        {
            if (_themeSettings == null || _level == null) return sceneRoot;
            var theme = _themeSettings.GetTheme(_level.Theme);
            if (theme == null || theme.TrayDispenserPrefab == null) return sceneRoot;

            if (sceneRoot != null) sceneRoot.gameObject.SetActive(false);

            var spawned = _container.InstantiatePrefab(theme.TrayDispenserPrefab);
            spawned.name = $"{theme.TrayDispenserPrefab.name}_{_level.Theme}";
            spawned.SetActive(true);
            return spawned.transform;
        }

        private void PositionOnGateCell(Transform root, Quaternion baseRotation)
        {
            var gateCell = _level.ConveyorCells.FirstOrDefault(c => c.IsGate);
            if (!gateCell.IsGate)
            {
                Debug.LogError("[TrayDispenserPlacer] Level has no Gate cell — dispenser left at its scene position.");
                return;
            }

            var beltOrigin = ConveyorWorldUtil.ComputeBeltOrigin(_level, _tuning, _prefabSettings.CellWorldSize);
            var position = ConveyorWorldUtil.ToWorldPosition(gateCell.Coord, _prefabSettings.CellWorldSize, 0f, beltOrigin);
            position.y = _tuning.TrayDispenserHeight;

            float yaw = ConveyorWorldUtil.ToWorldYRotation(gateCell.Shape, gateCell.Rotation);
            var rotation = Quaternion.Euler(0f, yaw, 0f) * baseRotation;

            position += Quaternion.Euler(0f, yaw, 0f) * _tuning.TrayDispenserOffset;

            if (root != null) root.SetPositionAndRotation(position, rotation);
        }
    }
}
