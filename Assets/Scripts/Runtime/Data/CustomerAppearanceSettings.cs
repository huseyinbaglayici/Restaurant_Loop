using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    [System.Serializable]
    public class CustomerColorGroup
    {
        public string GroupName = "Material Slot";
        [Tooltip("Optional — if left empty, renderers are matched to this group by name (see CustomerVisualRandomizer).")]
        public Material TargetMaterial;
        public List<Color> PossibleColors = new();
    }

    [CreateAssetMenu(menuName = "RestaurantLoop/Customer Appearance Settings", fileName = "CustomerAppearanceSettings")]
    public class CustomerAppearanceSettings : ScriptableObject
    {
        [Header("Hair")]
        public List<Color> HairColors = new();

        [Header("Body / Outfit — matched to renderers by name (body, hair, tshirt, pants, shoes, eyes)")]
        public List<CustomerColorGroup> ColorGroups = new();
    }
}
