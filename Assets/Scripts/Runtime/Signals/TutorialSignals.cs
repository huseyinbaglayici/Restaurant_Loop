using RestaurantLoop.Runtime.Data;

namespace RestaurantLoop.Runtime.Signals
{
    public class TutorialStepActivatedSignal
    {
        public TutorialStepData Step;
    }

    public class TutorialEndedSignal
    {
    }

    public class TutorialActionPerformedSignal
    {
        public TutorialHoverTargetKind Kind;
        public PowerUpButtonId PowerUpButton;
        public int QueueLaneIndex;
        public int QueueRowIndex;
    }
}
