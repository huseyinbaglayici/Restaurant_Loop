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
    public class TutorialController : IInitializable, ITickable, IDisposable
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

        private readonly CustomerConsumeService _consumeService;
        private readonly PowerUpSettings _powerUpSettings;

        private int _currentStepIndex;
        private bool _isRunning;
        private bool _isStepActive;
        private float _cellSize;
        private Vector3 _beltOrigin;

        public TutorialController(ConveyorLevelDataSO level, SignalBus signalBus, QueueSpawner queueSpawner,
            ExitStackAnchor exitStackAnchor, TutorialTargetRegistry registry, TutorialView view, Camera camera,
            ConveyorPrefabSettings prefabSettings, ConveyorTuning tuning,
            [Inject(Optional = true)] CustomerConsumeService consumeService = null,
            [Inject(Optional = true)] PowerUpSettings powerUpSettings = null)
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
            _consumeService = consumeService;
            _powerUpSettings = powerUpSettings;
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

            if (_consumeService != null)
            {
                _consumeService.OnLevelEnded += HandleLevelEnded;
            }

            TryShowCurrentStep(TutorialTriggerCondition.OnGameplaySceneLoaded);
        }

        public void Tick()
        {
            if (!_isRunning || !_isStepActive || _currentStepIndex >= _level.TutorialSteps.Count) return;

            // Kamera veya UI oturdukça parmak pozisyonunu sürekli güncel tut
            var step = _level.TutorialSteps[_currentStepIndex];
            var screenPos = ResolveScreenPos(step);
            if (screenPos.HasValue && _view != null)
            {
                _view.UpdateFingerPosition(screenPos.Value);
            }
        }

        public void Dispose()
        {
            if (!_isRunning) return;

            _view.OnAdvance -= OnAdvance;
            _signalBus.TryUnsubscribe<FoodEnteredBeltSignal>(OnFoodEnteredBelt);
            _signalBus.TryUnsubscribe<FoodDockedSignal>(OnFoodDocked);
            _signalBus.TryUnsubscribe<TutorialActionPerformedSignal>(OnActionPerformed);

            if (_consumeService != null)
            {
                _consumeService.OnLevelEnded -= HandleLevelEnded;
            }
        }

        private void HandleLevelEnded(bool won)
        {
            if (!won)
            {
                TryShowCurrentStep(TutorialTriggerCondition.OnFirstLevelFail);
            }
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
            if (!_isRunning || !_isStepActive || _currentStepIndex >= _level.TutorialSteps.Count) return;

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

            _isStepActive = true;
            bool tapToAdvance = step.HoverKind is TutorialHoverTargetKind.None or TutorialHoverTargetKind.ConveyorCell;

            var settings = _powerUpSettings ?? Resources.Load<PowerUpSettings>("PowerUpSettings");
            Vector2? customPanelPos = null;
            Vector2? fingerOffset = null;
            Vector2? blockerOffset = null;

            bool isPowerUpRelated = step.HoverKind != TutorialHoverTargetKind.QueueLane &&
                                   step.HoverKind != TutorialHoverTargetKind.ConveyorCell &&
                                   (step.HoverKind == TutorialHoverTargetKind.PowerUpButton ||
                                    step.Trigger == TutorialTriggerCondition.OnFirstLevelFail ||
                                    (_currentStepIndex + 1 < _level.TutorialSteps.Count && _level.TutorialSteps[_currentStepIndex + 1].HoverKind == TutorialHoverTargetKind.PowerUpButton));

            if (isPowerUpRelated && settings != null)
            {
                PowerUpButtonId powerUpId = step.HoverKind == TutorialHoverTargetKind.PowerUpButton 
                    ? step.PowerUpButton 
                    : (_currentStepIndex + 1 < _level.TutorialSteps.Count && _level.TutorialSteps[_currentStepIndex + 1].HoverKind == TutorialHoverTargetKind.PowerUpButton 
                        ? _level.TutorialSteps[_currentStepIndex + 1].PowerUpButton 
                        : step.PowerUpButton);

                var def = settings.GetDefinition(powerUpId);
                if (def != null && def.CustomTutorialPanelPos != Vector2.zero)
                {
                    customPanelPos = def.CustomTutorialPanelPos;
                }
                else
                {
                    customPanelPos = settings.PowerUpTutorialPanelPos;
                }

                if (def != null && def.CustomFingerOffset != Vector2.zero)
                {
                    fingerOffset = def.CustomFingerOffset;
                }
                else if (settings.PowerUpFingerOffset != Vector2.zero)
                {
                    fingerOffset = settings.PowerUpFingerOffset;
                }

                if (def != null && def.CustomBlockerOffset != Vector2.zero)
                {
                    blockerOffset = def.CustomBlockerOffset;
                }
                else if (settings.PowerUpBlockerOffset != Vector2.zero)
                {
                    blockerOffset = settings.PowerUpBlockerOffset;
                }
            }

            _view.Show(step.Text, ResolveScreenPos(step), tapToAdvance, customPanelPos, fingerOffset, blockerOffset, isPowerUpRelated);
            _signalBus.Fire(new TutorialStepActivatedSignal { Step = step });
        }

        private void OnAdvance()
        {
            _isStepActive = false;
            _currentStepIndex++;

            if (_currentStepIndex >= _level.TutorialSteps.Count)
            {
                _view.Hide();
                _signalBus.Fire(new TutorialEndedSignal());
                Dispose();
                _isRunning = false;
                return;
            }

            var nextStep = _level.TutorialSteps[_currentStepIndex];
            // Eğer sıradaki adım doğrudan devam eden bir adımsa (aynı tetikleyici veya None) kesintisiz göster
            TryShowCurrentStep(nextStep.Trigger);
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
