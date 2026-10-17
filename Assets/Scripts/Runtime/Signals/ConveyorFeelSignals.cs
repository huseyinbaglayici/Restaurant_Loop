using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Signals
{
    // Logical layer (ConveyorManager / CustomerConsumeService) fires these the instant a decision is
    // made, and the visual layer (ConveyorVisualService) reacts the same frame — no lag, no waiting.
    // Anything cosmetic added later — VFX, SFX, screen shake, DOTween flourishes — subscribes here too.

    public class FoodEnteredBeltSignal
    {
        public int ItemId;
        public FoodItemController Visual;

        // Where the visual physically is right now (queue slot, exit-stack slot, or the belt entry itself).
        public Vector3 SourceWorldPosition;

        // Path progress the logical item was created at (0 for entry, cell index for pre-placed level items).
        public float StartProgress;

        // false for level-authored items that are already sitting on the belt at boot.
        public bool PlayEntryFlight;
    }

    // Fired by the visual layer the instant a food's belt-entry flight actually lands (or immediately,
    // for level-authored items that never played one). TrayVisualService waits for this before flying a
    // tray in, so the tray always targets where the food already is instead of racing it there.
    public class FoodBeltEntryLandedSignal
    {
        public int ItemId;
        public FoodItemController Visual;
        public float Progress;
    }

    // Fired when a click wants an item on the belt but the entry cell hasn't cleared MinEntrySpacing yet.
    // The item is already detached from its queue/exit-stack slot, so it can't just sit there — it floats
    // above the entry until ConveyorManager.Tick() dequeues it once CanAcceptEntry() is true again.
    public class FoodQueuedForEntrySignal
    {
        public FoodItemController Visual;
        public Vector3 SourceWorldPosition;
        public Vector3 HoverWorldPosition;
    }

    public class FoodDockedSignal
    {
        public int ItemId;
        public FoodItemController Visual;
        public int SlotIndex;
        public Vector3 SlotWorldPosition;
    }

    // Fired by the visual layer once a docked item's fly-to-slot flight actually lands. Lets the logical
    // layer defer decisions that depend on the item visually being in the stack (e.g. a deadlock fail)
    // until the player can actually see it there, instead of firing the same frame the dock is decided.
    public class FoodDockLandedSignal
    {
        public int ItemId;
        public FoodItemController Visual;
    }

    // Fired instead of FoodDockedSignal when ConveyorManager.LoopModeActive is true and an item reaches
    // the belt end — tells the visual layer to drop its old belt-visual handle for this id without a
    // dock flight, since the FoodEnteredBeltSignal that immediately follows (same reused visual, new id)
    // takes over the transform and flies it back to the entry instead.
    public class FoodLoopedSignal
    {
        public int ItemId;
    }

    // Fired when an item reaches the belt end and ExitStackAnchor.TryDock fails (every slot already
    // occupied) — e.g. a linked pair docks two items in one tick, filling the last slot with the first
    // and leaving the second with nowhere to go. CheckDeadlock's full-stack edge trigger already fired
    // (or didn't need to, if an occupant still looked reachable) before this happened, so nothing else
    // catches it — without this signal the item is just despawned and the level silently never fails.
    public class ExitStackOverflowSignal
    {
        public int ItemId;
    }

    public class FoodConsumedSignal
    {
        public int ItemId;
        public FoodItemController Visual;

        // What the customer took — not what is left on the belt item.
        public FoodStackData TakenStack;

        // true: the belt item is gone logically and its visual is handed over to the visual layer.
        // false: the belt item stays on the belt with a reduced amount.
        public bool ItemFullyConsumed;

        public int? PartnerItemId;
        public FoodItemController PartnerVisual;

        public Vector2Int CustomerCoord;
        public RuntimeCustomerState.ConsumeResult ServeResult;
        public bool LevelWon;
    }

    // Fired by the visual layer once the food actually reaches the customer's handoff anchor.
    // This is the hook point for consume VFX / SFX / customer dissolve.
    public class FoodDeliveredToCustomerSignal
    {
        public Vector2Int CustomerCoord;
        public FoodStackData DeliveredStack;
        public Vector3 WorldPosition;
        public RuntimeCustomerState.ConsumeResult ServeResult;
    }

    // Belt was wiped logically (level teardown / restart) — visual layer drops everything it still owns.
    public class BeltClearedSignal
    {
    }

    // A queue/exit-stack pick was refused because every tray is already out — TrayDispenserCounterUI
    // shakes and flashes its counter.
    public class TrayDispenserRejectedSignal
    {
    }
}
