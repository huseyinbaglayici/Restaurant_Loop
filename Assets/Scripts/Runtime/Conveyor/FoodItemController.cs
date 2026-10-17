using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Pooled food item: owns the stack state and pool lifecycle, and decides which visual shows —
    // the 2D queue sprite or the 3D stack with its amount badge. The visuals themselves live in
    // FoodStackBuilder, FoodQueueSprite and FoodAmountBadge.
    public class FoodItemController : MonoBehaviour, IPoolable<Vector3, FoodStackData, FoodVisualState>
    {
        [SerializeField] private GameObject _unitPrefab;

        [Inject] private FoodMeshSettings _foodMeshSettings;
        [Inject] private FoodPlacementSettings _placement;
        [Inject] private TableSettings _tableSettings;
        // Scene-scoped binding while this pool lives in ProjectContext, so this currently resolves to null.
        [Inject(Optional = true)] private CameraController _cameraController;

        private FoodStackBuilder _stackBuilder;
        private FoodQueueSprite _queueSprite;
        private FoodAmountBadge _amountBadge;

        // The rotation this item rests at. Captured from the prefab, restored on every spawn so a pooled
        // object never inherits a spin left over from its previous life.
        private Quaternion _baseRotation = Quaternion.identity;
        private bool _baseRotationCaptured;

        private FoodVisualState _visualState;
        private FoodLocation _location;

        public FoodStackData Stack { get; private set; }

        public FoodLocation Location => _location;

        // e.g. a drink rack: several pre-placed single-item children under one shared base, instead of a
        // vertical stack of copies. Consume flights fly one of those children directly (see TakeFixedSlotChild)
        // rather than a duplicate of the whole rack.
        public bool UsesFixedChildSlots => _stackBuilder.UsesFixedChildSlots;

        public Vector3 VisualCenter
        {
            get
            {
                if (ShouldUse2DQueue() && _queueSprite.Exists)
                {
                    return _queueSprite.Position;
                }

                return _stackBuilder.GetVisualCenter(Stack, _location);
            }
        }

        // World position of the top unit — the one that renders Opened while this item is Opened. Consume
        // flights should spawn here, not at the item root (which sits at the bottom of the stack).
        public Vector3 TopUnitWorldPosition => _stackBuilder.TopUnitWorldPosition;

        // Zenject injects before Awake (the prefab is instantiated inactive), so the settings are ready here.
        private void Awake()
        {
            _stackBuilder = new FoodStackBuilder(transform, _unitPrefab, _foodMeshSettings, _placement, _tableSettings, _cameraController);
            _queueSprite = new FoodQueueSprite(transform, _foodMeshSettings, _placement, _tableSettings, _cameraController);
            _amountBadge = new FoodAmountBadge(transform, _stackBuilder, _foodMeshSettings, _cameraController);
        }

        public void OnSpawned(Vector3 position, FoodStackData stack, FoodVisualState visualState)
        {
            CaptureBaseRotationOnce();

            transform.SetPositionAndRotation(position, _baseRotation);
            Stack = stack;
            _visualState = visualState;
            _location = FoodLocation.Queue;
            ApplyStack(stack);
            gameObject.SetActive(true);
        }

        public void OnDespawned()
        {
            if (this == null || gameObject == null) return;
            _queueSprite.Hide();
            _amountBadge.Hide();
            gameObject.SetActive(false);
        }

        public void SetVisualState(FoodVisualState visualState)
        {
            if (_visualState == visualState) return;

            _visualState = visualState;
            ApplyStack(Stack);
        }

        // Where this item currently sits (queue / exit stack / belt) — drives which of FoodPlacementSettings'
        // per-location scales applies. Called by the systems that move a visual between those places.
        public void SetLocation(FoodLocation location)
        {
            if (_location == location) return;

            _location = location;
            ApplyStack(Stack);
        }

        public void ReduceAmount(int amount)
        {
            var stack = Stack;
            stack.Amount -= amount;
            Stack = stack;
            ApplyStack(stack);
        }

        public void SetAmountBadgeVisible(bool visible)
        {
            _amountBadge.SetVisible(visible);
        }

        public void PopAmountBadge()
        {
            _amountBadge.Pop(Stack, _location);
        }

        public Transform TakeFixedSlotChild()
        {
            return _stackBuilder.TakeFixedSlotChild();
        }

        private bool ShouldUse2DQueue()
        {
            return _location == FoodLocation.Queue && (_tableSettings == null || _tableSettings.Enable2DQueueVisuals);
        }

        private void ApplyStack(FoodStackData stack)
        {
            if (ShouldUse2DQueue())
            {
                _queueSprite.Show(stack);
                _stackBuilder.SetActive(false);
                _amountBadge.Hide();
                return;
            }

            _queueSprite.Hide();
            _stackBuilder.SetActive(true);
            // Badge is positioned before the rebuild (against the previous units); LateUpdate re-aligns it.
            _amountBadge.Show(stack, _location);
            _stackBuilder.Build(stack, _visualState, _location);
        }

        // Keeps camera-facing visuals aligned and picks up scale / spacing tweaks made in the placement
        // asset while the game is running.
        private void LateUpdate()
        {
            if (ShouldUse2DQueue())
            {
                _queueSprite.Refresh();
                return;
            }

            _amountBadge.AlignWithCamera(_location);
            _stackBuilder.RefreshLayout(Stack.Type, _visualState, _location);
        }

        private void CaptureBaseRotationOnce()
        {
            if (_baseRotationCaptured) return;

            _baseRotationCaptured = true;
            _baseRotation = transform.rotation;
        }

        public class Pool : MonoPoolableMemoryPool<Vector3, FoodStackData, FoodVisualState, FoodItemController>
        {
        }
    }
}
