using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Signals
{
    // Fired by the logical layer (ConveyorManager / CustomerConsumeService) the moment a decision is made;
    // the visual layer (ConveyorVisualService) and cosmetic effects react to them.

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

    // The belt-entry flight landed (or there was none). TrayVisualService waits for this so the tray
    // targets where the food already is instead of racing it there.
    public class FoodBeltEntryLandedSignal
    {
        public int ItemId;
        public FoodItemController Visual;
        public float Progress;
    }

    // The entry cell hasn't cleared MinEntrySpacing yet: the item, already detached from its slot,
    // hovers above the entry until ConveyorManager lets it in.
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

    // A docked item's flight landed. Lets the logical layer hold decisions such as a deadlock fail
    // until the item is visibly in the stack.
    public class FoodDockLandedSignal
    {
        public int ItemId;
        public FoodItemController Visual;
    }

    // Replaces FoodDockedSignal in loop mode: the old belt handle is dropped without a dock flight, since the
    // FoodEnteredBeltSignal that follows (same visual, new id) takes the item back to the entry.
    public class FoodLoopedSignal
    {
        public int ItemId;
    }

    // A linked pair was sent to the belt / refused because its partner isn't reachable yet.
    public class LinkedPairPickedSignal
    {
        public int LinkId;
    }

    public class LinkedPairRejectedSignal
    {
        public int LinkId;
    }

    // An item reached the belt end with every exit slot taken (e.g. a linked pair docking two items in one
    // tick). It waits at the belt end and docks once a slot frees.
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

    // The food reached the customer's handoff anchor: hook point for consume VFX / SFX.
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

    // A pick was refused because every tray is already out.
    public class TrayDispenserRejectedSignal
    {
    }
}
