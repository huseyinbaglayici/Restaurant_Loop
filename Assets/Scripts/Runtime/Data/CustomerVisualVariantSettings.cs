using System;
using RestaurantLoop.Runtime.Conveyor;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    public class CustomerVisualVariantSettings : ScriptableObject
    {
        public CustomerVariantEntry[] Variants;
    }

    [Serializable]
    public class CustomerVariantEntry
    {
        public CustomerVariant Variant;
        public GameObject Prefab;
    }
}
