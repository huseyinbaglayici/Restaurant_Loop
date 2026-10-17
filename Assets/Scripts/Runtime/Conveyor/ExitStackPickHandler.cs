using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Audio;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Signals;
using RestaurantLoop.Runtime.Tutorial;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Exit-stack pick rules: which rack occupant a click hits, whether it may leave (not still flying in
    // or settling after its dock, tutorial gate, free trays, linked partner) and sending it back onto the belt.
    public class ExitStackPickHandler : IInitializable, IDisposable
    {
        private readonly ExitStackAnchor _exitStackAnchor;
        private readonly ConveyorManager _conveyorManager;
        private readonly TrayDispenserAnchor _trayAnchor;
        private readonly ConveyorTuning _tuning;
        private readonly FoodFlightService _flightService;
        private readonly AudioService _audioService;
        private readonly AudioLibrary _audioLibrary;
        private readonly TutorialInputGate _gate;
        private readonly SignalBus _signalBus;

        private readonly Dictionary<FoodItemController, float> _dockSettleUntil = new();

        public ExitStackPickHandler(
            ExitStackAnchor exitStackAnchor,
            ConveyorManager conveyorManager,
            TrayDispenserAnchor trayAnchor,
            ConveyorTuning tuning,
            FoodFlightService flightService,
            AudioService audioService,
            AudioLibrary audioLibrary,
            TutorialInputGate gate,
            SignalBus signalBus)
        {
            _exitStackAnchor = exitStackAnchor;
            _conveyorManager = conveyorManager;
            _trayAnchor = trayAnchor;
            _tuning = tuning;
            _flightService = flightService;
            _audioService = audioService;
            _audioLibrary = audioLibrary;
            _gate = gate;
            _signalBus = signalBus;
        }

        public void Initialize()
        {
            _signalBus.Subscribe<FoodDockLandedSignal>(OnFoodDockLanded);
        }

        public void Dispose()
        {
            _signalBus.TryUnsubscribe<FoodDockLandedSignal>(OnFoodDockLanded);
            _dockSettleUntil.Clear();
        }

        private void OnFoodDockLanded(FoodDockLandedSignal signal)
        {
            if (signal.Visual != null)
            {
                float cooldown = _tuning != null && _tuning.ExitRackSendCooldown > 0f ? _tuning.ExitRackSendCooldown : 0.35f;
                _dockSettleUntil[signal.Visual] = Time.time + cooldown;
            }
        }

        public bool TryHandleClick(Camera camera, Vector2 screenPoint)
        {
            int bestSlot = -1;
            float bestDistance = _tuning.ClickRadiusPixels;

            for (int i = 0; i < _exitStackAnchor.SlotPositions.Length; i++)
            {
                if (!_exitStackAnchor.TryGetOccupant(i, out var occupant)) continue;
                if (!CanPick(occupant)) continue;

                float distance = ConveyorPointer.ScreenDistance(camera, occupant.VisualCenter, screenPoint);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                bestSlot = i;
            }

            if (bestSlot < 0) return false;
            if (!_gate.CanClickExitStack()) return true;

            // Sending from the rack must not exceed the total tray capacity either
            int maxTrays = _trayAnchor.TotalCount;
            int currentReservedOrOnBelt = _conveyorManager.ReservedItemCount;
            if (currentReservedOrOnBelt >= maxTrays)
            {
                _signalBus.Fire<TrayDispenserRejectedSignal>();
                return false;
            }

            _audioService.PlaySfx(_audioLibrary.FoodClickSfx != null ? _audioLibrary.FoodClickSfx : _audioLibrary.DefaultClickSfx);

            if (_exitStackAnchor.TryGetOccupant(bestSlot, out var clickedOccupant) && clickedOccupant.Stack.LinkId != 0)
            {
                int linkId = clickedOccupant.Stack.LinkId;
                int partnerSlot = -1;
                FoodItemController partnerVisual = null;

                for (int i = 0; i < _exitStackAnchor.SlotPositions.Length; i++)
                {
                    if (i == bestSlot) continue;
                    if (_exitStackAnchor.TryGetOccupant(i, out var occ) && occ.Stack.LinkId == linkId)
                    {
                        partnerSlot = i;
                        partnerVisual = occ;
                        break;
                    }
                }

                if (partnerSlot >= 0 && partnerVisual != null)
                {
                    if (!CanPick(partnerVisual))
                    {
                        return true;
                    }

                    if (currentReservedOrOnBelt + 2 > maxTrays)
                    {
                        _signalBus.Fire<TrayDispenserRejectedSignal>();
                        return false;
                    }

                    _exitStackAnchor.TryRemoveFromSlot(bestSlot, out var v1);
                    _exitStackAnchor.TryRemoveFromSlot(partnerSlot, out var v2);
                    _exitStackAnchor.CompactSlots();
                    _conveyorManager.AddLinkedPairAtEntry(
                        v1.Stack, v1, v1.transform.position,
                        v2.Stack, v2, v2.transform.position);
                    _audioService.PlaySfx(_audioLibrary.FoodSendSfx);
                    return true;
                }
            }

            _exitStackAnchor.TryRemoveFromSlot(bestSlot, out var visual);
            _exitStackAnchor.CompactSlots();
            _conveyorManager.AddItemAtEntry(visual.Stack, visual, visual.transform.position);
            _audioService.PlaySfx(_audioLibrary.FoodSendSfx);
            return true;
        }

        // Still flying in, or landed too recently to be sent straight back out.
        private bool CanPick(FoodItemController occupant)
        {
            if (occupant == null) return false;
            if (_flightService.IsFlying(occupant.transform)) return false;
            if (_dockSettleUntil.TryGetValue(occupant, out float settleTime) && Time.time < settleTime) return false;
            return true;
        }
    }
}
