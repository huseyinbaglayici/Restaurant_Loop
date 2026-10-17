using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Fixed-child-slot food visual (e.g. a drink shelf of pre-placed bottles): one instance is kept for the
    // item's whole life and Amount toggles how many slot children are visible. Owned by FoodStackBuilder.
    public class FoodRackVisual
    {
        private readonly Transform _owner;

        private GameObject _instance;
        private GameObject _sourcePrefab;
        private Vector3 _baseScale = Vector3.one;
        private Quaternion _baseRotation = Quaternion.identity;
        private float _appliedScale = -1f;

        public FoodRackVisual(Transform owner)
        {
            _owner = owner;
        }

        public void Apply(GameObject sourcePrefab, Vector3 rotationEuler, int amount, float scale)
        {
            if (sourcePrefab != _sourcePrefab)
            {
                DestroyInstance();
                _sourcePrefab = sourcePrefab;
                _baseScale = sourcePrefab != null ? sourcePrefab.transform.localScale : Vector3.one;
                _baseRotation = sourcePrefab != null ? sourcePrefab.transform.localRotation : Quaternion.identity;
            }

            if (_instance == null)
            {
                _instance = Object.Instantiate(sourcePrefab, _owner);
                _instance.transform.localPosition = Vector3.zero;
            }

            _instance.transform.localRotation = _baseRotation * Quaternion.Euler(rotationEuler);
            _instance.transform.localScale = _baseScale * scale;
            _appliedScale = scale;

            var container = ResolveSlotContainer();
            int slotCount = container != null ? container.childCount : 0;
            // Amount 0 stays visible, same rule as stacked units (authored decoy stacks).
            int visibleCount = slotCount == 0 ? 0 : Mathf.Clamp(amount, 1, slotCount);

            for (int i = 0; i < slotCount; i++)
                container.GetChild(i).gameObject.SetActive(i < visibleCount);
        }

        public void RefreshScale(float scale)
        {
            if (Mathf.Approximately(scale, _appliedScale)) return;

            if (_instance != null) _instance.transform.localScale = _baseScale * scale;
            _appliedScale = scale;
        }

        public bool TryGetTopY(out float topY)
        {
            topY = float.MinValue;
            if (_instance == null) return false;

            bool found = false;
            var renderers = _instance.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || !renderers[i].gameObject.activeInHierarchy) continue;

                topY = Mathf.Max(topY, renderers[i].bounds.max.y);
                found = true;
            }

            return found;
        }

        // Slots only ever hide from the last child down, so the last child is the one that just went invisible.
        public Transform TakeSlotChild()
        {
            var container = ResolveSlotContainer();
            if (container == null || container.childCount == 0) return null;

            var child = container.GetChild(container.childCount - 1);
            child.gameObject.SetActive(true);
            child.SetParent(null, worldPositionStays: true);
            return child;
        }

        public void SetActive(bool active)
        {
            if (_instance != null) _instance.SetActive(active);
        }

        public void Clear()
        {
            DestroyInstance();
            _sourcePrefab = null;
        }

        private void DestroyInstance()
        {
            if (_instance == null) return;

            Object.Destroy(_instance);
            _instance = null;
        }

        // Walks down single-child wrappers (pivot/orientation fixes) to the node that holds the slots side by side.
        private Transform ResolveSlotContainer()
        {
            if (_instance == null) return null;

            var node = _instance.transform;
            while (node.childCount == 1) node = node.GetChild(0);
            return node;
        }
    }
}
