using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace RestaurantLoop.Editor.LevelEditor
{
    internal static class ConveyorSettingsProvider
    {
        private const string AssetPath = "Assets/Resources/ConveyorPrefabSettings.asset";

        private static ConveyorPrefabSettings _instance;

        public static ConveyorPrefabSettings GetOrCreate()
        {
            if (_instance != null) return _instance;

            _instance = AssetDatabase.LoadAssetAtPath<ConveyorPrefabSettings>(AssetPath);
            if (_instance != null) return _instance;

            AssetFolderUtil.EnsureFolder("Assets/Resources");
            _instance = ScriptableObject.CreateInstance<ConveyorPrefabSettings>();
            AssetDatabase.CreateAsset(_instance, AssetPath);
            AssetDatabase.SaveAssets();
            return _instance;
        }
    }
}
