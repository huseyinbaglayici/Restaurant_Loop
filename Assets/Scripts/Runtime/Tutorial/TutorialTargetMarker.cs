using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Tutorial
{
    public class TutorialTargetMarker : MonoBehaviour
    {
        [SerializeField] private PowerUpButtonId _id;

        private TutorialTargetRegistry _registry;

        [Inject]
        public void Construct(TutorialTargetRegistry registry) => _registry = registry;

        private void Start() => _registry.Register(_id, (RectTransform)transform);
    }
}
