using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Logical belt: where every item is along the path, which cells it crossed this frame, and what docks,
    // loops or gets consumed. Never moves a transform; ConveyorVisualService reacts to its signals.
    public class ConveyorManager : IInitializable, ITickable, IDisposable
    {
        private class BeltItem
        {
            public int Id;
            public FoodItemController Visual;
            public float Progress;
            public int LastCrossedIndex;

            // Waiting at the belt end for a free exit-stack slot; the overflow fail was already reported.
            public bool OverflowReported;
        }

        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorTuning _tuning;
        private readonly FoodItemController.Pool _foodPool;
        private readonly ExitStackAnchor _exitStackAnchor;
        private readonly BeltPath _path;
        private readonly BeltEntryGate _entryGate;
        private readonly SignalBus _signalBus;

        private readonly List<BeltItem> _items = new();

        private readonly List<(int PathIndex, FoodItemController Visual)> _crossingsThisFrame = new();
        public IReadOnlyList<(int PathIndex, FoodItemController Visual)> CrossingsThisFrame => _crossingsThisFrame;

        private int _nextItemId;
        private bool _authoredEntitiesPending;

        // Belt items only (GetVisualAt indexes these). Capacity checks should use ReservedItemCount.
        public int ItemCount => _items.Count;

        // Belt items plus items hovering above the entry, so rapid clicks can't reserve more trays than exist.
        public int ReservedItemCount => _items.Count + _entryGate.PendingItemCount;

        // Shared with ConveyorVisualService so both layers run at the same speed.
        public float SpeedMultiplier { get; set; } = 1f;

        // Items reaching the belt end loop back to the entry instead of docking. Never cleared.
        public bool LoopModeActive { get; private set; }

        public void EnableLoopMode() => LoopModeActive = true;

        public ConveyorManager(
            ConveyorLevelDataSO level,
            ConveyorTuning tuning,
            FoodItemController.Pool foodPool,
            ExitStackAnchor exitStackAnchor,
            BeltPath path,
            BeltEntryGate entryGate,
            SignalBus signalBus)
        {
            _level = level;
            _tuning = tuning;
            _foodPool = foodPool;
            _exitStackAnchor = exitStackAnchor;
            _path = path;
            _entryGate = entryGate;
            _signalBus = signalBus;
        }

        public void Initialize()
        {
            _signalBus.Subscribe<FoodBeltEntryLandedSignal>(OnFoodBeltEntryLanded);
            _signalBus.Subscribe<LevelResumedSignal>(OnLevelResumed);

            var orderedPath = _level.BakedPath.OrderedPath;
            if (orderedPath.Count == 0)
            {
                Debug.LogError(
                    "[ConveyorManager] Level has no baked path — save it in the level editor (with a Gate set) first.");
                return;
            }

            _authoredEntitiesPending = _level.ConveyorEntities.Count > 0;
        }

        // First tick rather than Initialize, so the visual and tray services (bound later) are subscribed.
        private void SpawnAuthoredEntities()
        {
            _authoredEntitiesPending = false;
            var orderedPath = _level.BakedPath.OrderedPath;
            foreach (var entity in _level.ConveyorEntities)
            {
                int index = orderedPath.IndexOf(entity.Coord);
                if (index < 0)
                {
                    Debug.LogError($"[ConveyorManager] Entity at {entity.Coord} isn't on the baked path — skipped.");
                    continue;
                }

                var position = _path.GetPosition(index);
                var visual = _foodPool.Spawn(position, entity.Stack, FoodVisualState.Opened);
                visual.SetLocation(FoodLocation.Conveyor);
                AddItem(entity.Stack, visual, index, position, playEntryFlight: false);
            }
        }

        public void Dispose()
        {
            _signalBus.TryUnsubscribe<FoodBeltEntryLandedSignal>(OnFoodBeltEntryLanded);
            _signalBus.TryUnsubscribe<LevelResumedSignal>(OnLevelResumed);

            foreach (var item in _items) _foodPool.Despawn(item.Visual);
            _items.Clear();
            _entryGate.Clear();
        }

        private void OnFoodBeltEntryLanded(FoodBeltEntryLandedSignal signal) => _entryGate.MarkLanded(signal.ItemId);

        // Keep Playing resumed the level: an item still stuck at the belt end must be able to fail it again.
        private void OnLevelResumed()
        {
            for (int i = 0; i < _items.Count; i++) _items[i].OverflowReported = false;
        }

        public IEnumerable<FoodItemController> PendingVisuals() => _entryGate.PendingVisuals();

        public FoodItemController GetVisualAt(int index) => _items[index].Visual;

        public float GetProgressById(int itemId)
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Id == itemId) return _items[i].Progress;

            return 0f;
        }

        public Vector3 SampleWorldPosition(float progress) => _path.Sample(progress, LoopModeActive);

        // Spawns a fresh visual at the belt entry (no travel).
        public void AddItemAtEntry(FoodStackData stack)
        {
            if (_path.Count == 0) return;

            var visual = _foodPool.Spawn(_path.EntryPosition, stack, FoodVisualState.Opened);
            visual.SetLocation(FoodLocation.Conveyor);
            AddItem(stack, visual, 0f, _path.EntryPosition, playEntryFlight: false);
        }

        // Reuses an existing visual (queue or exit-stack slot) and flies it in from where it sits. If the
        // entry is busy the item hovers above it until Tick() lets it in. Linked pairs use AddLinkedPairAtEntry.
        public void AddItemAtEntry(FoodStackData stack, FoodItemController reusedVisual, Vector3 sourceWorldPosition, float entryProgress = 0f)
        {
            if (_path.Count == 0) return;

            if (reusedVisual == null)
            {
                AddItemAtEntry(stack);
                return;
            }

            if (_entryGate.CanEnterNow(LowestBeltProgress(), 0f))
            {
                AddItem(stack, reusedVisual, entryProgress, sourceWorldPosition, playEntryFlight: true);
                return;
            }

            var hoverPosition = _path.EntryPosition + _entryGate.NextHoverLift;
            _entryGate.Hold(new BeltEntryGate.PendingEntry
            {
                Stack = stack,
                Visual = reusedVisual,
                HoverWorldPosition = hoverPosition,
                EntryProgress = entryProgress
            });

            _signalBus.Fire(new FoodQueuedForEntrySignal
            {
                Visual = reusedVisual,
                SourceWorldPosition = sourceWorldPosition,
                HoverWorldPosition = hoverPosition
            });
        }

        // Both halves of a linked pair enter in one step so they can't get split up while the entry is busy.
        // A lands one spacing ahead of the entry cell, B on it.
        public void AddLinkedPairAtEntry(
            FoodStackData stackA, FoodItemController visualA, Vector3 sourceA,
            FoodStackData stackB, FoodItemController visualB, Vector3 sourceB)
        {
            if (_path.Count == 0) return;
            if (visualA == null || visualB == null) return;

            float spacing = _tuning != null && _tuning.LinkedPairBeltSpacing > 0f ? _tuning.LinkedPairBeltSpacing : 1f;

            if (_entryGate.CanEnterNow(LowestBeltProgress(), spacing))
            {
                AddItem(stackA, visualA, spacing, sourceA, playEntryFlight: true, crossFromEntry: true);
                AddItem(stackB, visualB, 0f, sourceB, playEntryFlight: true);
                return;
            }

            // Both hover at the same height above the cells they'll land on, so the pair still reads as linked.
            var lift = _entryGate.NextHoverLift;
            var hoverA = SampleWorldPosition(spacing) + lift;
            var hoverB = _path.EntryPosition + lift;

            var pendingB = new BeltEntryGate.PendingEntry { Stack = stackB, Visual = visualB, HoverWorldPosition = hoverB, EntryProgress = 0f };
            var pendingA = new BeltEntryGate.PendingEntry { Stack = stackA, Visual = visualA, HoverWorldPosition = hoverA, EntryProgress = spacing, Partner = pendingB };
            _entryGate.Hold(pendingA);

            _signalBus.Fire(new FoodQueuedForEntrySignal { Visual = visualA, SourceWorldPosition = sourceA, HoverWorldPosition = hoverA });
            _signalBus.Fire(new FoodQueuedForEntrySignal { Visual = visualB, SourceWorldPosition = sourceB, HoverWorldPosition = hoverB });
        }

        private void AddItem(
            FoodStackData stack,
            FoodItemController visual,
            float startProgress,
            Vector3 sourceWorldPosition,
            bool playEntryFlight,
            bool crossFromEntry = false)
        {
            var item = new BeltItem
            {
                Id = _nextItemId++,
                Visual = visual,
                Progress = startProgress,
                // One below the starting cell so the first Tick() fires that cell's crossing too. A pair's
                // leading half starts ahead of the entry but still crosses every cell from 0.
                LastCrossedIndex = crossFromEntry ? -1 : Mathf.FloorToInt(startProgress + 0.5f) - 1
            };
            _items.Add(item);

            if (playEntryFlight) _entryGate.MarkFlying(item.Id);

            _signalBus.Fire(new FoodEnteredBeltSignal
            {
                ItemId = item.Id,
                Visual = visual,
                SourceWorldPosition = sourceWorldPosition,
                StartProgress = startProgress,
                PlayEntryFlight = playEntryFlight
            });
        }

        // Called by CustomerConsumeService once it has processed this frame's crossings.
        public void ClearCrossings() => _crossingsThisFrame.Clear();

        // Teardown only (won level).
        public void ClearBelt()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                _foodPool.Despawn(_items[i].Visual);
                _items.RemoveAt(i);
            }

            // The visual layer cancels in-flight entries silently on BeltClearedSignal, so no landed signal
            // will ever reopen the gate for them.
            _entryGate.Clear();

            _signalBus.Fire(new BeltClearedSignal());
        }

        public void Tick()
        {
            if (_path.Count == 0) return;
            if (_authoredEntitiesPending) SpawnAuthoredEntities();

            float delta = _tuning.BeltSpeed * SpeedMultiplier * Time.deltaTime;
            float lastIndex = _path.Count - 1;
            int finalIndex = Mathf.FloorToInt(lastIndex);

            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];
                if (IsHeldByEntryFlight(item)) continue;

                item.Progress += delta;

                if (item.Progress >= lastIndex)
                {
                    HandleBeltEnd(item, i, lastIndex, finalIndex);
                    continue;
                }

                // Crossing fires at cell center, matching TryPeekTypeAt's RoundToInt.
                RecordCrossings(item, Mathf.FloorToInt(item.Progress + 0.5f));
            }

            // Adding one closes the gate again (its entry flight is in progress), so at most one entry
            // (or one linked pair) gets in per frame.
            while (_entryGate.TryRelease(LowestBeltProgress(), out var pending))
            {
                AddItem(pending.Stack, pending.Visual, pending.EntryProgress, pending.HoverWorldPosition, playEntryFlight: true,
                    crossFromEntry: pending.Partner != null);

                // The partner goes in unconditionally; re-checking the gate would always fail and split the pair.
                if (pending.Partner != null)
                    AddItem(pending.Partner.Stack, pending.Partner.Visual, pending.Partner.EntryProgress, pending.Partner.HoverWorldPosition, playEntryFlight: true);
            }
        }

        // Progress stays at its entry value while the item (or its linked partner) is still flying in,
        // mirroring ConveyorVisualService, so the visual lands exactly where the logical item is.
        private bool IsHeldByEntryFlight(BeltItem item)
        {
            if (_entryGate.IsFlying(item.Id)) return true;
            if (item.Visual == null || item.Visual.Stack.LinkId == 0) return false;

            int linkId = item.Visual.Stack.LinkId;
            for (int j = 0; j < _items.Count; j++)
            {
                if (_items[j].Visual != null && _items[j].Visual.Stack.LinkId == linkId && _entryGate.IsFlying(_items[j].Id))
                    return true;
            }

            return false;
        }

        private void HandleBeltEnd(BeltItem item, int index, float lastIndex, int finalIndex)
        {
            // First frame at the end: fire the final-cell crossing and wait one tick, so the consume check
            // gets its shot at the last cell before the item docks.
            if (item.LastCrossedIndex < finalIndex)
            {
                RecordCrossings(item, finalIndex);
                item.Progress = lastIndex;
                return;
            }

            if (LoopModeActive)
            {
                var stack = item.Visual.Stack;
                var visual = item.Visual;
                var endPosition = SampleWorldPosition(item.Progress);

                _items.RemoveAt(index);
                _signalBus.Fire(new FoodLoopedSignal { ItemId = item.Id });
                AddItemAtEntry(stack, visual, endPosition);
                return;
            }

            if (_exitStackAnchor.TryDock(item.Visual, out int slotIndex))
            {
                _signalBus.Fire(new FoodDockedSignal
                {
                    ItemId = item.Id,
                    Visual = item.Visual,
                    SlotIndex = slotIndex,
                    SlotWorldPosition = _exitStackAnchor.GetFoodPosition(slotIndex)
                });

                _items.RemoveAt(index);
                return;
            }

            // No free slot: the item waits at the belt end (despawning it would make the level unwinnable)
            // and docks once a slot frees up. The fail is reported once per stall.
            item.Progress = lastIndex;
            if (!item.OverflowReported)
            {
                item.OverflowReported = true;
                _signalBus.Fire(new ExitStackOverflowSignal { ItemId = item.Id });
            }
        }

        private void RecordCrossings(BeltItem item, int upToIndex)
        {
            for (int idx = Mathf.Max(0, item.LastCrossedIndex + 1); idx <= upToIndex; idx++)
                _crossingsThisFrame.Add((idx, item.Visual));
            item.LastCrossedIndex = upToIndex;
        }

        private float LowestBeltProgress()
        {
            float lowest = float.MaxValue;
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Progress < lowest) lowest = _items[i].Progress;

            return lowest;
        }

        public bool TryPeekTypeAt(int pathIndex, out FoodType type)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Visual != null && _items[i].Visual.Stack.Amount <= 0) continue;

                int itemIndex = Mathf.RoundToInt(_items[i].Progress) % _path.Count;
                if (itemIndex != pathIndex) continue;

                type = _items[i].Visual.Stack.Type;
                return true;
            }

            type = default;
            return false;
        }

        // On a full take the item leaves the belt but its visual is NOT despawned: ConveyorVisualService
        // takes ownership and flies it to the customer.
        public int ConsumeFromItem(
            FoodItemController visual,
            int amountWanted,
            out int itemId,
            out bool itemFullyConsumed,
            out int? partnerItemId,
            out FoodItemController partnerVisual)
        {
            partnerItemId = null;
            partnerVisual = null;
            itemFullyConsumed = false;

            int index = IndexOfVisual(visual);
            if (index < 0)
            {
                itemId = -1;
                return 0;
            }

            itemId = _items[index].Id;
            int amountTaken = Mathf.Min(amountWanted, visual.Stack.Amount);
            if (amountTaken <= 0) return 0;

            if (visual.Stack.LinkId != 0)
            {
                visual.ReduceAmount(amountTaken);
                itemFullyConsumed = TryRemoveEmptyLinkedPair(visual, out partnerItemId, out partnerVisual);
                return amountTaken;
            }

            itemFullyConsumed = amountTaken >= visual.Stack.Amount;

            if (itemFullyConsumed) _items.RemoveAt(index);
            else visual.ReduceAmount(amountTaken);

            return amountTaken;
        }

        private int IndexOfVisual(FoodItemController visual)
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Visual == visual) return i;

            return -1;
        }

        // A linked half can reach 0 and keep riding; both leave the belt only once both are empty.
        private bool TryRemoveEmptyLinkedPair(FoodItemController visual, out int? partnerItemId, out FoodItemController partnerVisual)
        {
            partnerItemId = null;
            partnerVisual = null;

            int linkId = visual.Stack.LinkId;
            for (int j = 0; j < _items.Count; j++)
            {
                if (_items[j].Visual != null && _items[j].Visual.Stack.LinkId == linkId && _items[j].Visual.Stack.Amount > 0)
                    return false;
            }

            for (int j = 0; j < _items.Count; j++)
            {
                if (_items[j].Visual != visual && _items[j].Visual != null && _items[j].Visual.Stack.LinkId == linkId)
                {
                    partnerItemId = _items[j].Id;
                    partnerVisual = _items[j].Visual;
                    break;
                }
            }

            // The half that emptied first may already have docked; release its exit-stack slot too.
            if (partnerVisual == null) partnerVisual = TakeEmptyLinkedHalfFromExitStack(linkId, visual);

            _items.RemoveAll(item => item.Visual != null && item.Visual.Stack.LinkId == linkId);
            return true;
        }

        private FoodItemController TakeEmptyLinkedHalfFromExitStack(int linkId, FoodItemController exclude)
        {
            for (int i = 0; i < _exitStackAnchor.SlotCount; i++)
            {
                if (!_exitStackAnchor.TryGetOccupant(i, out var occupant)) continue;
                if (occupant == exclude || occupant.Stack.LinkId != linkId || occupant.Stack.Amount > 0) continue;

                _exitStackAnchor.TryRemoveFromSlot(i, out occupant);
                _exitStackAnchor.CompactSlots();
                return occupant;
            }

            return null;
        }
    }
}
