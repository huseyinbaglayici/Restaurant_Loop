using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace Editor.LevelEditor
{
    /// <summary>
    /// Editor-side get-or-create for <see cref="ConveyorPrefabSettings"/>. Lives here (not on the
    /// Runtime type itself) because AssetDatabase isn't available outside the editor — the asset
    /// path is under Resources/ so PoolInstaller can Resources.Load the same file at runtime.
    /// </summary>
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
