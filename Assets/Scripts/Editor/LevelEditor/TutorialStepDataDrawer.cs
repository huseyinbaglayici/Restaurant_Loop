using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace Editor.LevelEditor
{
    [CustomPropertyDrawer(typeof(TutorialStepData))]
    public class TutorialStepDataDrawer : PropertyDrawer
    {
        private const float LineHeight = 18f;
        private const float Spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var text = property.FindPropertyRelative(nameof(TutorialStepData.Text));
            var hoverKind = property.FindPropertyRelative(nameof(TutorialStepData.HoverKind));
            float textHeight = EditorGUI.GetPropertyHeight(text);
            int payloadRows = PayloadRowCount((TutorialHoverTargetKind)hoverKind.enumValueIndex);
            return textHeight + Spacing + (LineHeight + Spacing) * (2 + payloadRows);
        }

        private static int PayloadRowCount(TutorialHoverTargetKind kind) => kind switch
        {
            TutorialHoverTargetKind.PowerUpButton => 1,
            TutorialHoverTargetKind.QueueLane => 3, // lane index, row index, hint line
            TutorialHoverTargetKind.ConveyorCell => 1,
            _ => 0
        };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var text = property.FindPropertyRelative(nameof(TutorialStepData.Text));
            var trigger = property.FindPropertyRelative(nameof(TutorialStepData.Trigger));
            var hoverKind = property.FindPropertyRelative(nameof(TutorialStepData.HoverKind));
            var powerUpButton = property.FindPropertyRelative(nameof(TutorialStepData.PowerUpButton));
            var queueLaneIndex = property.FindPropertyRelative(nameof(TutorialStepData.QueueLaneIndex));
            var queueRowIndex = property.FindPropertyRelative(nameof(TutorialStepData.QueueRowIndex));
            var conveyorCell = property.FindPropertyRelative(nameof(TutorialStepData.ConveyorCell));

            var rect = new Rect(position.x, position.y, position.width, EditorGUI.GetPropertyHeight(text));
            EditorGUI.PropertyField(rect, text);
            rect.y += rect.height + Spacing;

            rect.height = LineHeight;
            EditorGUI.PropertyField(rect, trigger);
            rect.y += LineHeight + Spacing;

            EditorGUI.PropertyField(rect, hoverKind);
            rect.y += LineHeight + Spacing;

            switch ((TutorialHoverTargetKind)hoverKind.enumValueIndex)
            {
                case TutorialHoverTargetKind.PowerUpButton:
                    EditorGUI.PropertyField(rect, powerUpButton);
                    break;

                case TutorialHoverTargetKind.QueueLane:
                {
                    var level = property.serializedObject.targetObject as ConveyorLevelDataSO;
                    string laneLabel = level != null ? $"Queue Lane Index (0-{level.Queues.Count - 1})" : "Queue Lane Index";
                    EditorGUI.PropertyField(rect, queueLaneIndex, new GUIContent(laneLabel));
                    rect.y += LineHeight + Spacing;

                    EditorGUI.PropertyField(rect, queueRowIndex, new GUIContent("Queue Row Index (0 = front)"));
                    rect.y += LineHeight + Spacing;

                    EditorGUI.LabelField(rect, "Row > 0 requires Pick-From-Bottom active first.", EditorStyles.miniLabel);
                    break;
                }

                case TutorialHoverTargetKind.ConveyorCell:
                {
                    float buttonWidth = 90f;
                    var cellRect = new Rect(rect.x, rect.y, rect.width - buttonWidth - 4f, rect.height);
                    var buttonRect = new Rect(rect.xMax - buttonWidth, rect.y, buttonWidth, rect.height);

                    EditorGUI.PropertyField(cellRect, conveyorCell);

                    var level = property.serializedObject.targetObject as ConveyorLevelDataSO;
                    if (GUI.Button(buttonRect, "Pick on Grid") && level != null)
                    {
                        var so = property.serializedObject;
                        string propertyPath = conveyorCell.propertyPath;

                        ConveyorGridWindow.TryBeginCellPick(level, coord =>
                        {
                            so.Update();
                            so.FindProperty(propertyPath).vector2IntValue = coord;
                            so.ApplyModifiedProperties();
                        });
                    }
                    break;
                }
            }
        }
    }
}
