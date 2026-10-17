using System.Collections.Generic;
using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Installers
{
    // Prewarmed, ProjectContext-scoped pools plus the prefab settings they are built from.
    public class PoolInstaller : MonoInstaller
    {
        [SerializeField] private ConveyorPrefabSettings _conveyorPrefabSettings;
        [SerializeField] private FoodItemPrefabSettings _foodItemPrefabSettings;
        [SerializeField] private ExitSlotPrefabSettings _exitSlotPrefabSettings;
        [SerializeField] private CustomerPrefabSettings _customerPrefabSettings;
        [SerializeField] private CustomerVisualVariantSettings _customerVisualVariantSettings;

        [SerializeField] private int _conveyorPieceInitialSize = 30;
        [SerializeField] private int _foodItemInitialSize = 20;
        [SerializeField] private int _customerInitialSize = 10;
        [SerializeField] private int _customerVisualInitialSizePerVariant = 3;

        public override void InstallBindings()
        {
            Container.BindInstance(_conveyorPrefabSettings).AsSingle();
            Container.BindInstance(_foodItemPrefabSettings).AsSingle();
            Container.BindInstance(_exitSlotPrefabSettings).AsSingle();
            Container.BindInstance(_customerPrefabSettings).AsSingle();

            BindConveyorPiecePool(ConveyorTileShape.Straight, _conveyorPrefabSettings.ConveyorStraightPrefab);
            BindConveyorPiecePool(ConveyorTileShape.Corner, _conveyorPrefabSettings.ConveyorCornerPrefab);
            BindFoodItemPool();
            BindExitSlotPool();
            BindCustomerPool();
            BindCustomerVisualPools();
        }

        private void BindConveyorPiecePool(ConveyorTileShape shape, GameObject prefab)
        {
            Container.BindMemoryPool<ConveyorPieceController, ConveyorPieceController.Pool>()
                .WithId(shape)
                .WithInitialSize(_conveyorPieceInitialSize)
                .FromComponentInNewPrefab(prefab)
                .UnderTransformGroup("ConveyorPieces")
                .NonLazy();
        }

        private void BindFoodItemPool()
        {
            Container.BindMemoryPool<FoodItemController, FoodItemController.Pool>()
                .WithInitialSize(_foodItemInitialSize)
                .FromComponentInNewPrefab(_foodItemPrefabSettings.FoodItemPrefab)
                .UnderTransformGroup("FoodItems")
                .NonLazy();
        }

        // ExitStackAnchor spawns a single rack piece spanning every slot.
        private void BindExitSlotPool()
        {
            Container.BindMemoryPool<ExitSlotController, ExitSlotController.Pool>()
                .WithInitialSize(1)
                .FromComponentInNewPrefab(_exitSlotPrefabSettings.ExitSlotPrefab)
                .UnderTransformGroup("ExitSlots")
                .NonLazy();
        }

        // ProjectContext-scoped so customers survive GameplayScene reloads instead of being re-prewarmed
        // every level. The order widget pool stays scene-local because it needs the scene's canvas.
        private void BindCustomerPool()
        {
            Container.BindInstance(_customerInitialSize).WithId("CustomerPoolSize").AsCached();

            Container.BindMemoryPool<CustomerController, CustomerController.Pool>()
                .WithInitialSize(_customerInitialSize)
                .FromComponentInNewPrefab(_customerPrefabSettings.CustomerPrefab)
                .UnderTransformGroup("Customers")
                .NonLazy();
        }

        // One small pool per variant, sized for concurrent on-screen customers.
        private void BindCustomerVisualPools()
        {
            var boundVariants = new List<CustomerVariant>();

            foreach (var entry in _customerVisualVariantSettings.Variants)
            {
                if (entry.Prefab == null)
                {
                    Debug.LogError($"[PoolInstaller] No prefab assigned for variant {entry.Variant} in CustomerVisualVariantSettings.");
                    continue;
                }

                Container.BindMemoryPool<CustomerVisualInstance, CustomerVisualInstance.Pool>()
                    .WithId(entry.Variant)
                    .WithInitialSize(_customerVisualInitialSizePerVariant)
                    .FromComponentInNewPrefab(entry.Prefab)
                    .UnderTransformGroup("CustomerVisuals")
                    .NonLazy();

                boundVariants.Add(entry.Variant);
            }

            Container.Bind<IReadOnlyList<CustomerVariant>>().FromInstance(boundVariants.ToArray()).AsCached();
        }
    }
}
