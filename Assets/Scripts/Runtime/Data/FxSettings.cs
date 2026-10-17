using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Effects;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    [CreateAssetMenu(fileName = "FxSettings", menuName = "RestaurantLoop/Fx Settings")]
    public class FxSettings : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public FxType Type;
            public GameObject Prefab;
            public float Scale = 1f;
            public int Prewarm = 1;
        }

        public List<Entry> Effects = new();

        public Entry Get(FxType type)
        {
            foreach (var entry in Effects)
                if (entry != null && entry.Type == type) return entry;
            return null;
        }
    }
}
