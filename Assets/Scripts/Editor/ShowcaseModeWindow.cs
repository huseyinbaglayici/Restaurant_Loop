using System.Collections.Generic;
using System.Linq;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.SceneFlow;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RestaurantLoop.Editor
{
    public class ShowcaseModeWindow : EditorWindow
    {
        private const string BootstrapScenePath = "Assets/Scenes/BoostrapScene.unity";

        private bool _showcaseEnabled;
        private List<int> _playlist = new();
        private int _currentPlaylistIndex = 0;
        private Vector2 _scrollPos;
        private Vector2 _dbScrollPos;

        [MenuItem("RestaurantLoop/Showcase (Test Modu)...", false, 0)]
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

            // Mode Toggle Header Box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Sunum / Showcase Test Modu", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _showcaseEnabled = EditorGUILayout.ToggleLeft(" Showcase Modu Aktif", _showcaseEnabled, EditorStyles.boldLabel);
            if (EditorGUI.EndChangeCheck())
            {
                ShowcaseModeConfig.IsEnabled = _showcaseEnabled;
            }

            if (_showcaseEnabled)
            {
                EditorGUILayout.HelpBox("Showcase modu devrede:\n• Aşağıdaki oynatma listesindeki levellar sırasıyla açılır.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Normal mod aktif (Standart GameplayScene ve normal kayıt akışı geçerli).", MessageType.None);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // Quick Play Button from Bootstrap
            if (!Application.isPlaying)
            {
                GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
                if (GUILayout.Button("▶ Showcase'i Başlat (BoostrapScene)", GUILayout.Height(34)))
                {
                    ShowcaseModeConfig.IsEnabled = true;
                    _showcaseEnabled = true;
                    SavePlaylist();
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(BootstrapScenePath);
                    EditorApplication.isPlaying = true;
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(8);

            // Database Levels
            var db = Resources.Load<LevelDatabase>("LevelDatabase");
            var sortedLevels = (db != null && db.Levels != null) 
                ? db.Levels.Where(l => l != null).OrderBy(l => l.LevelId).ToList() 
                : new List<ConveyorLevelDataSO>();

            // Playlist Box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Seçili Level Sırası ({_playlist.Count} Level)", EditorStyles.boldLabel);

            if (GUILayout.Button("+ Tümünü Sırayla Ekle", GUILayout.Width(140)))
            {
                _playlist.Clear();
                foreach (var lvl in sortedLevels)
                {
                    _playlist.Add(lvl.LevelId);
                }
                _currentPlaylistIndex = 0;
                SavePlaylist();
            }

            if (GUILayout.Button("Listeyi Temizle", GUILayout.Width(95)))
            {
                _playlist.Clear();
                _currentPlaylistIndex = 0;
                SavePlaylist();
            }
            EditorGUILayout.EndHorizontal();

            if (_playlist.Count == 0)
            {
                EditorGUILayout.HelpBox("Henüz level eklenmedi. Aşağıdaki listeden istediğiniz sırayla levellara tıklayarak ekleyin.", MessageType.Warning);
            }
            else
            {
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(130));
                for (int i = 0; i < _playlist.Count; i++)
                {
                    int levelId = _playlist[i];
                    bool isCurrentPlaying = (i == _currentPlaylistIndex);

                    var levelData = sortedLevels.FirstOrDefault(l => l.LevelId == levelId);
                    string themeText = levelData != null ? $" ({levelData.Theme})" : "";
                    string diffText = "";
                    if (levelData != null)
                    {
                        if (levelData.Difficulty == LevelDifficulty.Hard) diffText = " [Zor]";
                        else if (levelData.Difficulty == LevelDifficulty.SuperHard) diffText = " [Çok Zor]";
                        else if (levelData.Difficulty == LevelDifficulty.Bonus) diffText = " [Bonus]";
                    }

                    EditorGUILayout.BeginHorizontal(EditorStyles.textArea);

                    GUI.backgroundColor = isCurrentPlaying ? new Color(0.4f, 0.9f, 0.4f) : Color.white;
                    string label = $"{i + 1}. Sıra: Level {levelId}{themeText}{diffText}" + (isCurrentPlaying ? "  [Oynanacak]" : "");
                    EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
                    GUI.backgroundColor = Color.white;

                    if (GUILayout.Button("Başlat", GUILayout.Width(60)))
                    {
                        _currentPlaylistIndex = i;
                        ShowcaseModeConfig.CurrentPlaylistIndex = i;
                        if (Application.isPlaying)
                        {
                            var provider = FindCurrentLevelProvider();
                            provider?.ResolveFromSave();
                            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                        }
                    }

                    GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
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
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // Database Levels to Add
            EditorGUILayout.LabelField("Veritabanındaki Levellar (Eklemek için tıklayın):", EditorStyles.boldLabel);

            if (db == null || db.Levels == null || db.Levels.Count == 0)
            {
                EditorGUILayout.HelpBox("Resources klasöründe LevelDatabase bulunamadı.", MessageType.Error);
                return;
            }

            _dbScrollPos = EditorGUILayout.BeginScrollView(_dbScrollPos);
            foreach (var level in sortedLevels)
            {
                string themeText = $" ({level.Theme})";
                string diffText = "";
                if (level.Difficulty == LevelDifficulty.Hard) diffText = " [Zor]";
                else if (level.Difficulty == LevelDifficulty.SuperHard) diffText = " [Çok Zor]";
                else if (level.Difficulty == LevelDifficulty.Bonus) diffText = " [Bonus]";

                string info = $"Level {level.LevelId}{themeText}{diffText}" + (level.IsTutorial ? " (Tutorial)" : "");

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button($"+ Listeye Ekle: {info}", GUILayout.Height(26)))
                {
                    _playlist.Add(level.LevelId);
                    SavePlaylist();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // Quick Play / Restart Actions
            if (Application.isPlaying)
            {
                if (GUILayout.Button("Mevcut Sahneyi Seçili Level ile Yeniden Başlat", GUILayout.Height(32)))
                {
                    SavePlaylist();
                    var provider = FindCurrentLevelProvider();
                    provider?.ResolveFromSave();
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
            }
        }

        private void SavePlaylist()
        {
            ShowcaseModeConfig.Playlist = _playlist;
            ShowcaseModeConfig.CurrentPlaylistIndex = _currentPlaylistIndex;
        }

        private static CurrentLevelProvider FindCurrentLevelProvider()
        {
            // Zenject Context'ten çözümlenebilirse
            return null;
        }
    }
}
