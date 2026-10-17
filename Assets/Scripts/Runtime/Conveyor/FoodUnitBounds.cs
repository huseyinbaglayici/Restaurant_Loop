using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Measures food prefabs without instantiating them, so stacked units can sit exactly one height apart.
    public static class FoodUnitBounds
    {
        private const float DefaultUnitHeight = 0.25f;

        // Uninstantiated sprite prefabs can report heights around 20 units; anything taller is clamped.
        private const float MaxUnitHeight = 0.6f;

        // Height along world Y after the unit's fixed orientation and scale, so a prefab laid on its side
        // stacks by its new height, not the authored one.
        public static float ResolveUnitHeight(GameObject prefab, Quaternion appliedRotation, Vector3 scale)
        {
            var bounds = CombinedLocalBounds(prefab);
            if (!bounds.HasValue) return DefaultUnitHeight;

            var size = appliedRotation * Vector3.Scale(bounds.Value.size, scale);
            float height = Mathf.Abs(size.y);
            if (height > MaxUnitHeight) height = DefaultUnitHeight;
            return height > 0.0001f ? height : DefaultUnitHeight;
        }

        // Every child mesh and sprite in the prefab root's local space, so composite visuals are covered.
        private static Bounds? CombinedLocalBounds(GameObject prefab)
        {
            if (prefab == null) return null;

            var meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
            var spriteRenderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
            if (meshFilters.Length == 0 && spriteRenderers.Length == 0) return null;

            var worldToRoot = prefab.transform.worldToLocalMatrix;
            Bounds? combined = null;

            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh == null) continue;
                Encapsulate(ref combined, meshFilter.sharedMesh.bounds, worldToRoot * meshFilter.transform.localToWorldMatrix);
            }

            foreach (var spriteRenderer in spriteRenderers)
            {
                if (spriteRenderer.sprite == null) continue;
                Encapsulate(ref combined, spriteRenderer.sprite.bounds, worldToRoot * spriteRenderer.transform.localToWorldMatrix);
            }

            return combined;
        }

        // Transforms all 8 corners, so rotated children still give a correct axis-aligned box.
        private static void Encapsulate(ref Bounds? combined, Bounds source, Matrix4x4 toRoot)
        {
            var extents = source.extents;
            var local = new Bounds(toRoot.MultiplyPoint3x4(source.center), Vector3.zero);

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                var corner = source.center + Vector3.Scale(extents, new Vector3(x, y, z));
                local.Encapsulate(toRoot.MultiplyPoint3x4(corner));
            }

            if (combined.HasValue)
            {
                var merged = combined.Value;
                merged.Encapsulate(local);
                combined = merged;
            }
            else
            {
                combined = local;
            }
        }
    }
}
