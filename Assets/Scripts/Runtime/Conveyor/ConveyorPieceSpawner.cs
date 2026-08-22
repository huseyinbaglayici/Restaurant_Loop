using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class ConveyorPieceSpawner : IInitializable, IDisposable
    {
        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPieceController.Pool _straightPool;
        private readonly ConveyorPieceController.Pool _cornerPool;
        private readonly ConveyorPrefabSettings _prefabSettings;

        private readonly List<ConveyorPieceController> _spawnedStraight = new();
        private readonly List<ConveyorPieceController> _spawnedCorner = new();

        public ConveyorPieceSpawner(
            ConveyorLevelDataSO level,
            [Inject(Id = ConveyorTileShape.Straight)]
            ConveyorPieceController.Pool straightPool,
            [Inject(Id = ConveyorTileShape.Corner)]
            ConveyorPieceController.Pool cornerPool,
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
                // gate: no physical piece
                if (cell.IsGate) continue;

                var position = ConveyorWorldUtil.ToWorldPosition(cell.Coord, cellSize);
                var worldYRotation = ConveyorWorldUtil.ToWorldYRotation(cell.Shape, cell.Rotation);

                if (cell.Shape == ConveyorTileShape.Straight)
                    _spawnedStraight.Add(_straightPool.Spawn(position, worldYRotation));
                else
                    _spawnedCorner.Add(_cornerPool.Spawn(position, worldYRotation));
            }
        }

        public void Dispose()
        {
            foreach (var piece in _spawnedStraight) _straightPool.Despawn(piece);
            foreach (var piece in _spawnedCorner) _cornerPool.Despawn(piece);
            _spawnedStraight.Clear();
            _spawnedCorner.Clear();
        }
    }
}