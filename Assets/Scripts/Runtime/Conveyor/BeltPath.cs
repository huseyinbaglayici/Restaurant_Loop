using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // World positions of the baked belt path (one per cell, at food height) and sampling between them.
    public class BeltPath
    {
        private readonly List<Vector3> _positions = new();

        public int Count => _positions.Count;

        public Vector3 EntryPosition => _positions[0];

        public BeltPath(ConveyorLevelDataSO level, ConveyorPrefabSettings prefabSettings, ConveyorTuning tuning)
        {
            float cellSize = prefabSettings.CellWorldSize;
            var beltOrigin = ConveyorWorldUtil.ComputeBeltOrigin(level, tuning, cellSize);
            foreach (var coord in level.BakedPath.OrderedPath)
            {
                var position = ConveyorWorldUtil.ToWorldPosition(coord, cellSize, 0f, beltOrigin);
                position.y = tuning.BeltFoodHeight;
                _positions.Add(position);
            }
        }

        public Vector3 GetPosition(int index) => _positions[index];

        public Vector3 Sample(float progress, bool loop)
        {
            int count = _positions.Count;
            if (count == 0) return Vector3.zero;

            // Before cell 0: extrapolate backward along the entry direction instead of clamping onto cell 0.
            if (progress < 0f && count > 1)
            {
                Vector3 entryDir = _positions[0] - _positions[1];
                return _positions[0] + entryDir * -progress;
            }

            int index = Mathf.Clamp(Mathf.FloorToInt(progress), 0, count - 1);
            int nextIndex = loop ? (index + 1) % count : Mathf.Min(index + 1, count - 1);
            float frac = Mathf.Clamp01(progress - Mathf.Floor(progress));
            return Vector3.Lerp(_positions[index], _positions[nextIndex], frac);
        }
    }
}
