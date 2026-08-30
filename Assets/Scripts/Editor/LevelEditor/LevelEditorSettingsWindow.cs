using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor.LevelEditor
{
    public class LevelEditorSettingsWindow : EditorWindow
    {
        [MenuItem("RestaurantLoop/Level Editor Settings")]
        public static void ShowWindow()
        {
            var w = GetWindow<LevelEditorSettingsWindow>();
            w.titleContent = new GUIContent("Level Editor Settings");
            w.minSize = new Vector2(360, 260);
        }

        public void CreateGUI()
        {
            var settings = ConveyorSettingsProvider.GetOrCreate();
            var so = new SerializedObject(settings);

            var root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingTop = 8;
            root.style.paddingRight = 8;

            var header = new Label("Conveyor");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 4;
            root.Add(header);

            root.Add(new PropertyField(so.FindProperty("ConveyorStraightPrefab"), "Straight Prefab"));
            root.Add(new PropertyField(so.FindProperty("ConveyorCornerPrefab"), "Corner Prefab"));
            root.Add(new PropertyField(so.FindProperty("CellWorldSize"), "Cell World Size"));

            var envHeader = new Label("Environment Presets");
            envHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            envHeader.style.marginTop = 12;
            envHeader.style.marginBottom = 4;
            root.Add(envHeader);

            root.Add(new PropertyField(so.FindProperty("EnvironmentPresets"), "Presets"));

            root.Bind(so);
        }
    }
}
