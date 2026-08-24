using System.Linq;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Repositions the decorative tray-dispenser prop onto the current level's Gate cell every time the
    // level initializes. GameplayScene is shared across levels with different Gate placements, so a
    // hand-placed transform would be wrong on any level but the one it was last positioned for.
    public class TrayDispenserAnchor : IInitializable
    {
        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ConveyorTuning _tuning;
        private readonly Transform _dispenserRoot;

        // The prop's own authored orientation (e.g. an fbx-import "lie flat -> stand up" correction, plus
        // whatever decorative tilt it was modeled with) — captured once so re-facing it per level never
        // loses that correction. Every tray pooled from this dispenser rests at this same rotation, just
        // re-faced the same way, so a freshly created Tray.prefab should start out matching it visually.
        private Quaternion _baseRotation = Quaternion.identity;
        private bool _baseRotationCaptured;

        public Vector3 SpawnPosition { get; private set; }
        public Quaternion SpawnRotation { get; private set; }

        public TrayDispenserAnchor(
            ConveyorLevelDataSO level,
            ConveyorPrefabSettings prefabSettings,
            ConveyorTuning tuning,
            Transform dispenserRoot)
        {
            _level = level;
            _prefabSettings = prefabSettings;
            _tuning = tuning;
            _dispenserRoot = dispenserRoot;
        }

        public void Initialize()
        {
            CaptureBaseRotationOnce();

            var gateCell = _level.ConveyorCells.FirstOrDefault(c => c.IsGate);
            if (!gateCell.IsGate)
            {
                Debug.LogError("[TrayDispenserAnchor] Level has no Gate cell — dispenser left at its scene position.");
                SpawnPosition = _dispenserRoot != null ? _dispenserRoot.position : Vector3.zero;
                SpawnRotation = _dispenserRoot != null ? _dispenserRoot.rotation : Quaternion.identity;
                return;
            }

            var position = ConveyorWorldUtil.ToWorldPosition(gateCell.Coord, _prefabSettings.CellWorldSize);
            position.y = _tuning.TrayDispenserHeight;

            float yaw = ConveyorWorldUtil.ToWorldYRotation(gateCell.Shape, gateCell.Rotation);
            var rotation = Quaternion.Euler(0f, yaw, 0f) * _baseRotation;

            SpawnPosition = position;
            SpawnRotation = rotation;

            if (_dispenserRoot != null) _dispenserRoot.SetPositionAndRotation(position, rotation);
        }

        private void CaptureBaseRotationOnce()
        {
            if (_baseRotationCaptured) return;

            _baseRotationCaptured = true;
            if (_dispenserRoot != null) _baseRotation = _dispenserRoot.rotation;
        }
    }
}
