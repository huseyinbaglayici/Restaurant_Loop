using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace Editor.LevelEditor
{
    internal static class ConveyorSceneBuilder
    {
        public static void Build(ConveyorLevelDataSO data)
        {
            var settings = ConveyorSettingsProvider.GetOrCreate();
            if (settings.ConveyorStraightPrefab == null || settings.ConveyorCornerPrefab == null)
            {
                Debug.LogWarning(
                    "[ConveyorSceneBuilder] Assign both conveyor prefabs in RestaurantLoop/Level Editor Settings first.");
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
                if (cell.IsGate) continue; // gate: no physical piece

                var prefab = cell.Shape == ConveyorTileShape.Straight
                    ? settings.ConveyorStraightPrefab
                    : settings.ConveyorCornerPrefab;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
                instance.transform.localPosition = ConveyorWorldUtil.ToWorldPosition(cell.Coord, cellSize);

                var baseEuler = instance.transform.localRotation.eulerAngles;
                instance.transform.localRotation = Quaternion.Euler(baseEuler.x,
                    ConveyorWorldUtil.ToWorldYRotation(cell.Shape, cell.Rotation), baseEuler.z);
                Undo.RegisterCreatedObjectUndo(instance, "Build Conveyor");
            }

            Selection.activeGameObject = container;
        }
    }
}