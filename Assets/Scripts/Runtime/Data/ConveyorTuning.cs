using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    [CreateAssetMenu(fileName = "ConveyorTuning", menuName = "RestaurantLoop/Conveyor Tuning")]
    public class ConveyorTuning : ScriptableObject
    {
        [Header("World Placement")]
        [Tooltip("World-space origin every conveyor piece, food item, queue lane, exit slot, tray dispenser " +
                 "and customer is placed relative to — move this instead of dragging scene props, so a level " +
                 "rebuild always lands back on the same spot.")]
        public Vector3 WorldOrigin = Vector3.zero;

        [Header("Camera & Auto-Scale (CameraController)")]
        [Tooltip("If the level's grid width or height exceeds ReferenceGridDimension, the camera orthographic size scales up proportionally.")]
        public bool EnableDynamicCameraScale = true;

        [Tooltip("When enabled, offsets the belt/customer world origin based on the difference between the level's grid size and the reference grid size.")]
        public bool AutoCenterBeltGrid = true;

        [Tooltip("Reference grid width where WorldOrigin was authored (Level 1).")]
        public int ReferenceGridWidth = 7;

        [Tooltip("Reference grid height where WorldOrigin was authored (Level 1).")]
        public int ReferenceGridHeight = 9;

        [Header("Food (item height per stage)")]
        [Tooltip("World Y every food item sits at while it is on the belt.")]
        public float BeltFoodHeight = 1f;

        [Tooltip("World Y every food item sits at while it is in a queue lane.")]
        public float QueueFoodHeight = 1f;

        [Tooltip("World Y a docked food item sits at in the exit stack — lifts it to rest on top of the rack " +
                 "instead of inside it.")]
        public float ExitStackFoodHeight = 1f;

        [Header("Belt (ConveyorManager, ConveyorPieceSpawner)")]
        [Tooltip("Path cells per second — not world units, every step takes the same time regardless of straight/corner.")]
        public float BeltSpeed = 3f;

        [Tooltip("World Y the belt track pieces themselves (straight/corner meshes) sit at.")]
        public float BeltTrackHeight = 0f;

        [Tooltip("Minimum path-cell gap (in Progress units) the entry cell must clear before another item can " +
                 "spawn there — blocks rapid-click stacking at the belt entry without a fixed input cooldown.")]
        public float MinEntrySpacing = 0.5f;

        [Header("Queue (QueueAnchor, QueueSpawner)")]
        [Tooltip("World units between consecutive lanes on X, centered under the exit stack's midpoint.")]
        public float QueueLaneSpacing = 1.2f;

        [Tooltip("World units 'below' (further -Z) ExitStackAnchor's slot row.")]
        public float QueueZOffsetFromExitStack = 2.2f;

        [Tooltip("World units between consecutive rows within one queue lane.")]
        public float QueueRowSpacing = 1.2f;

        [Tooltip("World units/sec for the forward-shift animation after a queue PopFront.")]
        public float QueueShiftSpeed = 4f;

        [Header("Serving Tray (TrayDispenserAnchor, TrayVisualService)")]
        [Tooltip("World Y the dispenser prop (and every tray parked in it) rests at.")]
        public float TrayDispenserHeight = 0f;

        [Tooltip("World Y a tray sits at while riding the belt — kept slightly under BeltFoodHeight so the tray reads as underneath the food.")]
        public float TrayBeltHeight = 0.9f;

        [Tooltip("Extra offset applied on top of the Gate cell's position, in the dispenser's own local space " +
                 "(X = sideways, Z = forward/back relative to its facing) — pushes the prop clear of the belt " +
                 "loop instead of sitting dead-center on the Gate cell.")]
        public Vector3 TrayDispenserOffset = Vector3.zero;

        [Header("Exit Stack (ExitStackAnchor)")]
        [Tooltip("World Y the exit stack rack (floor prop the food docks on top of) sits at.")]
        public float ExitStackGroundHeight = 0f;

        [Tooltip("World units 'below' (negative Z) the belt's own closest (minimum-Y) row.")]
        public float ExitStackZOffsetFromGrid = 1.5f;

        [Tooltip("World X the exit stack (and the queue, which centers itself under it) is centered on, " +
                 "relative to WorldOrigin.x — manual placement, no longer derived from the grid's width.")]
        public float ExitStackXOffset = 0f;

        [Tooltip("Distance between consecutive exit stack food slots on X. Defaults to 1 (uses cell size if 0 or left unassigned).")]
        public float ExitStackSlotSpacing = 1f;

        [Tooltip("Extra Tray power-up: local offset from the leftmost exit slot for the decorative extra rack visual.")]
        public Vector3 ExtraRackVisualOffset = new Vector3(-0.8f, 0.6f, -0.8f);

        [Tooltip("Extra Tray power-up: Euler rotation applied to the decorative extra rack visual.")]
        public Vector3 ExtraRackVisualRotation = new Vector3(0f, 90f, 0f);

        [Header("Customer (CustomerSpawner)")]
        [Tooltip("World Y a customer NPC stands at.")]
        public float CustomerHeight = 0f;

        [Header("Input (ConveyorEntryInput)")]
        [Tooltip("Click/tap hit-test radius in screen pixels.")]
        public float ClickRadiusPixels = 80f;

        [Header("Queue Link Line (QueueLinkVisualizer)")]
        [Tooltip("World units wide, the rope drawn between two linked queue items.")]
        public float LinkLineWidth = 0.06f;

        [Tooltip("World units the rope dips down at its midpoint — 0 draws it straight.")]
        public float LinkLineSag = 0.35f;

        [Tooltip("Curve smoothness — number of straight segments approximating the rope's arc.")]
        public int LinkLineSegments = 16;
    }
}
