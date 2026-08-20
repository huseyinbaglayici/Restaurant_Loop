using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Installers
{
    public class PoolInstaller : MonoInstaller
    {
        [SerializeField] private int _conveyorPieceInitialSize = 30;

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
        }

        // Prewarmed at startup (WithInitialSize + NonLazy) instead of allocating the first time a
        // level asks for a piece — belt pieces are placed in bulk, mid-gameplay hitches aren't worth it.
        private void BindConveyorPiecePool(ConveyorTileShape shape, GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError($"[PoolInstaller] No prefab assigned for {shape} — set it in RestaurantLoop/Level Editor Settings.");
                return;
            }

            Container.BindMemoryPool<ConveyorPieceController, ConveyorPieceController.Pool>()
                .WithId(shape)
                .WithInitialSize(_conveyorPieceInitialSize)
                .FromComponentInNewPrefab(prefab)
                .UnderTransformGroup("ConveyorPieces")
                .NonLazy();
        }
    }
}
