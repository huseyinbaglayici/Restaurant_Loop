using System.Collections.Generic;
using System.Linq;
using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RestaurantLoop.Editor
{
    public class ShowcaseModeWindow : EditorWindow
    {
        private const string BootstrapScenePath = "Assets/Scenes/BoostrapScene.unity";

        private static readonly Color ActiveRowColor = new(0.4f, 0.9f, 0.4f);
        private static readonly Color StartButtonColor = new(0.3f, 0.85f, 0.4f);
        private static readonly Color RemoveButtonColor = new(1f, 0.5f, 0.5f);

        private bool _showcaseEnabled;
        private List<int> _playlist = new();
        private int _currentPlaylistIndex = 0;
        private Vector2 _scrollPos;
        private Vector2 _dbScrollPos;

        [MenuItem("RestaurantLoop/Showcase Mode...", false, 0)]
        public static void Open()
        {
            var window = GetWindow<ShowcaseModeWindow>("Showcase Setup");
            window.minSize = new Vector2(380, 560);
            window.Show();
        }

        private void OnEnable()
        {
            _showcaseEnabled = ShowcaseModeConfig.IsEnabled;
            _playlist = ShowcaseModeConfig.Playlist;
            _currentPlaylistIndex = ShowcaseModeConfig.CurrentPlaylistIndex;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            DrawModeToggle();

            EditorGUILayout.Space(8);
            if (!Application.isPlaying) DrawStartButton();

            EditorGUILayout.Space(8);

            var db = Resources.Load<LevelDatabase>("LevelDatabase");
            var sortedLevels = (db != null && db.Levels != null)
                ? db.Levels.Where(l => l != null).OrderBy(l => l.LevelId).ToList()
                : new List<ConveyorLevelDataSO>();

            DrawPlaylist(sortedLevels);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Levels in database (click to add):", EditorStyles.boldLabel);

            if (db == null || db.Levels == null || db.Levels.Count == 0)
            {
                EditorGUILayout.HelpBox("LevelDatabase not found in Resources.", MessageType.Error);
                return;
            }

            DrawDatabaseLevels(sortedLevels);

            EditorGUILayout.Space(10);
            if (Application.isPlaying && GUILayout.Button("Restart Current Scene with Selected Level", GUILayout.Height(32)))
            {
                SavePlaylist();
                ReloadActiveScene();
            }
        }

        private void DrawModeToggle()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Showcase Test Mode", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _showcaseEnabled = EditorGUILayout.ToggleLeft(" Showcase Mode Enabled", _showcaseEnabled, EditorStyles.boldLabel);
            if (EditorGUI.EndChangeCheck())
                ShowcaseModeConfig.IsEnabled = _showcaseEnabled;

            if (_showcaseEnabled)
                EditorGUILayout.HelpBox("Showcase mode is on:\n• Levels in the playlist below are played in order.", MessageType.Info);
            else
                EditorGUILayout.HelpBox("Normal mode (standard GameplayScene and regular save flow).", MessageType.None);

            EditorGUILayout.EndVertical();
        }

        private void DrawStartButton()
        {
            GUI.backgroundColor = StartButtonColor;
            if (GUILayout.Button("▶ Start Showcase (BoostrapScene)", GUILayout.Height(34)))
            {
                ShowcaseModeConfig.IsEnabled = true;
                _showcaseEnabled = true;
                SavePlaylist();
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(BootstrapScenePath);
                EditorApplication.isPlaying = true;
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawPlaylist(List<ConveyorLevelDataSO> sortedLevels)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Playlist ({_playlist.Count} levels)", EditorStyles.boldLabel);

            if (GUILayout.Button("+ Add All in Order", GUILayout.Width(140)))
            {
                _playlist.Clear();
                foreach (var lvl in sortedLevels) _playlist.Add(lvl.LevelId);
                _currentPlaylistIndex = 0;
                SavePlaylist();
            }

            if (GUILayout.Button("Clear List", GUILayout.Width(95)))
            {
                _playlist.Clear();
                _currentPlaylistIndex = 0;
                SavePlaylist();
            }
            EditorGUILayout.EndHorizontal();

            if (_playlist.Count == 0)
            {
                EditorGUILayout.HelpBox("No levels added yet. Click levels in the list below, in the order you want them.", MessageType.Warning);
            }
            else
            {
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(130));
                for (int i = 0; i < _playlist.Count; i++)
                    DrawPlaylistRow(i, sortedLevels);
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawPlaylistRow(int i, List<ConveyorLevelDataSO> sortedLevels)
        {
            int levelId = _playlist[i];
            bool isCurrent = i == _currentPlaylistIndex;

            var levelData = sortedLevels.FirstOrDefault(l => l.LevelId == levelId);
            string details = levelData != null ? $" ({levelData.Theme}){DifficultyTag(levelData.Difficulty)}" : "";

            EditorGUILayout.BeginHorizontal(EditorStyles.textArea);

            GUI.backgroundColor = isCurrent ? ActiveRowColor : Color.white;
            string label = $"{i + 1}. Level {levelId}{details}" + (isCurrent ? "  [Next]" : "");
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Play", GUILayout.Width(60)))
            {
                _currentPlaylistIndex = i;
                ShowcaseModeConfig.CurrentPlaylistIndex = i;
                if (Application.isPlaying) ReloadActiveScene();
            }

            GUI.backgroundColor = RemoveButtonColor;
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                _playlist.RemoveAt(i);
                if (_currentPlaylistIndex >= _playlist.Count)
                    _currentPlaylistIndex = Mathf.Max(0, _playlist.Count - 1);
                SavePlaylist();
                GUIUtility.ExitGUI();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDatabaseLevels(List<ConveyorLevelDataSO> sortedLevels)
        {
            _dbScrollPos = EditorGUILayout.BeginScrollView(_dbScrollPos);
            foreach (var level in sortedLevels)
            {
                string info = $"Level {level.LevelId} ({level.Theme}){DifficultyTag(level.Difficulty)}" + (level.IsTutorial ? " (Tutorial)" : "");

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button($"+ Add to List: {info}", GUILayout.Height(26)))
                {
                    _playlist.Add(level.LevelId);
                    SavePlaylist();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private static string DifficultyTag(LevelDifficulty difficulty) => difficulty switch
        {
            LevelDifficulty.Hard => " [Hard]",
            LevelDifficulty.SuperHard => " [Super Hard]",
            LevelDifficulty.Bonus => " [Bonus]",
            _ => ""
        };

        private static void ReloadActiveScene() =>
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        private void SavePlaylist()
        {
            ShowcaseModeConfig.Playlist = _playlist;
            ShowcaseModeConfig.CurrentPlaylistIndex = _currentPlaylistIndex;
        }
    }
}
