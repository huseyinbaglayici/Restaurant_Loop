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
        private GameObject _environmentInstance;

        public EnvironmentController(
            ConveyorTuning tuning,
            ConveyorLevelDataSO level,
            ConveyorPrefabSettings prefabSettings)
        {
            _tuning = tuning;
            _level = level;
            _prefabSettings = prefabSettings;
        }

        public void Initialize()
        {
            if (_tuning == null) return;

            // Find existing environment object in scene or spawn from prefab
            var existingEnv = GameObject.Find("Environment") ?? GameObject.Find("Environment_Latest");
            if (existingEnv != null)
            {
                _environmentInstance = existingEnv;
            }
            else if (_tuning.EnvironmentPrefab != null)
            {
                _environmentInstance = Object.Instantiate(
                    _tuning.EnvironmentPrefab,
                    _tuning.EnvironmentBasePosition,
                    Quaternion.Euler(_tuning.EnvironmentBaseRotation));
                _environmentInstance.name = "Environment";
            }

            if (_environmentInstance == null) return;

            _environmentInstance.transform.localScale = _tuning.EnvironmentBaseScale;

            if (_tuning.AutoAlignEnvironment && _level.ConveyorCells.Count > 0)
            {
                float cellSize = _prefabSettings.CellWorldSize;
                var beltOrigin = ConveyorWorldUtil.ComputeBeltOrigin(_level, _tuning, cellSize);

                // Find the highest Y cell of conveyor (maximum Z in world space)
                int maxY = _level.ConveyorCells.Max(c => c.Coord.y);
                float conveyorTopZ = maxY * cellSize + cellSize * 0.5f + beltOrigin.z;

                // Position environment at least EnvironmentMinZGap away from the conveyor top edge
                float targetZ = Mathf.Max(_tuning.EnvironmentBasePosition.z, conveyorTopZ + _tuning.EnvironmentMinZGap) + _tuning.EnvironmentExtraZOffset;

                // Center environment on X (WorldOrigin.x) so it aligns with camera viewport
                var pos = _environmentInstance.transform.position;
                pos.x = _tuning.WorldOrigin.x + _tuning.EnvironmentBasePosition.x;
                pos.y = _tuning.EnvironmentBasePosition.y;
                pos.z = targetZ;
                _environmentInstance.transform.position = pos;
            }
            else
            {
                var pos = _tuning.EnvironmentBasePosition;
                pos.x += _tuning.WorldOrigin.x;
                _environmentInstance.transform.position = pos;
            }
        }
    }
}
