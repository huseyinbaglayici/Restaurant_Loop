using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Shows the level theme's environment prefab under LevelHolder and places it for the grid size and camera zoom.
    public class ThemeEnvironmentService : IInitializable
    {
        private readonly ConveyorTuning _tuning;
        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ThemeSettings _themeSettings;
        private readonly CameraController _cameraController;
        private readonly Transform _levelHolder;
        private GameObject _environmentInstance;

        public ThemeEnvironmentService(
            ConveyorTuning tuning,
            ConveyorLevelDataSO level,
            ConveyorPrefabSettings prefabSettings,
            ThemeSettings themeSettings,
            CameraController cameraController,
            [Inject(Id = "LevelHolder")] Transform levelHolder)
        {
            _tuning = tuning;
            _level = level;
            _prefabSettings = prefabSettings;
            _themeSettings = themeSettings;
            _cameraController = cameraController;
            _levelHolder = levelHolder;
        }

        public void Initialize()
        {
            if (_tuning == null) return;

            var themeEntry = _themeSettings != null ? _themeSettings.GetTheme(_level.Theme) : null;
            var targetPrefab = themeEntry?.EnvironmentPrefab;

            if (targetPrefab != null)
            {
                // Reuse a matching environment already under LevelHolder, hide the others
                foreach (Transform child in _levelHolder)
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

                if (_environmentInstance == null)
                {
                    _environmentInstance = Object.Instantiate(targetPrefab, _levelHolder);
                    _environmentInstance.name = targetPrefab.name;
                    _environmentInstance.SetActive(true);
                }
            }

            if (_environmentInstance == null) return;

            var themeOffset = themeEntry != null ? themeEntry.EnvironmentOffset : Vector3.zero;
            var themeScale = themeEntry != null && themeEntry.EnvironmentScale != Vector3.zero ? themeEntry.EnvironmentScale : Vector3.one;
            var themeRot = themeEntry != null ? themeEntry.EnvironmentRotation : Vector3.zero;

            _environmentInstance.transform.localScale = Vector3.Scale(_tuning.EnvironmentBaseScale, themeScale);
            _environmentInstance.transform.localRotation = Quaternion.Euler(themeRot);

            // Taller grids stretch the belt, so the environment moves back with it.
            float gridHeightDiff = _level.GridHeight - _tuning.ReferenceGridHeight;
            float beltPhysicalExpansionZ = gridHeightDiff * (_prefabSettings.CellWorldSize * 0.65f);

            // A zoomed-out camera needs the environment pushed further back to stay framed.
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
