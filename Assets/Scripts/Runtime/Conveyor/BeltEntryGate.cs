using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Decides when a picked item may drop onto the belt entry. Clicks that arrive while the entry is busy
    // wait here (hovering above it), and entry flights that haven't landed yet keep the entry closed.
    public class BeltEntryGate
    {
        // Already detached from its queue/exit-stack slot, hovering above the entry until it's let in.
        public class PendingEntry
        {
            public FoodStackData Stack;
            public FoodItemController Visual;
            public Vector3 HoverWorldPosition;
            public float EntryProgress;

            // Set on a linked pair's leading half; both halves are released together.
            public PendingEntry Partner;
        }

        private readonly ConveyorTuning _tuning;
        private readonly FoodItemController.Pool _foodPool;

        private readonly Queue<PendingEntry> _pending = new();

        // Ids whose entry flight hasn't landed yet. A far queue lane's flight can still be mid-air after
        // Progress alone would have reopened the entry, so the gate waits for the actual landing.
        private readonly HashSet<int> _flightsInProgress = new();

        public BeltEntryGate(ConveyorTuning tuning, FoodItemController.Pool foodPool)
        {
            _tuning = tuning;
            _foodPool = foodPool;
        }

        // A linked pair counts as 2.
        public int PendingItemCount
        {
            get
            {
                int count = 0;
                foreach (var pending in _pending)
                    count += pending.Partner != null ? 2 : 1;
                return count;
            }
        }

        // Each waiting entry hovers one step higher, so rapid clicks don't stack on top of each other.
        public Vector3 NextHoverLift => Vector3.up * (_tuning.EntryHoverHeight * (_pending.Count + 1));

        public IEnumerable<FoodItemController> PendingVisuals()
        {
            foreach (var pending in _pending)
            {
                yield return pending.Visual;
                if (pending.Partner != null) yield return pending.Partner.Visual;
            }
        }

        public bool IsFlying(int itemId) => _flightsInProgress.Contains(itemId);

        public void MarkFlying(int itemId) => _flightsInProgress.Add(itemId);

        public void MarkLanded(int itemId) => _flightsInProgress.Remove(itemId);

        // Nothing waiting and the entry is clear: the item can go straight onto the belt.
        public bool CanEnterNow(float lowestBeltProgress, float leadSpacing) =>
            _pending.Count == 0 && IsEntryClear(lowestBeltProgress, leadSpacing);

        public void Hold(PendingEntry entry) => _pending.Enqueue(entry);

        public bool TryRelease(float lowestBeltProgress, out PendingEntry entry)
        {
            entry = null;
            if (_pending.Count == 0) return false;

            var next = _pending.Peek();
            float leadSpacing = next.Partner != null ? next.EntryProgress : 0f;
            if (!IsEntryClear(lowestBeltProgress, leadSpacing)) return false;

            entry = _pending.Dequeue();
            return true;
        }

        // Despawns waiting visuals (nothing else owns them) and reopens the entry.
        public void Clear()
        {
            while (_pending.Count > 0)
            {
                var pending = _pending.Dequeue();
                _foodPool.Despawn(pending.Visual);
                if (pending.Partner != null) _foodPool.Despawn(pending.Partner.Visual);
            }

            _flightsInProgress.Clear();
        }

        // A pair's leading half lands leadSpacing ahead of the entry, so that stretch must be clear too.
        private bool IsEntryClear(float lowestBeltProgress, float leadSpacing)
        {
            if (_flightsInProgress.Count > 0) return false;
            return lowestBeltProgress >= leadSpacing + _tuning.MinEntrySpacing;
        }
    }
}
