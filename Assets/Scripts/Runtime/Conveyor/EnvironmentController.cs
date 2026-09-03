using System.Linq;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class EnvironmentController : IInitializable
    {
        private readonly ConveyorTuning _tuning;
        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ThemeSettings _themeSettings;
        private readonly CameraController _cameraController;
        private GameObject _environmentInstance;

        public EnvironmentController(
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
            var targetPrefab = _level.Environment != null ? _level.Environment : themeEntry?.EnvironmentPrefab;

            // 1. Find LevelHolder under GameRoot or anywhere in scene
            var gameRoot = GameObject.Find("GameRoot");
            var levelHolder = gameRoot != null ? gameRoot.transform.Find("LevelHolder")?.gameObject : GameObject.Find("LevelHolder");

            if (levelHolder != null && targetPrefab != null)
            {
                foreach (Transform child in levelHolder.transform)
                {
                    bool match = child.name == targetPrefab.name || 
                                 child.name.StartsWith(targetPrefab.name) || 
                                 targetPrefab.name.StartsWith(child.name);

                    child.gameObject.SetActive(match);
                    if (match) _environmentInstance = child.gameObject;
                }
            }

            // 2. If not matched in LevelHolder, check if any active/inactive Environment child exists in LevelHolder
            if (_environmentInstance == null && levelHolder != null)
            {
                foreach (Transform child in levelHolder.transform)
                {
                    if (child.name.ToLower().Contains("env"))
                    {
                        _environmentInstance = child.gameObject;
                        _environmentInstance.SetActive(true);
                        break;
                    }
                }
            }

            // 3. Fallback: Check if Environment / Environment_Latest exists in scene
            if (_environmentInstance == null)
            {
                _environmentInstance = GameObject.Find("Environment") ?? GameObject.Find("Environment_Latest");
            }

            // 4. Fallback: Instantiate from ThemeSettings
            if (_environmentInstance == null && targetPrefab != null)
            {
                _environmentInstance = Object.Instantiate(targetPrefab, _tuning.EnvironmentBasePosition, Quaternion.identity);
                _environmentInstance.name = "Environment";
            }

            if (_environmentInstance == null) return;

            var themeOffset = themeEntry != null ? themeEntry.EnvironmentOffset : Vector3.zero;
            var themeScale = themeEntry != null && themeEntry.EnvironmentScale != Vector3.zero ? themeEntry.EnvironmentScale : Vector3.one;
            var themeRot = themeEntry != null ? themeEntry.EnvironmentRotation : Vector3.zero;

            _environmentInstance.transform.localScale = Vector3.Scale(_tuning.EnvironmentBaseScale, themeScale);
            _environmentInstance.transform.localRotation = Quaternion.Euler(themeRot);

            // Calculate extra Y and Z offset from camera zoom (pushes background back/up when camera zooms out)
            float cameraZoomPushY = 0f;
            float cameraZoomPushZ = 0f;
            if (_cameraController != null && _cameraController.CameraZoomFactor > 1f)
            {
                float zoomDiff = _cameraController.CameraZoomFactor - 1f;

                float yMult = themeEntry != null && themeEntry.CameraYOffsetMultiplier != 0f 
                    ? themeEntry.CameraYOffsetMultiplier 
                    : _tuning.EnvironmentCameraYMultiplier;

                float zMult = themeEntry != null && themeEntry.CameraZOffsetMultiplier != 0f 
                    ? themeEntry.CameraZOffsetMultiplier 
                    : _tuning.EnvironmentCameraZMultiplier;

                cameraZoomPushY = zoomDiff * yMult;
                cameraZoomPushZ = zoomDiff * zMult;
            }

            if (_tuning.AutoAlignEnvironment && _level.ConveyorCells.Count > 0)
            {
                float cellSize = _prefabSettings.CellWorldSize;
                var beltOrigin = ConveyorWorldUtil.ComputeBeltOrigin(_level, _tuning, cellSize);

                // Find the highest Y cell of conveyor (maximum Z in world space)
                int maxY = _level.ConveyorCells.Max(c => c.Coord.y);
                float conveyorTopZ = maxY * cellSize + cellSize * 0.5f + beltOrigin.z;

                // Position environment at least EnvironmentMinZGap away from the conveyor top edge + camera zoom push
                float targetZ = Mathf.Max(_tuning.EnvironmentBasePosition.z, conveyorTopZ + _tuning.EnvironmentMinZGap) 
                    + _tuning.EnvironmentExtraZOffset + themeOffset.z + cameraZoomPushZ;

                // Center environment on X (WorldOrigin.x) so it aligns with camera viewport
                var pos = _environmentInstance.transform.position;
                pos.x = _tuning.WorldOrigin.x + _tuning.EnvironmentBasePosition.x + themeOffset.x;
                pos.y = _tuning.EnvironmentBasePosition.y + themeOffset.y + cameraZoomPushY;
                pos.z = targetZ;
                _environmentInstance.transform.position = pos;
            }
            else
            {
                var pos = _tuning.EnvironmentBasePosition + themeOffset;
                pos.x += _tuning.WorldOrigin.x;
                pos.y += cameraZoomPushY;
                pos.z += cameraZoomPushZ;
                _environmentInstance.transform.position = pos;
            }
        }
    }
}
