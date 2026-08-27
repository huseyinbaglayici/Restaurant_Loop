using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    public class CustomerPrefabSettings : ScriptableObject
    {
        public GameObject CustomerPrefab;

        // No Canvas component on this one — it's parented under the single shared world-space
        // order canvas at runtime (see PoolInstaller.BindCustomerPool), not its own Canvas.
        public GameObject CustomerOrderWidgetPrefab;
    }
}
