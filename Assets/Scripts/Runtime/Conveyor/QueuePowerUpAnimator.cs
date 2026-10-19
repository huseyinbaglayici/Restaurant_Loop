using System;
using System.Collections.Generic;
using DG.Tweening;
using RestaurantLoop.Runtime.Audio;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Effects;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Queue-side power-up visuals. Shuffle: the queue crouches, every item jumps into a swirl above the
    // table, spins once and is dealt back to its new slot one by one with a squash on landing.
    // Pick From Bottom: a hop ripples through the whole queue from front to back.
    public class QueuePowerUpAnimator : IDisposable
    {
        private const float CrouchDuration = 0.08f;
        private const float GatherDuration = 0.28f;
        private const float SwirlDuration = 0.45f;
        private const float DealDuration = 0.3f;
        private const float DealStagger = 0.03f;
        private const float SwirlLift = 1.8f;
        private const float SwirlRadius = 2.3f;

        private readonly QueueSpawner _queueSpawner;
        private readonly FxService _fxService;
        private readonly CameraController _cameraController;
        private readonly AudioService _audioService;
        private readonly AudioLibrary _audioLibrary;
        private readonly ConveyorTuning _tuning;

        private readonly List<Sequence> _running = new();

        public QueuePowerUpAnimator(
            QueueSpawner queueSpawner,
            FxService fxService,
            CameraController cameraController,
            AudioService audioService,
            AudioLibrary audioLibrary,
            ConveyorTuning tuning)
        {
            _tuning = tuning;
            _queueSpawner = queueSpawner;
            _fxService = fxService;
            _cameraController = cameraController;
            _audioService = audioService;
            _audioLibrary = audioLibrary;
        }

        public void PlayShuffle()
        {
            _running.RemoveAll(s => !s.IsActive());
            var visuals = new List<FoodItemController>(_queueSpawner.AllVisuals());
            if (visuals.Count == 0) return;

            foreach (var visual in visuals) _queueSpawner.ClaimForAnimation(visual);
            var center = VisibleQueueCenter();
            var cameraTransform = _cameraController.Camera.transform;
            var screenUp = cameraTransform.up;
            var screenRight = cameraTransform.right;
            var swirlCenter = center + screenUp * SwirlLift;

            _queueSpawner.ShuffleLanes();

            _fxService.Play(FxType.ShuffleSwirl, swirlCenter);
            _cameraController.Shake(0.12f, 0.35f);
            if (_audioLibrary.ShuffleSfx != null) _audioService.PlaySfx(_audioLibrary.ShuffleSfx);

            for (int i = 0; i < visuals.Count; i++)
                _running.Add(Animate(visuals[i], i, visuals.Count, swirlCenter, screenUp, screenRight));
        }

        private Sequence Animate(FoodItemController visual, int index, int count, Vector3 swirlCenter, Vector3 screenUp, Vector3 screenRight)
        {
            var itemTransform = visual.transform;
            var baseScale = itemTransform.localScale;
            var baseRotation = itemTransform.rotation;
            float startAngle = index * Mathf.PI * 2f / count;
            float angle = startAngle;

            _queueSpawner.TryFindSlot(visual, out int lane, out int row);
            var slot = lane >= 0 ? _queueSpawner.GetSlotPosition(lane, row) : itemTransform.position;

            // Orbit in the screen plane (squashed vertically) so the swirl never moves toward the camera.
            Vector3 OrbitPoint(float a, float radius) => swirlCenter + (screenRight * Mathf.Cos(a) + screenUp * (Mathf.Sin(a) * 0.65f)) * radius;

            var sequence = DOTween.Sequence().SetTarget(itemTransform);
            sequence.Append(itemTransform.DOScale(new Vector3(baseScale.x * 1.15f, baseScale.y * 0.7f, baseScale.z * 1.15f), CrouchDuration));
            sequence.Append(ArcTween.To(itemTransform, OrbitPoint(startAngle, SwirlRadius), screenUp, 0.6f, GatherDuration).SetEase(Ease.OutQuad));
            sequence.Join(itemTransform.DOScale(baseScale * 0.8f, GatherDuration).SetEase(Ease.OutBack));
            sequence.Append(DOTween.To(() => angle, a =>
            {
                angle = a;
                float t = Mathf.InverseLerp(startAngle, startAngle + Mathf.PI * 2f, a);
                itemTransform.position = OrbitPoint(a, Mathf.Lerp(SwirlRadius, SwirlRadius * 0.6f, Mathf.Sin(t * Mathf.PI)));
            }, startAngle + Mathf.PI * 2f, SwirlDuration).SetEase(Ease.InOutSine));
            sequence.Join(itemTransform.DOPunchScale(baseScale * 0.12f, SwirlDuration, 4, 0.5f));
            sequence.AppendInterval(index * DealStagger);
            sequence.Append(ArcTween.To(itemTransform, slot, screenUp, 0.9f, DealDuration).SetEase(Ease.InQuad));
            sequence.Join(itemTransform.DOScale(baseScale, DealDuration));
            sequence.AppendCallback(() => _fxService.Play(FxType.ItemLand, slot, 0.6f));
            sequence.Append(itemTransform.DOScale(new Vector3(baseScale.x * 1.2f, baseScale.y * 0.78f, baseScale.z * 1.2f), 0.06f));
            sequence.Append(itemTransform.DOScale(baseScale, 0.18f).SetEase(Ease.OutBack));
            sequence.OnKill(() =>
            {
                if (itemTransform == null) return;
                itemTransform.position = slot;
                itemTransform.rotation = baseRotation;
                itemTransform.localScale = baseScale;
                _queueSpawner.ReleaseFromAnimation(visual, slot);
            });
            return sequence;
        }

        public void PlayPickFromBottomRipple()
        {
            int count = 0;
            for (int lane = 0; lane < _queueSpawner.LaneCount; lane++)
            {
                for (int row = 0; _queueSpawner.TryGetVisual(lane, row, out var visual); row++)
                {
                    var itemTransform = visual.transform;
                    var baseScale = itemTransform.localScale;
                    count++;
                    itemTransform.DOPunchPosition(_cameraController.Camera.transform.up * 0.45f, 0.35f, 1, 0f).SetDelay(row * 0.05f + lane * 0.02f).SetTarget(itemTransform);
                    itemTransform.DOPunchScale(baseScale * 0.18f, 0.35f, 5, 0.6f).SetDelay(row * 0.05f + lane * 0.02f).SetTarget(itemTransform)
                        .OnKill(() => { if (itemTransform != null) itemTransform.localScale = baseScale; });
                }
            }

            if (count == 0) return;
            _fxService.Play(FxType.PowerUpBurst, VisibleQueueCenter() + _cameraController.Camera.transform.up * 0.5f);
            if (_audioLibrary.PowerUpSfx != null) _audioService.PlaySfx(_audioLibrary.PowerUpSfx);
        }

        // Middle of the rows the player can actually see; deep rows tuck under the bottom bar.
        private Vector3 VisibleQueueCenter()
        {
            int rows = Mathf.Max(1, _tuning.CameraVisibleQueueRows);
            var sum = Vector3.zero;
            int count = 0;
            for (int lane = 0; lane < _queueSpawner.LaneCount; lane++)
                for (int row = 0; row < rows; row++)
                {
                    sum += _queueSpawner.GetSlotPosition(lane, row);
                    count++;
                }

            return count > 0 ? sum / count : Vector3.zero;
        }

        public void Dispose()
        {
            foreach (var sequence in _running) sequence.Kill();
            _running.Clear();
        }
    }
}
