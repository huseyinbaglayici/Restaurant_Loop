using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
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
