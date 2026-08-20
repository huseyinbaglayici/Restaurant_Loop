using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    /// <summary>
    /// Builds the physical belt for the current level by pulling pieces from the pools bound in
    /// PoolInstaller (ProjectContext) — the runtime, pooled equivalent of the editor's
    /// ConveyorSceneBuilder. Scene-scoped: runs once when GameplayScene's container initializes.
    /// </summary>
    public class ConveyorPieceSpawner : IInitializable
    {
        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPieceController.Pool _straightPool;
        private readonly ConveyorPieceController.Pool _cornerPool;
        private readonly ConveyorPrefabSettings _prefabSettings;

        public ConveyorPieceSpawner(
            ConveyorLevelDataSO level,
            [Inject(Id = ConveyorTileShape.Straight)] ConveyorPieceController.Pool straightPool,
            [Inject(Id = ConveyorTileShape.Corner)] ConveyorPieceController.Pool cornerPool,
            ConveyorPrefabSettings prefabSettings)
        {
            _level = level;
            _straightPool = straightPool;
            _cornerPool = cornerPool;
            _prefabSettings = prefabSettings;
        }

        public void Initialize()
        {
            float cellSize = _prefabSettings.CellWorldSize;

            foreach (var cell in _level.ConveyorCells)
            {
                // The Gate is a logical seam (spawn/despawn point for the baked path), not a
                // physical belt segment — it still counts for connectivity, but no piece here.
                if (cell.IsGate) continue;

                var pool = cell.Shape == ConveyorTileShape.Straight ? _straightPool : _cornerPool;
                var position = ConveyorWorldUtil.ToWorldPosition(cell.Coord, cellSize);
                var worldYRotation = ConveyorWorldUtil.ToWorldYRotation(cell.Shape, cell.Rotation);

                pool.Spawn(position, worldYRotation);
            }
        }
    }
}
