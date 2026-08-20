using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace Editor.LevelEditor
{
    /// <summary>
    /// Spawns the conveyor cells from a <see cref="ConveyorLevelDataSO"/> into the active scene
    /// as real prefab instances, using the prefabs assigned in <see cref="ConveyorPrefabSettings"/>.
    /// Editor-only preview — the runtime spawner does the equivalent from the object pool, using
    /// the same <see cref="ConveyorWorldUtil"/> math so the two never disagree.
    /// </summary>
    internal static class ConveyorSceneBuilder
    {
        public static void Build(ConveyorLevelDataSO data)
        {
            var settings = ConveyorSettingsProvider.GetOrCreate();
            if (settings.ConveyorStraightPrefab == null || settings.ConveyorCornerPrefab == null)
            {
                Debug.LogWarning("[ConveyorSceneBuilder] Assign both conveyor prefabs in RestaurantLoop/Level Editor Settings first.");
                return;
            }

            string containerName = $"ConveyorLevel_{data.LevelId}";
            var existing = GameObject.Find(containerName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var container = new GameObject(containerName);
            Undo.RegisterCreatedObjectUndo(container, "Build Conveyor");

            float cellSize = settings.CellWorldSize;
            foreach (var cell in data.ConveyorCells)
            {
                // The Gate is a logical seam (spawn/despawn point for the baked path), not a
                // physical belt segment — it still counts for connectivity, but no mesh here.
                if (cell.IsGate) continue;

                var prefab = cell.Shape == ConveyorTileShape.Straight
                    ? settings.ConveyorStraightPrefab
                    : settings.ConveyorCornerPrefab;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
                instance.transform.localPosition = ConveyorWorldUtil.ToWorldPosition(cell.Coord, cellSize);

                // Keep whatever X/Z tilt the prefab was authored with — only Y (facing) is ours to set.
                var baseEuler = instance.transform.localRotation.eulerAngles;
                instance.transform.localRotation = Quaternion.Euler(baseEuler.x, ConveyorWorldUtil.ToWorldYRotation(cell.Shape, cell.Rotation), baseEuler.z);
                Undo.RegisterCreatedObjectUndo(instance, "Build Conveyor");
            }

            Selection.activeGameObject = container;
        }
    }
}
