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
        private GameObject _environmentInstance;

        public EnvironmentController(
            ConveyorTuning tuning,
            ConveyorLevelDataSO level,
            ConveyorPrefabSettings prefabSettings,
            [Inject(Optional = true)] ThemeSettings themeSettings = null)
        {
            _tuning = tuning;
            _level = level;
            _prefabSettings = prefabSettings;
            _themeSettings = themeSettings ?? Resources.Load<ThemeSettings>("ThemeSettings");
        }

        public void Initialize()
        {
            if (_tuning == null) return;

            var themeEntry = _themeSettings != null ? _themeSettings.GetTheme(_level.Theme) : null;
            var targetPrefab = _level.Environment != null ? _level.Environment : themeEntry?.EnvironmentPrefab;

            // 1. If scene has a LevelHolder with multiple environment presets, switch them
            var levelHolder = GameObject.Find("LevelHolder");
            if (levelHolder != null && targetPrefab != null)
            {
                foreach (Transform child in levelHolder.transform)
                {
                    bool match = child.name == targetPrefab.name || child.name.StartsWith(targetPrefab.name);
                    child.gameObject.SetActive(match);
                    if (match) _environmentInstance = child.gameObject;
                }
            }

            // 2. If not found in LevelHolder, check if Environment / Environment_Latest exists in scene
            if (_environmentInstance == null)
            {
                _environmentInstance = GameObject.Find("Environment") ?? GameObject.Find("Environment_Latest");
            }

            // 3. If still null, dynamically instantiate from ThemeSettings
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

            if (_tuning.AutoAlignEnvironment && _level.ConveyorCells.Count > 0)
            {
                float cellSize = _prefabSettings.CellWorldSize;
                var beltOrigin = ConveyorWorldUtil.ComputeBeltOrigin(_level, _tuning, cellSize);

                // Find the highest Y cell of conveyor (maximum Z in world space)
                int maxY = _level.ConveyorCells.Max(c => c.Coord.y);
                float conveyorTopZ = maxY * cellSize + cellSize * 0.5f + beltOrigin.z;

                // Position environment at least EnvironmentMinZGap away from the conveyor top edge
                float targetZ = Mathf.Max(_tuning.EnvironmentBasePosition.z, conveyorTopZ + _tuning.EnvironmentMinZGap) + _tuning.EnvironmentExtraZOffset + themeOffset.z;

                // Center environment on X (WorldOrigin.x) so it aligns with camera viewport
                var pos = _environmentInstance.transform.position;
                pos.x = _tuning.WorldOrigin.x + _tuning.EnvironmentBasePosition.x + themeOffset.x;
                pos.y = _tuning.EnvironmentBasePosition.y + themeOffset.y;
                pos.z = targetZ;
                _environmentInstance.transform.position = pos;
            }
            else
            {
                var pos = _tuning.EnvironmentBasePosition + themeOffset;
                pos.x += _tuning.WorldOrigin.x;
                _environmentInstance.transform.position = pos;
            }
        }
    }
}
