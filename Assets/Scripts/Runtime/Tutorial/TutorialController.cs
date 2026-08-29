using System;
using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Tutorial
{
    // Plays a tutorial level's ordered TutorialSteps: each step waits for its trigger, then either
    // a matching gameplay action or a blocker tap advances it. Non-tutorial levels are untouched —
    // Initialize() bails out immediately and no signal is ever fired.
    public class TutorialController : IInitializable, IDisposable
    {
        private readonly ConveyorLevelDataSO _level;
        private readonly SignalBus _signalBus;
        private readonly QueueSpawner _queueSpawner;
        private readonly ExitStackAnchor _exitStackAnchor;
        private readonly TutorialTargetRegistry _registry;
        private readonly TutorialView _view;
        private readonly Camera _camera;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ConveyorTuning _tuning;

        private int _currentStepIndex;
        private bool _isRunning;
        private float _cellSize;
        private Vector3 _beltOrigin;

        public TutorialController(ConveyorLevelDataSO level, SignalBus signalBus, QueueSpawner queueSpawner,
            ExitStackAnchor exitStackAnchor, TutorialTargetRegistry registry, TutorialView view, Camera camera,
            ConveyorPrefabSettings prefabSettings, ConveyorTuning tuning)
        {
            _level = level;
            _signalBus = signalBus;
            _queueSpawner = queueSpawner;
            _exitStackAnchor = exitStackAnchor;
            _registry = registry;
            _view = view;
            _camera = camera;
            _prefabSettings = prefabSettings;
            _tuning = tuning;
        }

        public void Initialize()
        {
            if (!_level.IsTutorial || _level.TutorialSteps.Count == 0) return;

            if (_view == null)
            {
                Debug.LogError("[TutorialController] Level is marked IsTutorial but ConveyorInstaller's " +
                    "_tutorialView isn't assigned — tutorial won't play this level.");
                return;
            }

            _isRunning = true;
            _cellSize = _prefabSettings.CellWorldSize;
            _beltOrigin = ConveyorWorldUtil.ComputeBeltOrigin(_level, _tuning, _cellSize);

            _view.OnAdvance += OnAdvance;
            _signalBus.Subscribe<FoodEnteredBeltSignal>(OnFoodEnteredBelt);
            _signalBus.Subscribe<FoodDockedSignal>(OnFoodDocked);
            _signalBus.Subscribe<TutorialActionPerformedSignal>(OnActionPerformed);

            TryShowCurrentStep(TutorialTriggerCondition.OnGameplaySceneLoaded);
        }

        public void Dispose()
        {
            if (!_isRunning) return;

            _view.OnAdvance -= OnAdvance;
            _signalBus.TryUnsubscribe<FoodEnteredBeltSignal>(OnFoodEnteredBelt);
            _signalBus.TryUnsubscribe<FoodDockedSignal>(OnFoodDocked);
            _signalBus.TryUnsubscribe<TutorialActionPerformedSignal>(OnActionPerformed);
        }

        private void OnFoodEnteredBelt(FoodEnteredBeltSignal signal) =>
            TryShowCurrentStep(TutorialTriggerCondition.OnFirstFoodOnConveyor);

        private void OnFoodDocked(FoodDockedSignal signal)
        {
            if (_exitStackAnchor.OccupiedCount == _exitStackAnchor.SlotCount - 1)
                TryShowCurrentStep(TutorialTriggerCondition.OnExitStackNearFull);
        }

        // Only fired by ConveyorEntryInput / PowerUpButtonsController once the click's real
        // gameplay effect actually happened, not just once it was allowed.
        private void OnActionPerformed(TutorialActionPerformedSignal signal)
        {
            if (!_isRunning || _currentStepIndex >= _level.TutorialSteps.Count) return;

            var step = _level.TutorialSteps[_currentStepIndex];
            if (step.HoverKind != signal.Kind) return;
            if (step.HoverKind == TutorialHoverTargetKind.QueueLane &&
                (step.QueueLaneIndex != signal.QueueLaneIndex || step.QueueRowIndex != signal.QueueRowIndex)) return;
            if (step.HoverKind == TutorialHoverTargetKind.PowerUpButton && step.PowerUpButton != signal.PowerUpButton) return;

            OnAdvance();
        }

        private void TryShowCurrentStep(TutorialTriggerCondition firedTrigger)
        {
            if (!_isRunning || _currentStepIndex >= _level.TutorialSteps.Count) return;

            var step = _level.TutorialSteps[_currentStepIndex];
            if (step.Trigger != firedTrigger) return;

            bool tapToAdvance = step.HoverKind is TutorialHoverTargetKind.None or TutorialHoverTargetKind.ConveyorCell;
            _view.Show(step.Text, ResolveScreenPos(step), tapToAdvance);
            _signalBus.Fire(new TutorialStepActivatedSignal { Step = step });
        }

        private void OnAdvance()
        {
            _currentStepIndex++;

            if (_currentStepIndex >= _level.TutorialSteps.Count)
            {
                _view.Hide();
                _signalBus.Fire(new TutorialEndedSignal());
                Dispose();
                _isRunning = false;
                return;
            }

            TryShowCurrentStep(_level.TutorialSteps[_currentStepIndex].Trigger);
        }

        private Vector3? ResolveScreenPos(TutorialStepData step) => step.HoverKind switch
        {
            TutorialHoverTargetKind.PowerUpButton => _registry.TryGet(step.PowerUpButton, out var rect)
                ? rect.position
                : null,

            TutorialHoverTargetKind.QueueLane => _queueSpawner.TryGetVisual(step.QueueLaneIndex, step.QueueRowIndex, out var visual)
                ? _camera.WorldToScreenPoint(visual.VisualCenter)
                : null,

            TutorialHoverTargetKind.ConveyorCell => _camera.WorldToScreenPoint(ConveyorWorldUtil.ToWorldPosition(
                step.ConveyorCell, _cellSize, _tuning.BeltFoodHeight, _beltOrigin)),

            _ => null
        };
    }
}
