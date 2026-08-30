using System;
using UnityEngine;

namespace RestaurantLoop.Runtime.Data
{
    public enum TutorialTriggerCondition
    {
        OnGameplaySceneLoaded,
        OnFirstFoodOnConveyor,
        OnExitStackNearFull,
        OnFirstLevelFail
    }

    public enum TutorialHoverTargetKind
    {
        None,
        PowerUpButton,
        QueueLane,
        ConveyorCell
    }

    public enum PowerUpButtonId
    {
        Shuffle,
        PickFromBottom,
        ExtraTray
    }

    [Serializable]
    public class TutorialStepData
    {
        [TextArea] public string Text;
        public TutorialTriggerCondition Trigger;
        public TutorialHoverTargetKind HoverKind;

        // Only the field matching HoverKind is used.
        public PowerUpButtonId PowerUpButton;
        public int QueueLaneIndex;
        public int QueueRowIndex;
        public Vector2Int ConveyorCell;
    }
}
