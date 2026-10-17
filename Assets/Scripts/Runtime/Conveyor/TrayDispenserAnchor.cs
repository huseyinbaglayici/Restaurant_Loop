using System.Collections.Generic;
using System.Linq;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // The tray dispenser's trays as a small fixed pool: the trays are the prop's own children, so it visibly
    // depletes while trays are out. Slot index 0 is the rightmost. TryAcquire drains the rightmost idle slot
    // and ReserveReturnSlot refills the leftmost empty one, so order holds however returns interleave.
    public class TrayDispenserAnchor : IInitializable
    {
        private enum SlotState
        {
            Empty,
            Reserved,
            Idle
        }

        private readonly TrayDispenserPlacer _placer;
        private Transform _dispenserRoot;

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
            [Inject(Optional = true, Id = "TrayDispenserRoot")] Transform dispenserRoot,
            DiContainer container,
            ThemeSettings themeSettings)
        {
            _dispenserRoot = dispenserRoot;
            _placer = new TrayDispenserPlacer(level, prefabSettings, tuning, container, themeSettings);
        }

        public void Initialize()
        {
            // GameplayScene is shared across levels with different Gate cells, so the prop is placed per level.
            _dispenserRoot = _placer.Place(_dispenserRoot);

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

        public void ResetAll()
        {
            _slotOccupant.Clear();
            _slotOccupant.AddRange(_traysHighToLow);

            _slotState.Clear();
            for (int i = 0; i < _traysHighToLow.Count; i++)
                _slotState.Add(SlotState.Idle);

            _idleCount = _traysHighToLow.Count;

            for (int i = 0; i < _traysHighToLow.Count; i++)
            {
                var tray = _traysHighToLow[i];
                if (tray == null) continue;
                tray.SnapToInitialPose();
                if (i < _slotPoses.Count)
                {
                    tray.transform.localPosition = _slotPoses[i].Position;
                    tray.transform.localRotation = _slotPoses[i].Rotation;
                    tray.transform.localScale = _slotPoses[i].Scale;
                }
            }
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

            // Every TryAcquire pairs with one of these calls, so an Empty slot should always exist. If the
            // bookkeeping slipped, reclaim the tray's home slot rather than lose the tray for the level.
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
            _idleCount = _slotState.Count(s => s == SlotState.Idle);
        }

        // Extra Tray power-up: clones a tray into a new rightmost slot, one step past the current first slot.
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
