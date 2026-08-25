using System.Collections.Generic;
using System.Linq;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Repositions the tray-dispenser prop onto the current level's Gate cell every time the level
    // initializes (GameplayScene is shared across levels with different Gate placements), and hands out
    // its 5 tray children as a small fixed pool — no separate prefab/pool, the trays ARE the dispenser's
    // own decorative children, which is what makes the dispenser visibly deplete while trays are out.
    public class TrayDispenserAnchor : IInitializable
    {
        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ConveyorTuning _tuning;
        private readonly Transform _dispenserRoot;

        // The prop's own authored orientation (e.g. an fbx-import "lie flat -> stand up" correction) —
        // captured once so re-facing it per level never loses that correction.
        private Quaternion _baseRotation = Quaternion.identity;
        private bool _baseRotationCaptured;

        // Fixed priority order, always drained from the same end: prefer the LAST tray in hierarchy
        // ("tepsi (4)"), then (3), (2), (1), then the first ("tepsi") — whichever of those is currently
        // free. This is deliberately not a round-robin: a tray that just came back is immediately
        // top-priority again if it's the highest-index free one, so the dispenser always reads as
        // draining/refilling from one consistent side instead of alternating which end looks emptiest.
        private readonly List<TrayController> _traysHighToLow = new();
        private readonly HashSet<TrayController> _busy = new();

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
            PositionOnGateCell();

            _traysHighToLow.Clear();
            _busy.Clear();
            if (_dispenserRoot != null)
            {
                _traysHighToLow.AddRange(_dispenserRoot.GetComponentsInChildren<TrayController>(true));
                _traysHighToLow.Reverse(); // hierarchy order is low->high; acquire wants high->low
            }
        }

        private void PositionOnGateCell()
        {
            var gateCell = _level.ConveyorCells.FirstOrDefault(c => c.IsGate);
            if (!gateCell.IsGate)
            {
                Debug.LogError("[TrayDispenserAnchor] Level has no Gate cell — dispenser left at its scene position.");
                return;
            }

            var position = ConveyorWorldUtil.ToWorldPosition(gateCell.Coord, _prefabSettings.CellWorldSize);
            position.y = _tuning.TrayDispenserHeight;

            float yaw = ConveyorWorldUtil.ToWorldYRotation(gateCell.Shape, gateCell.Rotation);
            var rotation = Quaternion.Euler(0f, yaw, 0f) * _baseRotation;

            // Offset expressed in the dispenser's own facing so it stays "sideways/forward relative to the
            // Gate" regardless of which way that Gate happens to face on a given level.
            position += Quaternion.Euler(0f, yaw, 0f) * _tuning.TrayDispenserOffset;

            if (_dispenserRoot != null) _dispenserRoot.SetPositionAndRotation(position, rotation);
        }

        private void CaptureBaseRotationOnce()
        {
            if (_baseRotationCaptured) return;

            _baseRotationCaptured = true;
            if (_dispenserRoot != null) _baseRotation = _dispenserRoot.rotation;
        }

        // false when every tray is already out on the belt (e.g. Extra Tray power-up pushed concurrent
        // items past the dispenser's fixed 5) — callers must treat that as "no tray for this item".
        public bool TryAcquire(out TrayController tray)
        {
            for (int i = 0; i < _traysHighToLow.Count; i++)
            {
                if (_busy.Contains(_traysHighToLow[i])) continue;

                tray = _traysHighToLow[i];
                _busy.Add(tray);
                return true;
            }

            tray = null;
            return false;
        }

        public void Release(TrayController tray)
        {
            if (tray == null) return;
            _busy.Remove(tray);
        }
    }
}
