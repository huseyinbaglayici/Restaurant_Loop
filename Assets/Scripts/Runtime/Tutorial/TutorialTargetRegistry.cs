using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Tutorial
{
    // Power-up buttons register here by id so the tutorial can point at them.
    public class TutorialTargetRegistry
    {
        private readonly Dictionary<PowerUpButtonId, RectTransform> _targets = new();

        public void Register(PowerUpButtonId id, RectTransform target) => _targets[id] = target;

        public bool TryGet(PowerUpButtonId id, out RectTransform target) => _targets.TryGetValue(id, out target);
    }
}
