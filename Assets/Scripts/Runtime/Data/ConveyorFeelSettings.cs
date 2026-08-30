using DG.Tweening;
using UnityEngine;

namespace Runtime.Data
{
    [CreateAssetMenu(fileName = "ConveyorFeelSettings", menuName = "RestaurantLoop/Conveyor Feel Settings")]
    public class ConveyorFeelSettings : ScriptableObject
    {
        [System.Serializable]
        public class FlightTuning
        {
            [Tooltip("World units per second. Flight time = distance / Speed, then clamped by Min/Max Duration.")]
            public float Speed = 10f;

            [Tooltip("Lower bound on flight time, so short hops still read as a move instead of a snap.")]
            public float MinDuration = 0.08f;

            [Tooltip("Upper bound on flight time, so long hops don't drag.")]
            public float MaxDuration = 1.2f;

            [Tooltip("Peak height (world units) of the parabola added on top of the straight A -> B line. 0 = flat.")]
            public float ArcHeight = 0.35f;

            [Tooltip("Degrees per second spun around world Y while flying. 0 = no spin.")]
            public float SpinYDegreesPerSecond;

            [Tooltip("Snap rotation back to what it was before the flight when it lands. Only matters when " +
                     "Spin Y is non-zero — the flight itself never touches X or Z.")]
            public bool ResetRotationOnArrive = true;

            [Tooltip("Easing applied to the A -> B interpolation (DOTween easing curve).")]
            public Ease Ease = Ease.OutQuad;

            public float ResolveDuration(float distance)
            {
                float raw = Speed <= 0f ? MinDuration : distance / Speed;
                return Mathf.Clamp(raw, MinDuration, Mathf.Max(MinDuration, MaxDuration));
            }
        }

        [Header("Logical -> visual offset")]
        [Tooltip("Seconds the visual layer runs behind the logical layer. The logical grid decides consume/dock " +
                 "immediately; the visual layer executes that decision this many seconds later.")]
        public float VisualLagSeconds = 0.05f;

        [Header("Queue -> belt entry")] public FlightTuning QueueToBelt = new() { Speed = 12f, ArcHeight = 0.6f };

        [Header("Belt -> exit stack")] public FlightTuning BeltToStack = new() { Speed = 8f, ArcHeight = 0.45f };

        [Header("Belt -> customer (consume)")]
        public FlightTuning ConsumeToCustomer = new() { Speed = 14f, ArcHeight = 0.8f };

        [Header("Serving tray")]
        [Tooltip("Dispenser <-> belt flight, shared by the tray leaving to catch its food and by its return trip " +
                 "(lap completed and docked, or food eaten mid-belt) — same feel every time, independent of " +
                 "how far the hop actually is.")]
        public FlightTuning TrayDispenserFlight = new()
        {
            Speed = 16f, MinDuration = 0.25f, MaxDuration = 0.25f, ArcHeight = 0.35f, ResetRotationOnArrive = false,
            Ease = Ease.OutBack
        };

        [Header("Belt ride")] [Tooltip("Degrees per second spun around world Y while riding the belt. 0 = no spin.")]
        public float BeltSpinYDegreesPerSecond;

        [Header("Customer consume reaction")]
        [Tooltip("Small scale punch played on the customer's model each time a food unit lands on them.")]
        public Vector3 ConsumePunchStrength = new(0.05f, 0.05f, 0.05f);

        public float ConsumePunchDuration = 0.15f;
        public int ConsumePunchVibrato = 6;
        [Range(0f, 1f)] public float ConsumePunchElasticity = 0.6f;
    }
}