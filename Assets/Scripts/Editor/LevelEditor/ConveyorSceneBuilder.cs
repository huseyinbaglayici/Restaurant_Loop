using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Editor.LevelEditor
{
    internal static class ConveyorSceneBuilder
    {
        private const string EnvironmentHolderName = "LevelHolder";

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

            var tuning = Resources.Load<ConveyorTuning>("ConveyorTuning");
            float groundY = tuning != null ? tuning.BeltTrackHeight : 0f;
            var worldOrigin = tuning != null ? tuning.WorldOrigin : Vector3.zero;

            float cellSize = settings.CellWorldSize;
            foreach (var cell in data.ConveyorCells)
            {
                if (cell.IsGate) continue; // gate: no physical piece

                var prefab = cell.Shape == ConveyorTileShape.Straight
                    ? settings.ConveyorStraightPrefab
                    : settings.ConveyorCornerPrefab;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
                instance.transform.localPosition = ConveyorWorldUtil.ToWorldPosition(cell.Coord, cellSize, groundY, worldOrigin);

                var baseEuler = instance.transform.localRotation.eulerAngles;
                instance.transform.localRotation = Quaternion.Euler(baseEuler.x,
                    baseEuler.y + ConveyorWorldUtil.ToWorldYRotation(cell.Shape, cell.Rotation), baseEuler.z);
                Undo.RegisterCreatedObjectUndo(instance, "Build Conveyor");
            }

            var themeSettings = Resources.Load<ThemeSettings>("ThemeSettings");
            var themeEntry = themeSettings != null ? themeSettings.GetTheme(data.Theme) : null;
            var envPrefab = data.Environment != null ? data.Environment : themeEntry?.EnvironmentPrefab;

            if (envPrefab != null) ApplyEnvironmentPreset(envPrefab);

            Selection.activeGameObject = container;
        }

        private static void ApplyEnvironmentPreset(GameObject presetPrefab)
        {
            var holder = GameObject.Find(EnvironmentHolderName);
            if (holder == null)
            {
                Debug.LogWarning($"[ConveyorSceneBuilder] No '{EnvironmentHolderName}' found in scene — environment preset not applied.");
                return;
            }

            foreach (Transform child in holder.transform)
            {
                bool shouldBeActive = PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject) == presetPrefab;
                if (child.gameObject.activeSelf == shouldBeActive) continue;

                Undo.RecordObject(child.gameObject, "Switch Environment Preset");
                child.gameObject.SetActive(shouldBeActive);
            }

            EditorSceneManager.MarkSceneDirty(holder.scene);
        }
    }
}