using UnityEngine;

namespace RestaurantLoop.Runtime.Environment
{
    // Combine is called after conveyor-based layout is applied elsewhere (future work) —
    // moving a child transform after Combine() breaks that child's batch, so any repositioning
    // logic must run before this, not after.
    public class EnvironmentController : MonoBehaviour
    {
        private void Start()
        {
            StaticBatchingUtility.Combine(gameObject);
        }
    }
}
