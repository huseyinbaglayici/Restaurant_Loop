using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    // Closed: packaged, sits in the queue. Opened: unpacked, on the belt / in a customer's hand.
    public enum FoodVisualState
    {
        Closed,
        Opened
    }

    [CreateAssetMenu(fileName = "FoodMeshSettings", menuName = "RestaurantLoop/Food Mesh Settings")]
    public class FoodMeshSettings : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public FoodType Type;

            [Header("Color")]
            [Tooltip("Color of this food type (link prism, UI). Leave unset (0,0,0,0) to use the built-in type color.")]
            public Color Color;

            [Tooltip("Instantiated as-is for the opened unit visual — its own authored mesh, materials and " +
                     "orientation are used directly. Leave empty to keep the pool's generic unit prefab, " +
                     "tinted with the food type colour.")]
            public GameObject OpenedPrefab;
            [Tooltip("Extra local rotation applied on top of the prefab's own orientation, for models authored " +
                     "facing the wrong way. Constant — same in the queue, on the belt and in the exit stack.")]
            public Vector3 OpenedRotationEuler;

            [Tooltip("Same as Opened Prefab, for the packaged/closed look.")]
            public GameObject ClosedPrefab;
            [Tooltip("Same as Opened Rotation Euler, for the packaged prefab.")]
            public Vector3 ClosedRotationEuler;

            [Header("2D Sprites")]
            [Tooltip("2D icon shown in the customer's order bubble.")]
            public Sprite CustomerIcon;

            [Tooltip("2D icon shown on the queue table.")]
            public Sprite QueueIcon;

            [Tooltip("UI sprite for this food type (fallback if specific icons are not set).")]
            public Sprite Icon;

            [Tooltip("This prefab is one fixed rack holding several pre-placed items (e.g. a shelf of drinks), " +
                     "not a single item to be stacked N times. One instance is spawned regardless of Amount; " +
                     "individual items are shown/hidden as the stack is consumed instead. Structure: child[0] " +
                     "is the always-visible base/decoration (e.g. the tray), children[1..] are the toggleable slots.")]
            public bool FixedChildSlots;
        }

        // What a single food unit should look like in one visual state.
        public readonly struct MeshVisual
        {
            public readonly GameObject Prefab;
            public readonly Vector3 RotationEuler;
            public readonly bool FixedChildSlots;

            public MeshVisual(GameObject prefab, Vector3 rotationEuler, bool fixedChildSlots)
            {
                Prefab = prefab;
                RotationEuler = rotationEuler;
                FixedChildSlots = fixedChildSlots;
            }
        }

        [Header("Customer Order Bubble")]
        [Tooltip("UI sprite shown when a customer is hidden (question mark / mystery bubble).")]
        public Sprite HiddenOrderBubble;

        [Tooltip("Overall scale of the customer order bubble.")]
        [Range(0.5f, 5f)] public float CustomerBubbleScale = 1.0f;

        [Tooltip("Height of the order bubble above the customer.")]
        public float CustomerBubbleHeight = 3.0f;

        [Tooltip("Scale the bubble up as the camera zooms out.")]
        public bool ScaleBubbleWithCamera = false;

        [Tooltip("Camera zoom multiplier for the bubble scale.")]
        public float BubbleCameraScaleMultiplier = 1.0f;

        [Header("Customer Order Bubble Pulsing Animation")]
        [Tooltip("Play a gentle pulse on the order bubble.")]
        public bool EnableBubblePulsing = true;
        [Tooltip("Pulse scale multiplier (e.g. 1.1).")]
        public float BubblePulseScale = 1.1f;
        [Tooltip("Pulse cycle duration in seconds.")]
        public float BubblePulseDuration = 0.7f;
        [Tooltip("Pulse color tint (above 1 brightens the bubble).")]
        public Color BubblePulseColor = new Color(1.12f, 1.12f, 1.12f, 1f);

        [Header("Fonts & Badges")]
        [Tooltip("Font asset used for stack amount numbers across 2D queue and 3D conveyor.")]
        public TMPro.TMP_FontAsset StackAmountFont;

        [Tooltip("Extra height of the amount badge above foods on the belt and exit rack.")]
        public float ConveyorBadgeHeightOffset = 0.8f;

        [SerializeField] private List<Entry> _entries = new();

        public bool TryGetVisual(FoodType type, FoodVisualState state, out MeshVisual visual)
        {
            foreach (var entry in _entries)
            {
                if (entry.Type != type) continue;

                bool isOpened = state == FoodVisualState.Opened;
                visual = isOpened
                    ? new MeshVisual(entry.OpenedPrefab, entry.OpenedRotationEuler, entry.FixedChildSlots)
                    : new MeshVisual(entry.ClosedPrefab, entry.ClosedRotationEuler, entry.FixedChildSlots);

                return visual.Prefab != null;
            }

            visual = default;
            return false;
        }

        public bool TryGetCustomerIcon(FoodType type, out Sprite icon)
        {
            foreach (var entry in _entries)
            {
                if (entry.Type != type) continue;

                icon = entry.CustomerIcon != null ? entry.CustomerIcon : entry.Icon;
                return icon != null;
            }

            icon = null;
            return false;
        }

        public bool TryGetQueueIcon(FoodType type, out Sprite icon)
        {
            foreach (var entry in _entries)
            {
                if (entry.Type != type) continue;

                icon = entry.QueueIcon != null ? entry.QueueIcon : entry.Icon;
                return icon != null;
            }

            icon = null;
            return false;
        }

        public bool TryGetIcon(FoodType type, out Sprite icon) => TryGetCustomerIcon(type, out icon);

        public Color GetColor(FoodType type)
        {
            foreach (var entry in _entries)
            {
                if (entry.Type != type) continue;
                var c = entry.Color;
                if (c.r > 0.001f || c.g > 0.001f || c.b > 0.001f || c.a > 0.001f)
                {
                    c.a = 1f;
                    return c;
                }
                break;
            }
            var fallback = FoodTypeUtil.ToColor(type);
            fallback.a = 1f;
            return fallback;
        }
    }
}
