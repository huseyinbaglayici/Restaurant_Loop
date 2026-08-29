using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Tutorial
{
    // Power-up buttons register themselves here by id — there are duplicate
    // PowerUpButtonsController instances in the hierarchy, only the active one registers.
    public class TutorialTargetRegistry
    {
        private readonly Dictionary<PowerUpButtonId, RectTransform> _targets = new();

        public void Register(PowerUpButtonId id, RectTransform target) => _targets[id] = target;

        public bool TryGet(PowerUpButtonId id, out RectTransform target) => _targets.TryGetValue(id, out target);
    }
}
