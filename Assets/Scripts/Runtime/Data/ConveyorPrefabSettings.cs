using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    /// <summary>
    /// Project-wide prefab assignments for conveyor pieces — one persisted asset (lives in a
    /// Resources folder so both the editor tool and the runtime pool can load it: the editor via
    /// AssetDatabase, PoolInstaller via Resources.Load). Not per-level data.
    /// </summary>
    public class ConveyorPrefabSettings : ScriptableObject
    {
        public GameObject ConveyorStraightPrefab;
        public GameObject ConveyorCornerPrefab;
        public float CellWorldSize = 2f;
    }
}
