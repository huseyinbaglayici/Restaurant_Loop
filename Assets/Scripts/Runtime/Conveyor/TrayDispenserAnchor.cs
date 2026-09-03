using System.Collections.Generic;
using System.Linq;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public readonly struct TraySlotPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;

        public TraySlotPose(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
    }

    // Repositions the tray-dispenser prop onto the current level's Gate cell every time the level
    // initializes (GameplayScene is shared across levels with different Gate placements), and hands out
    // its 5 tray children as a small fixed pool — no separate prefab/pool, the trays ARE the dispenser's
    // own decorative children, which is what makes the dispenser visibly deplete while trays are out.
    //
    // Slot positions are fixed (each tray's own authored pose, index 0 = rightmost/highest priority,
    // last index = leftmost). Slot occupancy is dynamic: TryAcquire always drains the rightmost occupied
    // slot, ReserveReturnSlot always refills the leftmost empty slot — independent scans, not "whatever's
    // adjacent to the current block", so send/return order stays correct no matter how returns interleave.
    public class TrayDispenserAnchor : IInitializable
    {
        private enum SlotState
        {
            Empty,
            Reserved,
            Idle
        }

        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ConveyorTuning _tuning;
        private readonly Transform _dispenserRoot;
        private readonly ThemeSettings _themeSettings;

        private Quaternion _baseRotation = Quaternion.identity;
        private bool _baseRotationCaptured;

        private readonly List<TrayController> _traysHighToLow = new();
        private readonly List<TraySlotPose> _slotPoses = new();
        private readonly List<TrayController> _slotOccupant = new();
        private readonly List<SlotState> _slotState = new();

        private int _idleCount;

        public int TotalCount => _traysHighToLow.Count;
        public int AvailableCount => _idleCount;

        public TrayDispenserAnchor(
            ConveyorLevelDataSO level,
            ConveyorPrefabSettings prefabSettings,
            ConveyorTuning tuning,
            Transform dispenserRoot,
            [Inject(Optional = true)] ThemeSettings themeSettings = null)
        {
            _level = level;
            _prefabSettings = prefabSettings;
            _tuning = tuning;
            _dispenserRoot = dispenserRoot;
            _themeSettings = themeSettings ?? Resources.Load<ThemeSettings>("ThemeSettings");
        }

        public void Initialize()
        {
            CaptureBaseRotationOnce();
            PositionOnGateCell();
            ApplyThemeMaterial();

            _traysHighToLow.Clear();
            if (_dispenserRoot != null)
            {
                _traysHighToLow.AddRange(_dispenserRoot.GetComponentsInChildren<TrayController>(true));
                _traysHighToLow.Reverse();
            }

            _slotPoses.Clear();
            foreach (var tray in _traysHighToLow)
                _slotPoses.Add(new TraySlotPose(tray.InitialLocalPosition, tray.InitialLocalRotation, tray.InitialLocalScale));

            ResetAll();
        }

        private void ApplyThemeMaterial()
        {
            if (_dispenserRoot == null || _themeSettings == null || _level == null) return;
            var theme = _themeSettings.GetTheme(_level.Theme);
            if (theme == null || theme.TrayDispenserMaterial == null) return;

            var renderers = _dispenserRoot.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                r.sharedMaterial = theme.TrayDispenserMaterial;
            }
        }

        public void ResetAll()
        {
            _slotOccupant.Clear();
            _slotOccupant.AddRange(_traysHighToLow);

            _slotState.Clear();
            for (int i = 0; i < _traysHighToLow.Count; i++)
                _slotState.Add(SlotState.Idle);

            _idleCount = _traysHighToLow.Count;

            foreach (var tray in _traysHighToLow)
                tray?.SnapToInitialPose();
        }

        private void PositionOnGateCell()
        {
            var gateCell = _level.ConveyorCells.FirstOrDefault(c => c.IsGate);
            if (!gateCell.IsGate)
            {
                Debug.LogError("[TrayDispenserAnchor] Level has no Gate cell — dispenser left at its scene position.");
                return;
            }

            var beltOrigin = ConveyorWorldUtil.ComputeBeltOrigin(_level, _tuning, _prefabSettings.CellWorldSize);
            var position = ConveyorWorldUtil.ToWorldPosition(gateCell.Coord, _prefabSettings.CellWorldSize, 0f, beltOrigin); // Y overwritten below
            position.y = _tuning.TrayDispenserHeight;

            float yaw = ConveyorWorldUtil.ToWorldYRotation(gateCell.Shape, gateCell.Rotation);
            var rotation = Quaternion.Euler(0f, yaw, 0f) * _baseRotation;

            position += Quaternion.Euler(0f, yaw, 0f) * _tuning.TrayDispenserOffset;

            if (_dispenserRoot != null) _dispenserRoot.SetPositionAndRotation(position, rotation);
        }

        private void CaptureBaseRotationOnce()
        {
            if (_baseRotationCaptured) return;

            _baseRotationCaptured = true;
            if (_dispenserRoot != null) _baseRotation = _dispenserRoot.rotation;
        }

        public bool TryAcquire(out TrayController tray)
        {
            for (int i = 0; i < _slotState.Count; i++)
            {
                if (_slotState[i] != SlotState.Idle) continue;

                tray = _slotOccupant[i];
                _slotOccupant[i] = null;
                _slotState[i] = SlotState.Empty;
                _idleCount--;
                return true;
            }

            tray = null;
            return false;
        }

        public TraySlotPose ReserveReturnSlot(TrayController tray)
        {
            for (int i = _slotState.Count - 1; i >= 0; i--)
            {
                if (_slotState[i] != SlotState.Empty) continue;

                _slotOccupant[i] = tray;
                _slotState[i] = SlotState.Reserved;
                return _slotPoses[i];
            }

            // Every TryAcquire is supposed to be paired with exactly one of these calls, so an Empty slot
            // for this tray should always exist — landing here means that invariant already broke somewhere
            // upstream. Returning default() used to be the actual bug: an all-zero pose sends the tray to
            // world origin with zero scale, and since it was never written into _slotOccupant, ConfirmReturn
            // can't find it either — _idleCount never increments and the tray is gone from the pool for the
            // rest of the level (which is exactly what "tray never came back" looked like). Recovering into
            // the tray's own home slot — reasserting it, whatever stale entry was there — is what keeps a
            // single bookkeeping slip from permanently costing a tray instead of just needing this warning.
            int homeIndex = _traysHighToLow.IndexOf(tray);
            if (homeIndex < 0) return default;

            Debug.LogWarning("[TrayDispenserAnchor] ReserveReturnSlot found no Empty slot — dispenser " +
                "bookkeeping is out of sync. Recovered by reclaiming the tray's own home slot.");
            _slotOccupant[homeIndex] = tray;
            _slotState[homeIndex] = SlotState.Reserved;
            return _slotPoses[homeIndex];
        }

        public void ConfirmReturn(TrayController tray)
        {
            int index = _slotOccupant.IndexOf(tray);
            if (index < 0) return;

            _slotState[index] = SlotState.Idle;
            _idleCount++;
        }

        public bool TryAddExtraTray()
        {
            if (_traysHighToLow.Count == 0 || _dispenserRoot == null) return false;

            var baseTray = _traysHighToLow[0];
            if (baseTray == null) return false;

            var newTrayObj = Object.Instantiate(baseTray.gameObject, _dispenserRoot);
            newTrayObj.name = $"tepsi_{_traysHighToLow.Count + 1}";

            var newTray = newTrayObj.GetComponent<TrayController>();
            if (newTray == null)
            {
                Object.Destroy(newTrayObj);
                return false;
            }

            Vector3 stepOffset = _slotPoses.Count > 1
                ? _slotPoses[0].Position - _slotPoses[1].Position
                : new Vector3(0f, 0.05f, 0f);

            var newPose = new TraySlotPose(
                _slotPoses[0].Position + stepOffset,
                _slotPoses[0].Rotation,
                _slotPoses[0].Scale);

            _traysHighToLow.Insert(0, newTray);
            _slotPoses.Insert(0, newPose);
            _slotOccupant.Insert(0, newTray);
            _slotState.Insert(0, SlotState.Idle);
            _idleCount++;

            newTray.transform.localPosition = newPose.Position;
            newTray.transform.localRotation = newPose.Rotation;
            newTray.transform.localScale = newPose.Scale;

            return true;
        }
    }
}
