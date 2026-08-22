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
            w.minSize = new Vector2(360, 200);
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

            root.Bind(so);
        }
    }
}
