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
            BindExitSlotPool();
            BindTuning();
            BindFeelSettings();
            BindPlacementSettings();
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

        private void BindPlacementSettings()
        {
            var placement = Resources.Load<FoodPlacementSettings>("FoodPlacementSettings");
            if (placement == null)
            {
                Debug.LogError("[PoolInstaller] FoodPlacementSettings.asset not found under Assets/Resources — " +
                                "create it via RestaurantLoop/Food Placement Settings, food scale/spacing needs it.");
                return;
            }

            Container.BindInstance(placement).AsSingle();
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

        // ExitStackAnchor spawns one rack piece total (spans every slot) — pool only ever needs one instance.
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
                .WithInitialSize(1)
                .FromComponentInNewPrefab(exitSlotSettings.ExitSlotPrefab)
                .UnderTransformGroup("ExitSlots")
                .NonLazy();
        }
    }
}