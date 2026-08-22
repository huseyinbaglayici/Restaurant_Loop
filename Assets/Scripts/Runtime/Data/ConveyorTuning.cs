using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    [CreateAssetMenu(fileName = "ConveyorTuning", menuName = "RestaurantLoop/Conveyor Tuning")]
    public class ConveyorTuning : ScriptableObject
    {
        [Header("Belt (ConveyorManager)")]
        [Tooltip("Path cells per second — not world units, every step takes the same time regardless of straight/corner.")]
        public float BeltSpeed = 3f;

        [Header("Queue (QueueAnchor, QueueSpawner)")]
        [Tooltip("World units between consecutive lanes on X, centered under the exit stack's midpoint.")]
        public float QueueLaneSpacing = 1.2f;

        [Tooltip("World units 'below' (further -Z) ExitStackAnchor's slot row.")]
        public float QueueZOffsetFromExitStack = 2.2f;

        [Tooltip("World units between consecutive rows within one queue lane.")]
        public float QueueRowSpacing = 1.2f;

        [Tooltip("World units/sec for the forward-shift animation after a queue PopFront.")]
        public float QueueShiftSpeed = 4f;

        [Header("Exit Stack (ExitStackAnchor)")]
        [Tooltip("World units 'below' (negative Z) the belt's own closest (minimum-Y) row.")]
        public float ExitStackZOffsetFromGrid = 1.5f;

        [Header("Input (ConveyorEntryInput)")]
        [Tooltip("Click/tap hit-test radius in screen pixels.")]
        public float ClickRadiusPixels = 80f;
    }
}
