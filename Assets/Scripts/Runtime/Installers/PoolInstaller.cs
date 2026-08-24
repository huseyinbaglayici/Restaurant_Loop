using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Installers
{
    public class PoolInstaller : MonoInstaller
    {
        [SerializeField] private int _conveyorPieceInitialSize = 30;
        [SerializeField] private int _foodItemInitialSize = 20;
        [SerializeField] private int _customerInitialSize = 10;

        public override void InstallBindings()
        {
            var settings = Resources.Load<ConveyorPrefabSettings>("ConveyorPrefabSettings");
            if (settings == null)
            {
                Debug.LogError("[PoolInstaller] ConveyorPrefabSettings.asset not found under a Resources folder — " +
                               "open RestaurantLoop/Level Editor Settings once to create it, assign the prefabs, " +
                               "then make sure it's saved under Assets/Resources.");
                return;
            }

            Container.BindInstance(settings).AsSingle();

            BindConveyorPiecePool(ConveyorTileShape.Straight, settings.ConveyorStraightPrefab);
            BindConveyorPiecePool(ConveyorTileShape.Corner, settings.ConveyorCornerPrefab);

            BindFoodItemPool();
            BindTrayPool();
            BindExitSlotPool();
            BindCustomerPool();
            BindTuning();
            BindFeelSettings();
        }

        private void BindFeelSettings()
        {
            var feel = Resources.Load<ConveyorFeelSettings>("ConveyorFeelSettings");
            if (feel == null)
            {
                Debug.LogError("[PoolInstaller] ConveyorFeelSettings.asset not found under Assets/Resources — " +
                               "create it via RestaurantLoop/Conveyor Feel Settings, the conveyor visual layer needs it.");
                return;
            }

            Container.BindInstance(feel).AsSingle();
        }

        private void BindTuning()
        {
            var tuning = Resources.Load<ConveyorTuning>("ConveyorTuning");
            if (tuning == null)
            {
                Debug.LogError("[PoolInstaller] ConveyorTuning.asset not found under Assets/Resources — " +
                               "queue/exit-stack/belt/input runtime classes need this bound.");
                return;
            }

            Container.BindInstance(tuning).AsSingle();
        }

        // prewarmed
        private void BindConveyorPiecePool(ConveyorTileShape shape, GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError(
                    $"[PoolInstaller] No prefab assigned for {shape} — set it in RestaurantLoop/Level Editor Settings.");
                return;
            }

            Container.BindMemoryPool<ConveyorPieceController, ConveyorPieceController.Pool>()
                .WithId(shape)
                .WithInitialSize(_conveyorPieceInitialSize)
                .FromComponentInNewPrefab(prefab)
                .UnderTransformGroup("ConveyorPieces")
                .NonLazy();
        }

        // prewarmed
        private void BindFoodItemPool()
        {
            var foodSettings = Resources.Load<FoodItemPrefabSettings>("FoodItemPrefabSettings");
            if (foodSettings == null || foodSettings.FoodItemPrefab == null)
            {
                Debug.LogError(
                    "[PoolInstaller] FoodItemPrefabSettings.asset missing or has no FoodItemPrefab assigned " +
                    "under Assets/Resources — food item pool not bound.");
                return;
            }

            Container.BindInstance(foodSettings).AsSingle();

            // optional
            var meshSettings = Resources.Load<FoodMeshSettings>("FoodMeshSettings");
            if (meshSettings != null) Container.BindInstance(meshSettings).AsSingle();

            Container.BindMemoryPool<FoodItemController, FoodItemController.Pool>()
                .WithInitialSize(_foodItemInitialSize)
                .FromComponentInNewPrefab(foodSettings.FoodItemPrefab)
                .UnderTransformGroup("FoodItems")
                .NonLazy();
        }

        private void BindTrayPool()
        {
            var traySettings = Resources.Load<TrayPrefabSettings>("TrayPrefabSettings");
            if (traySettings == null || traySettings.TrayPrefab == null)
            {
                Debug.LogError(
                    "[PoolInstaller] TrayPrefabSettings.asset missing or has no TrayPrefab assigned " +
                    "under Assets/Resources — tray pool not bound.");
                return;
            }

            Container.BindMemoryPool<TrayController, TrayController.Pool>()
                .WithInitialSize(_foodItemInitialSize)
                .FromComponentInNewPrefab(traySettings.TrayPrefab)
                .UnderTransformGroup("Trays")
                .NonLazy();
        }

        // base size: ExitStackAnchor.BaseSlotCount (pool auto-expands if the Extra Tray power-up adds a slot)
        private void BindExitSlotPool()
        {
            var exitSlotSettings = Resources.Load<ExitSlotPrefabSettings>("ExitSlotPrefabSettings");
            if (exitSlotSettings == null || exitSlotSettings.ExitSlotPrefab == null)
            {
                Debug.LogError(
                    "[PoolInstaller] ExitSlotPrefabSettings.asset missing or has no ExitSlotPrefab assigned " +
                    "under Assets/Resources — exit slot pool not bound.");
                return;
            }

            Container.BindInstance(exitSlotSettings).AsSingle();

            Container.BindMemoryPool<ExitSlotController, ExitSlotController.Pool>()
                .WithInitialSize(ExitStackAnchor.BaseSlotCount)
                .FromComponentInNewPrefab(exitSlotSettings.ExitSlotPrefab)
                .UnderTransformGroup("ExitSlots")
                .NonLazy();
        }

        private void BindCustomerPool()
        {
            var customerSettings = Resources.Load<CustomerPrefabSettings>("CustomerPrefabSettings");
            if (customerSettings == null || customerSettings.CustomerPrefab == null)
            {
                Debug.LogError(
                    "[PoolInstaller] CustomerPrefabSettings.asset missing or has no CustomerPrefab assigned " +
                    "under Assets/Resources — customer pool not bound.");
                return;
            }

            Container.BindInstance(customerSettings).AsSingle();

            var appearanceSettings = Resources.Load<CustomerAppearanceSettings>("CustomerAppearanceSettings");
            if (appearanceSettings == null)
            {
                Debug.LogError(
                    "[PoolInstaller] CustomerAppearanceSettings.asset not found under Assets/Resources — " +
                    "create it via RestaurantLoop/Customer Appearance Settings, customer pool not bound.");
                return;
            }

            Container.BindInstance(appearanceSettings).AsSingle();

            var animationSet = Resources.Load<CustomerAnimationSet>("CustomerAnimationSet");
            if (animationSet == null)
            {
                Debug.LogError(
                    "[PoolInstaller] CustomerAnimationSet.asset not found under Assets/Resources — " +
                    "create it via RestaurantLoop/Customer Animation Set, customer pool not bound.");
                return;
            }

            Container.BindInstance(animationSet).AsSingle();

            Container.BindMemoryPool<CustomerController, CustomerController.Pool>()
                .WithInitialSize(_customerInitialSize)
                .FromComponentInNewPrefab(customerSettings.CustomerPrefab)
                .UnderTransformGroup("Customers")
                .NonLazy();
        }
    }
}