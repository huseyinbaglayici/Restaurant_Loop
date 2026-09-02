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
            [Tooltip("Bu yiyecek tipine özel renk (bağlantı prizması, UI vs. için). Boş bırakılırsa varsayılan renk kullanılır.")]
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
            [Tooltip("Müşteri sipariş baloncuğunda çıkacak 2D ikon.")]
            public Sprite CustomerIcon;

            [Tooltip("Aşağıdaki kuyrukta (Queue) tabakta/masada çıkacak 2D ikon.")]
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

        [Tooltip("Müşteri sipariş baloncuğunun genel büyüklüğü / ölçeği (Scale).")]
        [Range(0.5f, 5f)] public float CustomerBubbleScale = 1.0f;

        [Tooltip("Sipariş baloncuğunun müşterinin ne kadar yukarısında duracağı (Y Yüksekliği).")]
        public float CustomerBubbleHeight = 3.0f;

        [Tooltip("Kamera zoom yaptıkça baloncuğun da büyümesini sağlar.")]
        public bool ScaleBubbleWithCamera = false;

        [Tooltip("Kamera zoom çarpanı.")]
        public float BubbleCameraScaleMultiplier = 1.0f;

        [Header("Fonts & Badges")]
        [Tooltip("Font asset used for stack amount numbers across 2D queue and 3D conveyor.")]
        public TMPro.TMP_FontAsset StackAmountFont;

        [Tooltip("Bantta (Conveyor) ve Rafta (Stack) yemeklerin tepesindeki sayı rozetinin ekstra Y yükseklik ofseti.")]
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
