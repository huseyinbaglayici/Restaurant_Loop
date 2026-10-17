using System.Linq;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.SceneFlow;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RestaurantLoop.Editor
{
    public class SelectLevelWindow : EditorWindow
    {
        private int _selectedLevelId = 1;
        private Vector2 _scrollPos;

        [MenuItem("RestaurantLoop/Test Mode/Select Level...", false, 1)]
        public static void Open()
        {
            var window = GetWindow<SelectLevelWindow>("Level Selector");
            window.minSize = new Vector2(340, 420);
            window.Show();
        }

        private void OnEnable()
        {
            _selectedLevelId = GetSavedLevelId();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            int currentSaved = GetSavedLevelId();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Level Selection (Test Mode)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Current Saved Level: Level {currentSaved}", EditorStyles.label);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            _selectedLevelId = EditorGUILayout.IntField("Target Level ID:", _selectedLevelId);
            if (GUILayout.Button("Set Level", GUILayout.Width(100)))
            {
                ApplyLevel(_selectedLevelId);
            }
            EditorGUILayout.EndHorizontal();

            if (Application.isPlaying)
            {
                if (GUILayout.Button("Restart Current Scene at Target Level", GUILayout.Height(30)))
                {
                    ApplyLevel(_selectedLevelId);
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
            }

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Available Levels in Database:", EditorStyles.boldLabel);

            var db = Resources.Load<LevelDatabase>("LevelDatabase");
            if (db == null || db.Levels == null || db.Levels.Count == 0)
            {
                EditorGUILayout.HelpBox("LevelDatabase not found in Resources or contains no levels.", MessageType.Warning);
                return;
            }

            var sortedLevels = db.Levels.Where(l => l != null).OrderBy(l => l.LevelId).ToList();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            foreach (var level in sortedLevels)
            {
                bool isCurrent = level.LevelId == currentSaved;
                GUI.backgroundColor = isCurrent ? new Color(0.4f, 0.9f, 0.4f) : Color.white;

                string label = $"Level {level.LevelId}" + (level.IsTutorial ? " (Tutorial)" : "") + (isCurrent ? "  [ACTIVE]" : "");

                if (GUILayout.Button(label, GUILayout.Height(28)))
                {
                    _selectedLevelId = level.LevelId;
                    ApplyLevel(level.LevelId);

                    if (Application.isPlaying)
                    {
                        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                    }
                }

                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndScrollView();
        }

        private static int GetSavedLevelId()
        {
            return ES3.KeyExists(CurrentLevelProvider.LevelIdKey) ? ES3.Load<int>(CurrentLevelProvider.LevelIdKey) : 1;
        }

        public static void ApplyLevel(int levelId)
        {
            ES3.Save(CurrentLevelProvider.LevelIdKey, levelId);
            Debug.Log($"[RestaurantLoop] Set Current Saved Level to: Level {levelId}");
        }
    }
}
