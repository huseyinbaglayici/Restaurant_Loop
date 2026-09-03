using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class ConveyorPieceSpawner : IInitializable, IDisposable
    {
        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPieceController.Pool _straightPool;
        private readonly ConveyorPieceController.Pool _cornerPool;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ConveyorTuning _tuning;
        private readonly ThemeSettings _themeSettings;

        private readonly List<ConveyorPieceController> _spawnedStraight = new();
        private readonly List<ConveyorPieceController> _spawnedCorner = new();

        public ConveyorPieceSpawner(
            ConveyorLevelDataSO level,
            [Inject(Id = ConveyorTileShape.Straight)]
            ConveyorPieceController.Pool straightPool,
            [Inject(Id = ConveyorTileShape.Corner)]
            ConveyorPieceController.Pool cornerPool,
            ConveyorPrefabSettings prefabSettings,
            ConveyorTuning tuning,
            [Inject(Optional = true)] ThemeSettings themeSettings = null)
        {
            _level = level;
            _straightPool = straightPool;
            _cornerPool = cornerPool;
            _prefabSettings = prefabSettings;
            _tuning = tuning;
            _themeSettings = themeSettings ?? Resources.Load<ThemeSettings>("ThemeSettings");
        }

        public void Initialize()
        {
            float cellSize = _prefabSettings.CellWorldSize;
            var beltOrigin = ConveyorWorldUtil.ComputeBeltOrigin(_level, _tuning, cellSize);
            var theme = _themeSettings != null ? _themeSettings.GetTheme(_level.Theme) : null;

            foreach (var cell in _level.ConveyorCells)
            {
                // gate: no physical piece
                if (cell.IsGate) continue;

                var position = ConveyorWorldUtil.ToWorldPosition(cell.Coord, cellSize, _tuning.BeltTrackHeight, beltOrigin);
                var worldYRotation = ConveyorWorldUtil.ToWorldYRotation(cell.Shape, cell.Rotation);

                if (cell.Shape == ConveyorTileShape.Straight)
                {
                    var piece = _straightPool.Spawn(position, worldYRotation);
                    if (theme != null && theme.ConveyorStraightPrefab != null)
                    {
                        piece.ApplyThemeVisual(theme.ConveyorStraightPrefab);
                    }
                    _spawnedStraight.Add(piece);
                }
                else
                {
                    var piece = _cornerPool.Spawn(position, worldYRotation);
                    if (theme != null && theme.ConveyorCornerPrefab != null)
                    {
                        piece.ApplyThemeVisual(theme.ConveyorCornerPrefab);
                    }
                    _spawnedCorner.Add(piece);
                }
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