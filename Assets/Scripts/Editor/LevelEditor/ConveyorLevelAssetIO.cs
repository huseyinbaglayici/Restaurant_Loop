using System.Collections.Generic;
using System.Linq;
using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace RestaurantLoop.Editor.LevelEditor
{
    // Saves a level draft to its ConveyorLevel_{id} asset (baking the belt path and registering it
    // in the LevelDatabase) and loads an asset back into the draft.
    internal static class ConveyorLevelAssetIO
    {
        private const string LevelFolder = "Assets/Data/Levels";
        private const string LevelDatabasePath = "Assets/Resources/LevelDatabase.asset";
        private const string LogPrefix = "[ConveyorGridWindow]";

        public static void Save(ConveyorLevelDataSO draft)
        {
            AssetFolderUtil.EnsureFolder(LevelFolder);
            string path = PathFor(draft.LevelId);

            var asset = AssetDatabase.LoadAssetAtPath<ConveyorLevelDataSO>(path);
            bool isNew = asset == null;
            if (isNew) asset = ScriptableObject.CreateInstance<ConveyorLevelDataSO>();

            asset.LevelId = draft.LevelId;
            asset.Difficulty = draft.Difficulty;
            asset.Theme = draft.Theme;
            asset.GridWidth = draft.GridWidth;
            asset.GridHeight = draft.GridHeight;
            asset.ConveyorCells = new List<ConveyorCellData>(draft.ConveyorCells);
            asset.ConveyorEntities = new List<ConveyorEntityPlacement>(draft.ConveyorEntities);
            asset.Customers = new List<CustomerEntity>(draft.Customers);
            asset.Queues = draft.Queues.Select(q => new QueueLaneData { Entries = new List<FoodStackData>(q.Entries) })
                .ToList();
            asset.IsTutorial = draft.IsTutorial;
            asset.TutorialSteps = CloneTutorialSteps(draft.TutorialSteps);

            if (ConveyorPathBaker.TryBake(draft, out var orderedPath, out var bakeError))
            {
                asset.BakedPath.OrderedPath = orderedPath;
                Debug.Log($"{LogPrefix} Baked path: {orderedPath.Count} cells — {string.Join(" -> ", orderedPath)}");
            }
            else
            {
                asset.BakedPath.OrderedPath.Clear();
                Debug.LogWarning($"{LogPrefix} Bake failed, saving without a path: {bakeError}");
            }

            if (isNew) AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RegisterInDatabase(asset);

            Debug.Log($"{LogPrefix} Saved level {asset.LevelId} to {path}");
        }

        public static bool TryLoadInto(ConveyorLevelDataSO draft, int levelId)
        {
            string path = PathFor(levelId);
            var loaded = AssetDatabase.LoadAssetAtPath<ConveyorLevelDataSO>(path);
            if (loaded == null)
            {
                Debug.LogWarning($"{LogPrefix} No level asset found at {path}");
                return false;
            }

            Undo.RecordObject(draft, "Load Level");
            draft.LevelId = loaded.LevelId;
            draft.Difficulty = loaded.Difficulty;
            draft.Theme = loaded.Theme;
            draft.GridWidth = loaded.GridWidth;
            draft.GridHeight = loaded.GridHeight;
            draft.ConveyorCells = new List<ConveyorCellData>(loaded.ConveyorCells);
            draft.ConveyorEntities = new List<ConveyorEntityPlacement>(loaded.ConveyorEntities);
            draft.Customers = new List<CustomerEntity>(loaded.Customers);
            draft.Queues = Enumerable.Range(0, ConveyorLevelEdits.QueueLaneCount)
                .Select(i => i < loaded.Queues.Count
                    ? new QueueLaneData { Entries = new List<FoodStackData>(loaded.Queues[i].Entries) }
                    : new QueueLaneData())
                .ToList();
            draft.IsTutorial = loaded.IsTutorial;
            draft.TutorialSteps = CloneTutorialSteps(loaded.TutorialSteps);

            Debug.Log($"{LogPrefix} Loaded level {draft.LevelId} from {path}");
            return true;
        }

        private static string PathFor(int levelId) => $"{LevelFolder}/ConveyorLevel_{levelId}.asset";

        private static List<TutorialStepData> CloneTutorialSteps(List<TutorialStepData> steps) =>
            steps.Select(s => new TutorialStepData
                {
                    Text = s.Text, Trigger = s.Trigger, HoverKind = s.HoverKind,
                    PowerUpButton = s.PowerUpButton, QueueLaneIndex = s.QueueLaneIndex,
                    QueueRowIndex = s.QueueRowIndex, ConveyorCell = s.ConveyorCell
                })
                .ToList();

        // replaces an existing entry with the same LevelId, otherwise appends
        private static void RegisterInDatabase(ConveyorLevelDataSO asset)
        {
            var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(LevelDatabasePath);
            if (database == null)
            {
                Debug.LogWarning($"{LogPrefix} No LevelDatabase found at {LevelDatabasePath} — level not registered.");
                return;
            }

            var so = new SerializedObject(database);
            var levels = so.FindProperty("_levels");

            for (int i = 0; i < levels.arraySize; i++)
            {
                var element = levels.GetArrayElementAtIndex(i);
                var existing = element.objectReferenceValue as ConveyorLevelDataSO;

                if (existing == asset) return;

                if (existing != null && existing.LevelId == asset.LevelId)
                {
                    element.objectReferenceValue = asset;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(database);
                    AssetDatabase.SaveAssets();
                    return;
                }
            }

            levels.InsertArrayElementAtIndex(levels.arraySize);
            levels.GetArrayElementAtIndex(levels.arraySize - 1).objectReferenceValue = asset;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }
    }
}
