using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Picks a hair variant + per-part color combo per spawn. Ported from TestCustomer /
    // TestCustomerController — kept to MaterialPropertyBlock only (no material instancing,
    // so SRP batching across the pooled crowd stays intact).
    public class CustomerVisualRandomizer : MonoBehaviour
    {
        [Inject] private CustomerAppearanceSettings _settings;

        [SerializeField] private GameObject[] _hairVariants;

        private List<Renderer> _bodyRenderers;
        private MaterialPropertyBlock _block;

        public void Randomize()
        {
            _block ??= new MaterialPropertyBlock();

            RandomizeHair();
            RandomizeBodyColors();
        }

        private void RandomizeHair()
        {
            if (_hairVariants == null || _hairVariants.Length == 0) AutoFindHairVariants();
            if (_hairVariants == null || _hairVariants.Length == 0) return;

            int selectedIndex = Random.Range(0, _hairVariants.Length);
            Color? selectedColor = PickHairColor();

            for (int i = 0; i < _hairVariants.Length; i++)
            {
                var hair = _hairVariants[i];
                if (hair == null) continue;

                bool isActive = i == selectedIndex;
                hair.SetActive(isActive);
                if (isActive && selectedColor.HasValue) ApplyColor(hair, selectedColor.Value);
            }
        }

        private void AutoFindHairVariants()
        {
            Transform hairRoot = transform.Find("Hair");
            if (hairRoot == null)
            {
                foreach (var t in GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.ToLower().Contains("hair") && t.childCount > 0)
                    {
                        hairRoot = t;
                        break;
                    }
                }
            }

            if (hairRoot == null) return;

            var hairs = new List<GameObject>();
            for (int i = 0; i < hairRoot.childCount; i++) hairs.Add(hairRoot.GetChild(i).gameObject);
            _hairVariants = hairs.ToArray();
        }

        private Color? PickHairColor()
        {
            var colors = _settings.HairColors;
            if (colors == null || colors.Count == 0) return null;
            return colors[Random.Range(0, colors.Count)];
        }

        private void RandomizeBodyColors()
        {
            var groups = _settings.ColorGroups;
            if (groups == null || groups.Count == 0) return;

            _bodyRenderers ??= new List<Renderer>(GetComponentsInChildren<Renderer>(true));

            foreach (var renderer in _bodyRenderers)
            {
                if (renderer == null || renderer.GetComponentInParent<Canvas>() != null) continue;

                int groupIndex = GetGroupIndexForRenderer(renderer, groups);
                if (groupIndex < 0 || groupIndex >= groups.Count) continue;

                var group = groups[groupIndex];
                if (group?.PossibleColors == null || group.PossibleColors.Count == 0) continue;

                Color chosen = group.PossibleColors[Random.Range(0, group.PossibleColors.Count)];
                ApplyColor(renderer, chosen);
            }
        }

        private void ApplyColor(GameObject obj, Color color)
        {
            Renderer renderer = obj.GetComponent<Renderer>() ?? obj.GetComponentInChildren<Renderer>();
            if (renderer != null) ApplyColor(renderer, color);
        }

        private void ApplyColor(Renderer renderer, Color color)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", color);
            _block.SetColor("_Color", color);
            renderer.SetPropertyBlock(_block);
        }

        // name-based match: eyes / body-skin / hair / t-shirt / pants / shoes — falls back to
        // TargetMaterial comparison if the renderer's name doesn't say what it is.
        private static int GetGroupIndexForRenderer(Renderer r, List<CustomerColorGroup> groups)
        {
            string go = r.gameObject.name.ToLowerInvariant().Trim();

            if (go.Contains("eyebrow") || go.Contains("kas")) return -1;
            if (go.Contains("eyes") || go.Contains("eye") || go.Contains("goz") || go.Contains("göz")) return 5;
            if (go.Contains("body") || go.Contains("skin") || go.Contains("ten") || go.Contains("head")) return 0;
            if (go.Contains("hair") || go.Contains("sac") || go.Contains("saç")) return 1;
            if (go.Contains("tshirt") || go.Contains("t-shirt") || go.Contains("shirt") || go.Contains("top") || go.Contains("ust") || go.Contains("üst")) return 2;
            if (go.Contains("pants") || go.Contains("pant") || go.Contains("bottom") || go.Contains("trouser") || go.Contains("alt")) return 3;
            if (go.Contains("shoes") || go.Contains("shoe") || go.Contains("feet") || go.Contains("ayak")) return 4;

            Material mat = r.sharedMaterial;
            if (mat != null)
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    if (groups[i].TargetMaterial != null && mat.name.StartsWith(groups[i].TargetMaterial.name))
                        return i;
                }
            }

            return -1;
        }
    }
}
