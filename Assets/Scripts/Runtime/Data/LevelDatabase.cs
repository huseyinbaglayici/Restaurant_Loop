using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    // Resources-loaded, same pattern as ConveyorPrefabSettings — lets SaveManager resolve a
    // persisted level index back into a ConveyorLevelDataSO without a Resources.Load per-asset
    // naming convention.
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "RestaurantLoop/Level Database")]
    public class LevelDatabase : ScriptableObject
    {
        [SerializeField] private List<ConveyorLevelDataSO> _levels = new();

        public bool TryGetLevel(int levelId, out ConveyorLevelDataSO level)
        {
            foreach (var candidate in _levels)
            {
                if (candidate != null && candidate.LevelId == levelId)
                {
                    level = candidate;
                    return true;
                }
            }

            level = null;
            return false;
        }
    }
}
