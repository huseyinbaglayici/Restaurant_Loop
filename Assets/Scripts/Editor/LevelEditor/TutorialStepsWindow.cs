using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace RestaurantLoop.Editor.LevelEditor
{
    public class TutorialStepsWindow : EditorWindow
    {
        private ConveyorLevelDataSO _draft;

        public static void Open(ConveyorLevelDataSO draft)
        {
            var w = GetWindow<TutorialStepsWindow>();
            w.titleContent = new GUIContent("Tutorial Steps");
            w.minSize = new Vector2(360, 300);
            w._draft = draft;
            w.Rebuild();
        }

        public void CreateGUI() => Rebuild();

        private void Rebuild()
        {
            rootVisualElement.Clear();
            if (_draft == null)
            {
                rootVisualElement.Add(new Label("Open this from the Conveyor Grid Tool."));
                return;
            }

            var serializedDraft = new SerializedObject(_draft);
            var stepsProperty = serializedDraft.FindProperty(nameof(ConveyorLevelDataSO.TutorialSteps));

            var listView = new ListView
            {
                bindingPath = stepsProperty.propertyPath,
                reorderable = true,
                showAddRemoveFooter = true,
                showBorder = true,
                showBoundCollectionSize = false,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                makeItem = () => new PropertyField(),
                bindItem = (element, index) =>
                    ((PropertyField)element).BindProperty(stepsProperty.GetArrayElementAtIndex(index))
            };
            listView.style.flexGrow = 1;
            listView.Bind(serializedDraft);

            rootVisualElement.Add(listView);
        }
    }
}
