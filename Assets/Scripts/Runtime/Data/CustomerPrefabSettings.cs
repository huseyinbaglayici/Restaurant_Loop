using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    public class CustomerPrefabSettings : ScriptableObject
    {
        public GameObject CustomerPrefab;

        // No Canvas of its own: it is parented under the shared world-space order canvas at runtime.
        public GameObject CustomerOrderWidgetPrefab;
    }
}
