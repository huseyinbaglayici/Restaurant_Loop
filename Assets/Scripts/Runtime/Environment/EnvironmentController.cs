using UnityEngine;

namespace RestaurantLoop.Runtime.Environment
{
    // Static-batches the environment on Start. Children must not move afterwards or their batch breaks.
    public class EnvironmentController : MonoBehaviour
    {
        private void Start()
        {
            StaticBatchingUtility.Combine(gameObject);
        }
    }
}
