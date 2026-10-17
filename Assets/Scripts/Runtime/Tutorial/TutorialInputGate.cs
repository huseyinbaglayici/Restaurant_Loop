using System;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Signals;
using Zenject;

namespace RestaurantLoop.Runtime.Tutorial
{
    // Decoupled from TutorialController — only talks to it via signals, so it's free for
    // non-tutorial levels (no signal ever arrives, _requiredStep stays null).
    public class TutorialInputGate : IInitializable, IDisposable
    {
        private readonly SignalBus _signalBus;
        private TutorialStepData _requiredStep;

        public TutorialInputGate(SignalBus signalBus) => _signalBus = signalBus;

        public void Initialize()
        {
            _signalBus.Subscribe<TutorialStepActivatedSignal>(OnStepActivated);
            _signalBus.Subscribe<TutorialEndedSignal>(OnTutorialEnded);
        }

        public void Dispose()
        {
            _signalBus.TryUnsubscribe<TutorialStepActivatedSignal>(OnStepActivated);
            _signalBus.TryUnsubscribe<TutorialEndedSignal>(OnTutorialEnded);
        }

        private void OnStepActivated(TutorialStepActivatedSignal signal) => _requiredStep = signal.Step;
        private void OnTutorialEnded(TutorialEndedSignal signal) => _requiredStep = null;

        private bool IsRestricting => _requiredStep != null;

        public bool CanClickQueueLane(int lane, int row) => !IsRestricting ||
            (_requiredStep.HoverKind == TutorialHoverTargetKind.QueueLane &&
             _requiredStep.QueueLaneIndex == lane && _requiredStep.QueueRowIndex == row);

        public bool CanClickPowerUpButton(PowerUpButtonId id) => !IsRestricting ||
            (_requiredStep.HoverKind == TutorialHoverTargetKind.PowerUpButton && _requiredStep.PowerUpButton == id);

        // Exit stack has no hover-target kind of its own — block it like any unrepresented action
        // while a step is actively restricting input.
        public bool CanClickExitStack() => !IsRestricting;
    }
}
