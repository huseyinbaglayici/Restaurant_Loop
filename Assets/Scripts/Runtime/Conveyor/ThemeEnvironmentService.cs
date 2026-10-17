using System.Linq;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class ThemeEnvironmentService : IInitializable
    {
        private readonly ConveyorTuning _tuning;
        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ThemeSettings _themeSettings;
        private readonly CameraController _cameraController;
        private GameObject _environmentInstance;

        public ThemeEnvironmentService(
            ConveyorTuning tuning,
            ConveyorLevelDataSO level,
            ConveyorPrefabSettings prefabSettings,
            [Inject(Optional = true)] ThemeSettings themeSettings = null,
            [Inject(Optional = true)] CameraController cameraController = null)
        {
            _tuning = tuning;
            _level = level;
            _prefabSettings = prefabSettings;
            _themeSettings = themeSettings ?? Resources.Load<ThemeSettings>("ThemeSettings");
            _cameraController = cameraController;
        }

        public void Initialize()
        {
            if (_tuning == null) return;

            var themeEntry = _themeSettings != null ? _themeSettings.GetTheme(_level.Theme) : null;
            var targetPrefab = themeEntry?.EnvironmentPrefab;

            // 1. Find LevelHolder under GameRoot or anywhere in scene
            var gameRoot = GameObject.Find("GameRoot");
            var levelHolder = gameRoot != null ? gameRoot.transform.Find("LevelHolder")?.gameObject : GameObject.Find("LevelHolder");

            if (levelHolder != null)
            {
                if (targetPrefab != null)
                {
                    foreach (Transform child in levelHolder.transform)
                    {
                        if (child.name == "Plane") continue;

                        bool match = child.name == targetPrefab.name || 
                                     child.name.StartsWith(targetPrefab.name) || 
                                     targetPrefab.name.StartsWith(child.name);

                        child.gameObject.SetActive(match);
                        if (match && _environmentInstance == null)
                        {
                            _environmentInstance = child.gameObject;
                        }
                    }
                }

                // If targetPrefab was not found among LevelHolder children, instantiate it under LevelHolder
                if (_environmentInstance == null && targetPrefab != null)
                {
                    // Deactivate existing environment children in LevelHolder
                    foreach (Transform child in levelHolder.transform)
                    {
                        if (child.name == "Plane") continue;
                        child.gameObject.SetActive(false);
                    }

                    _environmentInstance = Object.Instantiate(targetPrefab, levelHolder.transform);
                    _environmentInstance.name = targetPrefab.name;
                    _environmentInstance.SetActive(true);
                }
            }

            // Fallback: If no LevelHolder or targetPrefab not in hierarchy, instantiate standalone
            if (_environmentInstance == null && targetPrefab != null)
            {
                _environmentInstance = Object.Instantiate(targetPrefab, _tuning.EnvironmentBasePosition, Quaternion.identity);
                _environmentInstance.name = targetPrefab.name;
                _environmentInstance.SetActive(true);
            }

            if (_environmentInstance == null) return;

            var themeOffset = themeEntry != null ? themeEntry.EnvironmentOffset : Vector3.zero;
            var themeScale = themeEntry != null && themeEntry.EnvironmentScale != Vector3.zero ? themeEntry.EnvironmentScale : Vector3.one;
            var themeRot = themeEntry != null ? themeEntry.EnvironmentRotation : Vector3.zero;

            _environmentInstance.transform.localScale = Vector3.Scale(_tuning.EnvironmentBaseScale, themeScale);
            _environmentInstance.transform.localRotation = Quaternion.Euler(themeRot);

            // 1. Izgara yuksekligi arttiginda konveyorun fiziksel uzama boyu ile dengeli Z otelenmesi
            float gridHeightDiff = _level.GridHeight - _tuning.ReferenceGridHeight;
            float beltPhysicalExpansionZ = gridHeightDiff * (_prefabSettings.CellWorldSize * 0.65f);

            // 2. Kamera zoom-out yaptiginda dengeli kadraj payi
            float cameraZoomPushZ = 0f;
            if (_cameraController != null && _cameraController.CameraZoomFactor > 1f)
            {
                float zoomDiff = _cameraController.CameraZoomFactor - 1f;

                float zShiftMult = themeEntry != null && themeEntry.CameraZOffsetMultiplier != 0f
                    ? themeEntry.CameraZOffsetMultiplier
                    : (themeEntry != null && themeEntry.CameraZoomZShiftMultiplier != 0f 
                        ? themeEntry.CameraZoomZShiftMultiplier 
                        : 1.2f);

                cameraZoomPushZ = zoomDiff * zShiftMult;
            }

            var pos = _environmentInstance.transform.position;
            pos.x = _tuning.WorldOrigin.x + _tuning.EnvironmentBasePosition.x + themeOffset.x;
            pos.y = _tuning.EnvironmentBasePosition.y + themeOffset.y;
            pos.z = _tuning.EnvironmentBasePosition.z + themeOffset.z + beltPhysicalExpansionZ + cameraZoomPushZ;
            _environmentInstance.transform.position = pos;
        }
    }
}
