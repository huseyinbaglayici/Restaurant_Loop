using System;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Signals;
using Zenject;

namespace RestaurantLoop.Runtime.Tutorial
{
    // Restricts input to what the active tutorial step expects. Driven only by tutorial signals,
    // so it never restricts anything on non-tutorial levels.
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

        // The active step waits for exactly this button — its press must always go through, for free.
        public bool IsPowerUpRequired(PowerUpButtonId id) => IsRestricting &&
            _requiredStep.HoverKind == TutorialHoverTargetKind.PowerUpButton && _requiredStep.PowerUpButton == id;

        // The exit stack has no hover-target kind, so it is blocked while any step is active.
        public bool CanClickExitStack() => !IsRestricting;
    }
}
