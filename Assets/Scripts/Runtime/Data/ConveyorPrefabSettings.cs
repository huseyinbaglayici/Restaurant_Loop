using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    public class ConveyorPrefabSettings : ScriptableObject
    {
        public GameObject ConveyorStraightPrefab;
        public GameObject ConveyorCornerPrefab;
        public float CellWorldSize = 2f;
        public GameObject[] EnvironmentPresets;
    }
}
