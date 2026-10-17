using System;
using System.Collections.Generic;
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
            ThemeSettings themeSettings)
        {
            _level = level;
            _straightPool = straightPool;
            _cornerPool = cornerPool;
            _prefabSettings = prefabSettings;
            _tuning = tuning;
            _themeSettings = themeSettings;
        }

        public void Initialize()
        {
            float cellSize = _prefabSettings.CellWorldSize;
            var beltOrigin = ConveyorWorldUtil.ComputeBeltOrigin(_level, _tuning, cellSize);
            var theme = _themeSettings != null ? _themeSettings.GetTheme(_level.Theme) : null;
            var orderedPath = _level.BakedPath != null ? _level.BakedPath.OrderedPath : null;

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
                    float flowSign = 1f;
                    if (orderedPath != null && orderedPath.Count > 2)
                    {
                        int idx = orderedPath.IndexOf(cell.Coord);
                        if (idx >= 0)
                        {
                            var prev = orderedPath[(idx - 1 + orderedPath.Count) % orderedPath.Count];
                            var curr = orderedPath[idx];
                            var next = orderedPath[(idx + 1) % orderedPath.Count];
                            var inDir = curr - prev;
                            var outDir = next - curr;

                            // 2D cross product: > 0 means left turn (CCW), < 0 means right turn (CW)
                            int cross = inDir.x * outDir.y - inDir.y * outDir.x;
                            flowSign = cross > 0 ? 1f : -1f;
                        }
                    }

                    var piece = _cornerPool.Spawn(position, worldYRotation);
                    piece.SetAxisSign(flowSign);
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