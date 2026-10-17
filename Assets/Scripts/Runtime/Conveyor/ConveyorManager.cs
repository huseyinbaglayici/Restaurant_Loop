using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Logical layer: owns where every item is on the baked path, who crosses which cell, and what gets
    // consumed or docked. It never moves a transform — it fires signals the instant a decision happens,
    // and ConveyorVisualService reacts to them the same frame. Entry, mid-belt travel and exit (dock or
    // consume) all run through this one loop — no separate holds or waits for any of them.
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

        // A click that can't enter yet (MinEntrySpacing not clear) — already detached from its queue/exit-
        // stack slot, floating above the entry cell (see FoodQueuedForEntrySignal) until Tick() lets it in.
        private class PendingEntry
        {
            public FoodStackData Stack;
            public FoodItemController Visual;
            public Vector3 HoverWorldPosition;
            public float EntryProgress;

            // Set for a linked pair's first half — the second half, dequeued in the same Tick() so the
            // pair always enters together instead of splitting apart based on however busy the entry
            // happened to be when they were clicked (see AddLinkedPairAtEntry).
            public PendingEntry Partner;
        }

        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ConveyorTuning _tuning;
        private readonly FoodItemController.Pool _foodPool;
        private readonly ExitStackAnchor _exitStackAnchor;
        private readonly SignalBus _signalBus;

        private readonly List<Vector3> _pathWorldPositions = new();
        private readonly List<BeltItem> _items = new();
        private readonly Queue<PendingEntry> _pendingEntries = new();

        // Ids of items whose entry flight hasn't visually landed yet (see FoodBeltEntryLandedSignal below).
        // MinEntrySpacing alone assumes every entry flight lands in about the time it takes Progress to
        // clear the spacing — true for a short hop like the exit stack, false for a queue lane far from the
        // belt: that flight can still be mid-air when Progress alone would've reopened the gate, so a
        // same-frame second entry lands right on top of it. This tracks actual arrival instead of guessing.
        private readonly HashSet<int> _entryFlightsInProgress = new();

        private readonly List<(int PathIndex, FoodItemController Visual)> _crossingsThisFrame = new();
        public IReadOnlyList<(int PathIndex, FoodItemController Visual)> CrossingsThisFrame => _crossingsThisFrame;

        private int _nextItemId;

        // Belt items only — GetVisualAt(i) indexes _items, so QueueLinkVisualizer's 0..ItemCount loop
        // must stay in bounds with it. Capacity checks that also need to count hovering entries should
        // use ReservedItemCount instead.
        public int ItemCount => _items.Count;

        // ItemCount plus items still hovering above the entry (already detached from queue/exit-stack,
        // just not logically on the belt yet) — what ConveyorEntryInput should gate exit-stack capacity
        // against, so a burst of rapid clicks can't reserve more slots than actually exist. A linked pair
        // is one PendingEntry with a Partner, so it counts as 2.
        public int ReservedItemCount
        {
            get
            {
                int count = _items.Count;
                foreach (var pending in _pendingEntries)
                    count += pending.Partner != null ? 2 : 1;
                return count;
            }
        }

        public int PathLength => _pathWorldPositions.Count;

        // 1 = authored BeltSpeed. Set by ConveyorAutoClearService once the queue drains and every
        // remaining item is guaranteed to fit back on the exit stack — read by ConveyorVisualService too,
        // so both layers stay on the same speed with a single source of truth instead of two copies of
        // BeltSpeed drifting apart.
        public float SpeedMultiplier { get; set; } = 1f;

        // Once true, an item reaching the belt end loops back to the entry (FoodLoopedSignal + a normal
        // reused-visual AddItemAtEntry) instead of docking at the exit stack. Set by
        // ConveyorAutoClearService; never cleared — the level only exits this state by being won.
        public bool LoopModeActive { get; private set; }

        public void EnableLoopMode() => LoopModeActive = true;

        // True once the belt's newest item has moved far enough past Progress 0 that a fresh spawn there
        // won't visually overlap it. Checked internally by AddItemAtEntry/Tick to gate the pending-entry
        // queue below — callers never need to check this themselves.
        public bool CanAcceptEntry()
        {
            if (_entryFlightsInProgress.Count > 0) return false;

            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Progress < _tuning.MinEntrySpacing) return false;

            return true;
        }

        // A linked pair lands with its leading half one spacing ahead of the entry cell, so that stretch
        // of belt has to be clear too — otherwise the leading half would land on top of an item there.
        private bool CanAcceptPairEntry(float spacing)
        {
            if (_entryFlightsInProgress.Count > 0) return false;

            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Progress < spacing + _tuning.MinEntrySpacing) return false;

            return true;
        }

        public IEnumerable<FoodItemController> PendingVisuals()
        {
            foreach (var pending in _pendingEntries)
            {
                yield return pending.Visual;
                if (pending.Partner != null) yield return pending.Partner.Visual;
            }
        }

        private void OnFoodBeltEntryLanded(FoodBeltEntryLandedSignal signal) => _entryFlightsInProgress.Remove(signal.ItemId);

        // Keep Playing resumed the level: an item still stuck at the belt end must be able to fail it again.
        private void OnLevelResumed()
        {
            for (int i = 0; i < _items.Count; i++) _items[i].OverflowReported = false;
        }

        // Mirrors QueueSpawner.TryGetVisual so callers (e.g. QueueLinkVisualizer) can enumerate belt
        // items the same way they enumerate queue items, without exposing the BeltItem type itself.
        public FoodItemController GetVisualAt(int index) => _items[index].Visual;

        // Lets ConveyorVisualService re-sync its own (separately tracked) visual Progress to the real
        // logical value once an entry flight lands — see the long comment on that call site for why.
        public float GetProgressById(int itemId)
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Id == itemId) return _items[i].Progress;

            return 0f;
        }

        public ConveyorManager(
            ConveyorLevelDataSO level,
            ConveyorPrefabSettings prefabSettings,
            ConveyorTuning tuning,
            FoodItemController.Pool foodPool,
            ExitStackAnchor exitStackAnchor,
            SignalBus signalBus)
        {
            _level = level;
            _prefabSettings = prefabSettings;
            _tuning = tuning;
            _foodPool = foodPool;
            _exitStackAnchor = exitStackAnchor;
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

            float cellSize = _prefabSettings.CellWorldSize;
            var beltOrigin = ConveyorWorldUtil.ComputeBeltOrigin(_level, _tuning, cellSize);
            foreach (var coord in orderedPath)
            {
                var position = ConveyorWorldUtil.ToWorldPosition(coord, cellSize, 0f, beltOrigin); // Y overwritten below
                position.y = _tuning.BeltFoodHeight;
                _pathWorldPositions.Add(position);
            }

            foreach (var entity in _level.ConveyorEntities)
            {
                int index = orderedPath.IndexOf(entity.Coord);
                if (index < 0)
                {
                    Debug.LogError($"[ConveyorManager] Entity at {entity.Coord} isn't on the baked path — skipped.");
                    continue;
                }

                // Level-authored items start already on the belt: no entry flight, just a placed visual.
                var visual = _foodPool.Spawn(_pathWorldPositions[index], entity.Stack, FoodVisualState.Opened);
                visual.SetLocation(FoodLocation.Conveyor);
                AddItem(entity.Stack, visual, index, _pathWorldPositions[index], playEntryFlight: false);
            }
        }

        // Spawns a fresh visual at the belt entry (no travel).
        public void AddItemAtEntry(FoodStackData stack)
        {
            if (_pathWorldPositions.Count == 0) return;

            var visual = _foodPool.Spawn(_pathWorldPositions[0], stack, FoodVisualState.Opened);
            visual.SetLocation(FoodLocation.Conveyor);
            AddItem(stack, visual, 0f, _pathWorldPositions[0], playEntryFlight: false);
        }

        // Reuses an existing visual (queue slot, exit-stack slot) and lets the visual layer fly it in
        // from where it currently sits. Logically the item is on the belt entry right away — the flight
        // chases the belt slot's live position (SampleWorldPosition keeps advancing every Tick), so it
        // catches up naturally instead of either layer waiting on the other.
        // A linked pair must not come through here twice — see AddLinkedPairAtEntry below. Two separate
        // calls can independently land on either side of the pending queue and end up entering many
        // seconds apart depending on how busy the entry happens to be, which is the bug that method fixes.
        // Callers don't need to check CanAcceptEntry() themselves: if the entry cell is occupied this
        // holds the item above the belt and lets it in once Tick() sees room (see PendingEntry above).
        public void AddItemAtEntry(FoodStackData stack, FoodItemController reusedVisual, Vector3 sourceWorldPosition, float entryProgress = 0f)
        {
            if (_pathWorldPositions.Count == 0) return;

            if (reusedVisual == null)
            {
                AddItemAtEntry(stack);
                return;
            }

            // Fast path: nothing else waiting and the entry cell is clear — enters immediately, same as
            // before this queue existed. Only rapid-clicked overflow ever takes the pending path below.
            if (_pendingEntries.Count == 0 && CanAcceptEntry())
            {
                AddItem(stack, reusedVisual, entryProgress, sourceWorldPosition, playEntryFlight: true);
                return;
            }

            // Entry's occupied — hold it above the belt instead of dropping the click. Stacks one hover
            // height per queued item so several rapid clicks float at distinct heights, not on top of
            // each other. The oldest (lowest) one is exactly the one Tick() lets in first.
            var hoverPosition = _pathWorldPositions[0] +
                Vector3.up * (_tuning.EntryHoverHeight * (_pendingEntries.Count + 1));

            _pendingEntries.Enqueue(new PendingEntry
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

        // Two linked queue items clicked as one: admits both on the same CanAcceptEntry() check so they
        // always enter together, at their usual 1-cell gap (A at the entry, B one cell behind), instead
        // of racing through AddItemAtEntry independently and potentially getting split across the pending
        // queue by however busy the entry happens to be at that moment.
        public void AddLinkedPairAtEntry(
            FoodStackData stackA, FoodItemController visualA, Vector3 sourceA,
            FoodStackData stackB, FoodItemController visualB, Vector3 sourceB)
        {
            if (_pathWorldPositions.Count == 0) return;
            if (visualA == null || visualB == null) return;

            float spacing = _tuning != null && _tuning.LinkedPairBeltSpacing > 0f ? _tuning.LinkedPairBeltSpacing : 1f;

            // The pair lands forward along the belt: A one spacing ahead of the entry cell, B on it. Starting B
            // behind the entry (negative progress) put it — and its tray — off the belt, on the tray dispenser.
            if (_pendingEntries.Count == 0 && CanAcceptPairEntry(spacing))
            {
                AddItem(stackA, visualA, spacing, sourceA, playEntryFlight: true, crossFromEntry: true);
                AddItem(stackB, visualB, 0f, sourceB, playEntryFlight: true);
                return;
            }

            // Both hover at the same height above the cells they'll land on, so the pair reads as linked the
            // whole time it waits.
            var lift = Vector3.up * (_tuning.EntryHoverHeight * (_pendingEntries.Count + 1));
            var hoverA = SampleWorldPosition(spacing) + lift;
            var hoverB = _pathWorldPositions[0] + lift;

            var pendingB = new PendingEntry { Stack = stackB, Visual = visualB, HoverWorldPosition = hoverB, EntryProgress = 0f };
            var pendingA = new PendingEntry { Stack = stackA, Visual = visualA, HoverWorldPosition = hoverA, EntryProgress = spacing, Partner = pendingB };
            _pendingEntries.Enqueue(pendingA);

            _signalBus.Fire(new FoodQueuedForEntrySignal { Visual = visualA, SourceWorldPosition = sourceA, HoverWorldPosition = hoverA });
            _signalBus.Fire(new FoodQueuedForEntrySignal { Visual = visualB, SourceWorldPosition = sourceB, HoverWorldPosition = hoverB });
        }

        // Entry cell (index 0) gets its own crossing on the item's first Tick(), same as every other
        // belt cell — see LastCrossedIndex init below.
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
                // one below the entry cell so Tick() fires an entry-cell crossing (index 0) on its
                // very first pass, instead of only becoming eligible once it has already left cell 0.
                // A pair's leading half starts ahead of the entry but still passes every cell from 0.
                LastCrossedIndex = crossFromEntry ? -1 : Mathf.FloorToInt(startProgress + 0.5f) - 1
            };
            _items.Add(item);

            // Set before firing: for playEntryFlight the landed signal only ever comes back later (the
            // flight is ticked over future frames), but staying correct even if that ever changed is cheap.
            if (playEntryFlight) _entryFlightsInProgress.Add(item.Id);

            _signalBus.Fire(new FoodEnteredBeltSignal
            {
                ItemId = item.Id,
                Visual = visual,
                SourceWorldPosition = sourceWorldPosition,
                StartProgress = startProgress,
                PlayEntryFlight = playEntryFlight
            });
        }

        // cleared by ConsumeService, not here
        public void ClearCrossings() => _crossingsThisFrame.Clear();

        // Teardown only (won level) — it despawns food, and every level's supply exactly matches demand.
        public void ClearBelt()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                _foodPool.Despawn(_items[i].Visual);
                _items.RemoveAt(i);
            }

            // Hovering entries were already detached from their queue/exit-stack slot and never joined
            // _items — without this they'd leak, floating forever with nothing left owning them.
            while (_pendingEntries.Count > 0)
            {
                var pending = _pendingEntries.Dequeue();
                _foodPool.Despawn(pending.Visual);
                if (pending.Partner != null) _foodPool.Despawn(pending.Partner.Visual);
            }

            // The visual layer cancels in-flight flights silently on BeltClearedSignal (no landed signal
            // ever fires for them) — without this the gate would stay shut forever after any belt clear.
            _entryFlightsInProgress.Clear();

            _signalBus.Fire(new BeltClearedSignal());
        }

        public void Tick()
        {
            if (_pathWorldPositions.Count == 0) return;

            float delta = _tuning.BeltSpeed * SpeedMultiplier * Time.deltaTime;
            float lastIndex = _pathWorldPositions.Count - 1;

            int finalIndex = Mathf.FloorToInt(lastIndex);

            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];

                // Held at its entry value for as long as its visual is still flying in from the queue/
                // hover/exit-stack — exactly mirroring ConveyorVisualService, which freezes its own copy of
                // Progress the same way for the same reason (see BeltVisual.IsEnteringBelt). Advancing this
                // one without the visual used to mean the logical item was already a cell or more down the
                // belt by the time its flight landed — CanAcceptEntry() would read that as "clear" and let
                // the next item in while this one's visual was still arriving, and landing itself meant
                // jumping the visual forward to catch up, which is the snap this is here to prevent. Held
                // equal the whole time, there's nothing to catch up to.
                bool isFlying = _entryFlightsInProgress.Contains(item.Id);
                if (!isFlying && item.Visual != null && item.Visual.Stack.LinkId != 0)
                {
                    int linkId = item.Visual.Stack.LinkId;
                    for (int j = 0; j < _items.Count; j++)
                    {
                        if (_items[j].Visual != null && _items[j].Visual.Stack.LinkId == linkId && _entryFlightsInProgress.Contains(_items[j].Id))
                        {
                            isFlying = true;
                            break;
                        }
                    }
                }
                if (isFlying) continue;

                item.Progress += delta;

                if (item.Progress >= lastIndex)
                {
                    // First frame reaching the end: fire the final-cell crossing (so a consume check
                    // gets the same shot at the last grid as every other cell) and wait one tick before
                    // docking, so the consume service can react to it before the item is removed.
                    if (item.LastCrossedIndex < finalIndex)
                    {
                        for (int idx = Mathf.Max(0, item.LastCrossedIndex + 1); idx <= finalIndex; idx++)
                            _crossingsThisFrame.Add((idx, item.Visual));
                        item.LastCrossedIndex = finalIndex;
                        item.Progress = lastIndex;
                        continue;
                    }

                    if (LoopModeActive)
                    {
                        var stack = item.Visual.Stack;
                        var visual = item.Visual;
                        var endPosition = SampleWorldPosition(item.Progress);

                        _items.RemoveAt(i);
                        _signalBus.Fire(new FoodLoopedSignal { ItemId = item.Id });
                        AddItemAtEntry(stack, visual, endPosition);
                        continue;
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

                        _items.RemoveAt(i);
                        continue;
                    }

                    // No free slot: the item waits at the belt end instead of being despawned (losing food
                    // makes the level unwinnable) and docks as soon as a slot frees up, e.g. Keep Playing's
                    // extra slot. The fail is reported once per stall.
                    item.Progress = lastIndex;
                    if (!item.OverflowReported)
                    {
                        item.OverflowReported = true;
                        Debug.LogWarning("[ConveyorManager] Exit stack is full — item held at the belt end.");
                        _signalBus.Fire(new ExitStackOverflowSignal { ItemId = item.Id });
                    }
                    continue;
                }

                // Crossing fires at cell center, not cell start — matches TryPeekTypeAt's RoundToInt and
                // makes the consume moment line up with the item visually sitting mid-grid, not just
                // having entered it (lag-skip guard: items can start at a negative entryProgress).
                int newIndex = Mathf.FloorToInt(item.Progress + 0.5f);
                for (int idx = Mathf.Max(0, item.LastCrossedIndex + 1); idx <= newIndex; idx++)
                    _crossingsThisFrame.Add((idx, item.Visual));
                item.LastCrossedIndex = newIndex;
            }

            // Let waiting entries in now that this frame's movement may have cleared the entry cell.
            // Adding one drops CanAcceptEntry() back to false immediately (its own Progress starts below
            // MinEntrySpacing), so this never lets two in on the same frame.
            while (_pendingEntries.Count > 0 && CanReleasePending(_pendingEntries.Peek()))
            {
                var pending = _pendingEntries.Dequeue();
                AddItem(pending.Stack, pending.Visual, pending.EntryProgress, pending.HoverWorldPosition, playEntryFlight: true,
                    crossFromEntry: pending.Partner != null);

                // Partner goes in the same pass, unconditionally — re-checking CanAcceptEntry() here would
                // always fail (the line above just added an item at Progress 0) and split the pair again.
                if (pending.Partner != null)
                    AddItem(pending.Partner.Stack, pending.Partner.Visual, pending.Partner.EntryProgress, pending.Partner.HoverWorldPosition, playEntryFlight: true);
            }
        }

        private bool CanReleasePending(PendingEntry pending) =>
            pending.Partner != null ? CanAcceptPairEntry(pending.EntryProgress) : CanAcceptEntry();

        // peek-only
        public bool TryPeekTypeAt(int pathIndex, out FoodType type)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Visual != null && _items[i].Visual.Stack.Amount <= 0) continue;

                int itemIndex = Mathf.RoundToInt(_items[i].Progress) % _pathWorldPositions.Count;
                if (itemIndex != pathIndex) continue;

                type = _items[i].Visual.Stack.Type;
                return true;
            }

            type = default;
            return false;
        }

        // by-reference, not by position. On a full take the item leaves the belt but its visual is NOT
        // despawned — ConveyorVisualService takes ownership and flies it to the customer.
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

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Visual != visual) continue;

                itemId = _items[i].Id;
                int amountTaken = Mathf.Min(amountWanted, visual.Stack.Amount);
                if (amountTaken <= 0)
                {
                    itemFullyConsumed = false;
                    return 0;
                }

                // Linked meals: one can reach 0 and stay riding on the belt until BOTH reach 0!
                if (visual.Stack.LinkId != 0)
                {
                    visual.ReduceAmount(amountTaken);

                    int linkId = visual.Stack.LinkId;
                    bool bothEmpty = true;
                    for (int j = 0; j < _items.Count; j++)
                    {
                        if (_items[j].Visual != null && _items[j].Visual.Stack.LinkId == linkId)
                        {
                            if (_items[j].Visual.Stack.Amount > 0)
                            {
                                bothEmpty = false;
                                break;
                            }
                        }
                    }

                    if (bothEmpty)
                    {
                        itemFullyConsumed = true;

                        // Find the partner item that was already at 0 portion
                        for (int j = 0; j < _items.Count; j++)
                        {
                            if (_items[j].Visual != visual && _items[j].Visual != null && _items[j].Visual.Stack.LinkId == linkId)
                            {
                                partnerItemId = _items[j].Id;
                                partnerVisual = _items[j].Visual;
                                break;
                            }
                        }

                        // The half that emptied first may have ridden to the end and docked with 0 left —
                        // release that slot along with this one instead of stranding it in the exit stack.
                        if (partnerVisual == null) partnerVisual = TakeEmptyLinkedHalfFromExitStack(linkId, visual);

                        for (int j = _items.Count - 1; j >= 0; j--)
                        {
                            if (_items[j].Visual != null && _items[j].Visual.Stack.LinkId == linkId)
                            {
                                _items.RemoveAt(j);
                            }
                        }
                    }
                    else
                    {
                        itemFullyConsumed = false;
                    }

                    return amountTaken;
                }

                itemFullyConsumed = amountTaken >= visual.Stack.Amount;

                if (itemFullyConsumed) _items.RemoveAt(i);
                else visual.ReduceAmount(amountTaken);

                return amountTaken;
            }

            itemId = -1;
            itemFullyConsumed = false;
            return 0;
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

        public Vector3 SampleWorldPosition(float progress)
        {
            int count = _pathWorldPositions.Count;
            if (count == 0) return Vector3.zero;

            // Before cell 0 (entryProgress < 0, e.g. the trailing half of a linked pair): extrapolate
            // backward along the entry direction instead of clamping to cell 0, so it doesn't overlap it.
            if (progress < 0f && count > 1)
            {
                Vector3 entryDir = _pathWorldPositions[0] - _pathWorldPositions[1];
                return _pathWorldPositions[0] + entryDir * -progress;
            }

            int index = Mathf.Clamp(Mathf.FloorToInt(progress), 0, count - 1);
            int nextIndex = LoopModeActive ? (index + 1) % count : Mathf.Min(index + 1, count - 1);
            float frac = Mathf.Clamp01(progress - Mathf.Floor(progress));
            return Vector3.Lerp(_pathWorldPositions[index], _pathWorldPositions[nextIndex], frac);
        }

        public void Dispose()
        {
            _signalBus.TryUnsubscribe<FoodBeltEntryLandedSignal>(OnFoodBeltEntryLanded);
            _signalBus.TryUnsubscribe<LevelResumedSignal>(OnLevelResumed);

            foreach (var item in _items) _foodPool.Despawn(item.Visual);
            _items.Clear();

            foreach (var pending in _pendingEntries)
            {
                _foodPool.Despawn(pending.Visual);
                if (pending.Partner != null) _foodPool.Despawn(pending.Partner.Visual);
            }
            _pendingEntries.Clear();
            _entryFlightsInProgress.Clear();
        }
    }
}
