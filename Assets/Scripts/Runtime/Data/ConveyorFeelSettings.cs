using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    // Everything about how the conveyor *feels* — nothing here changes what the logical layer decides,
    // only how (and how late) the visual layer plays that decision out.
    [CreateAssetMenu(fileName = "ConveyorFeelSettings", menuName = "RestaurantLoop/Conveyor Feel Settings")]
    public class ConveyorFeelSettings : ScriptableObject
    {
        // One A -> B hop of a food visual. Each hop type (queue entry, exit dock, customer delivery)
        // gets its own instance so they can be tuned independently.
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

        [Header("Queue -> belt entry")]
        public FlightTuning QueueToBelt = new() { Speed = 12f, ArcHeight = 0.6f };

        [Header("Belt -> exit stack")]
        public FlightTuning BeltToStack = new() { Speed = 8f, ArcHeight = 0.45f };

        [Header("Belt -> customer (consume)")]
        public FlightTuning ConsumeToCustomer = new() { Speed = 14f, ArcHeight = 0.8f };

        [Header("Serving tray")]
        [Tooltip("Dispenser -> belt entry flight (tray leaving its resting spot to catch up with its food).")]
        public FlightTuning DispenserToBelt = new() { Speed = 12f, ArcHeight = 0.3f };

        [Tooltip("Seconds for the tray's upright <-> flat rotation, played alongside the Dispenser->Belt flight.")]
        public float TrayRotateDuration = 0.25f;

        [Tooltip("Seconds for each half (shrink, then grow back) of the tray's return-to-dispenser pop — " +
                 "it shrinks away where it stands, teleports to its resting pose, then grows back in. No " +
                 "flight across the belt for the return trip.")]
        public float TrayVanishDuration = 0.18f;

        [Tooltip("Overshoot on the punchier version of the vanish/reappear pop, played when a tray's food was " +
                 "fully eaten mid-belt (vs. the calmer pop when it completed its lap and docked instead).")]
        public float TrayVanishPunchOvershoot = 1.7f;

        // Runtime-only display scale. Nothing here is ever written back to the food prefab, the unit prefab or
        // the source mesh — the multiplier is applied to the spawned units every time the visual is rebuilt,
        // so a mesh that was authored at a different import scale can be normalized without touching art data.
        [System.Serializable]
        public class FoodScaleOverride
        {
            public FoodType Type;

            [Tooltip("Multiplier for the unpacked (belt / customer) mesh. 0 or less is read as 1.")]
            public float OpenedScale = 1f;

            [Tooltip("Multiplier for the packaged (queue) mesh. 0 or less is read as 1.")]
            public float ClosedScale = 1f;
        }

        [Header("Food display scale (runtime only)")]
        [Tooltip("Applied to every food type that has no override below.")]
        public float DefaultOpenedScale = 1f;

        public float DefaultClosedScale = 1f;

        [Tooltip("Per-type multipliers, for meshes whose import scale doesn't match the rest.")]
        public List<FoodScaleOverride> FoodScaleOverrides = new();

        [Tooltip("Gap between stacked units, as a fraction of one unit's actual rendered height. 1 = units touch, " +
                 "below 1 = they overlap, above 1 = they float apart. 0 or less falls back to the food prefab's own " +
                 "Stack Spacing value.")]
        public float StackSpacingFactor = 1f;

        public float ResolveFoodScale(FoodType type, FoodVisualState state)
        {
            bool isOpened = state == FoodVisualState.Opened;

            for (int i = 0; i < FoodScaleOverrides.Count; i++)
            {
                var entry = FoodScaleOverrides[i];
                if (entry == null || entry.Type != type) continue;

                return Normalize(isOpened ? entry.OpenedScale : entry.ClosedScale);
            }

            return Normalize(isOpened ? DefaultOpenedScale : DefaultClosedScale);
        }

        private static float Normalize(float scale) => scale > 0f ? scale : 1f;

        [Header("Belt ride")]
        [Tooltip("Degrees per second spun around world Y while riding the belt. 0 = no spin.")]
        public float BeltSpinYDegreesPerSecond;

        [Header("Customer consume reaction")]
        [Tooltip("Small scale punch played on the customer's model each time a food unit lands on them.")]
        public Vector3 ConsumePunchStrength = new(0.05f, 0.05f, 0.05f);

        public float ConsumePunchDuration = 0.15f;
        public int ConsumePunchVibrato = 6;
        [Range(0f, 1f)] public float ConsumePunchElasticity = 0.6f;
    }
}
