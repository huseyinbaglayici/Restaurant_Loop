using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Tutorial
{
    // Where the bubble, finger and advance blocker go for one tutorial step, read from PowerUpSettings.
    // Null means "keep the view's default".
    public struct TutorialStepLayout
    {
        public Vector2? PanelPos;
        public Vector2? FingerOffset;
        public Vector2? BlockerOffset;
        public bool IsPowerUpStep;

        public static TutorialStepLayout Resolve(IReadOnlyList<TutorialStepData> steps, int index, PowerUpSettings settings)
        {
            var step = steps[index];
            var next = index + 1 < steps.Count ? steps[index + 1] : null;
            bool nextIsPowerUp = next != null && next.HoverKind == TutorialHoverTargetKind.PowerUpButton;

            // The power-up layout also covers the fail step and the intro step right before a power-up step.
            var layout = new TutorialStepLayout
            {
                IsPowerUpStep = step.HoverKind != TutorialHoverTargetKind.QueueLane &&
                                step.HoverKind != TutorialHoverTargetKind.ConveyorCell &&
                                (step.HoverKind == TutorialHoverTargetKind.PowerUpButton ||
                                 step.Trigger == TutorialTriggerCondition.OnFirstLevelFail ||
                                 nextIsPowerUp)
            };

            if (settings == null) return layout;

            if (layout.IsPowerUpStep)
            {
                PowerUpButtonId powerUpId = step.HoverKind == TutorialHoverTargetKind.PowerUpButton
                    ? step.PowerUpButton
                    : nextIsPowerUp ? next.PowerUpButton : step.PowerUpButton;

                var def = settings.GetDefinition(powerUpId);
                layout.PanelPos = def != null && def.CustomTutorialPanelPos != Vector2.zero
                    ? def.CustomTutorialPanelPos
                    : settings.PowerUpTutorialPanelPos;
                layout.FingerOffset = FirstNonZero(def?.CustomFingerOffset, settings.PowerUpFingerOffset);
                layout.BlockerOffset = FirstNonZero(def?.CustomBlockerOffset, settings.PowerUpBlockerOffset);
            }
            else if (step.HoverKind == TutorialHoverTargetKind.QueueLane)
            {
                layout.FingerOffset = FirstNonZero(settings.QueueFingerOffset, settings.NormalTutorialFingerOffset);
            }
            else
            {
                layout.FingerOffset = FirstNonZero(null, settings.NormalTutorialFingerOffset);
            }

            return layout;
        }

        // Vector2.zero means "not set" in PowerUpSettings.
        private static Vector2? FirstNonZero(Vector2? preferred, Vector2 fallback)
        {
            if (preferred.HasValue && preferred.Value != Vector2.zero) return preferred;
            if (fallback != Vector2.zero) return fallback;
            return null;
        }
    }
}
