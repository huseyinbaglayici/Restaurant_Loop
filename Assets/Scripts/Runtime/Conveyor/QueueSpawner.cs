using System;
using System.Collections.Generic;
using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
 
    public class QueueSpawner : IInitializable, ITickable, IDisposable
    {
        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorTuning _tuning;
        private readonly QueueAnchor _queueAnchor;
        private readonly FoodItemController.Pool _foodPool;
        private readonly CameraController _cameraController;
        private readonly SignalBus _signalBus;

        // Per lane, front-to-back (index 0 = front).
        private readonly List<List<FoodItemController>> _lanes = new();

        // Slot each visual is currently moving to; a changed slot starts a short hop instead of a slide.
        private readonly Dictionary<FoodItemController, Vector3> _slotTargets = new();
        // Visuals owned by a power-up animation (shuffle); the layout tick leaves them alone.
        private readonly HashSet<FoodItemController> _externallyAnimated = new();

        private const float HopHeight = 0.35f;
        private const float MinHopDuration = 0.14f;
        private const float MaxHopDuration = 0.32f;
        private const float RowStagger = 0.035f;

        // Pick From Bottom power-up: while true, any row in a lane is pickable, not just the front. One-shot.
        public bool PickFromBottomEnabled { get; set; }

        // True while a shuffle animation is moving the queue; picks wait until it settles.
        public bool IsAnimating => !_introPlayed || _externallyAnimated.Count > 0;

        public int LaneCount => _lanes.Count;

        public QueueSpawner(ConveyorLevelDataSO level, ConveyorTuning tuning, QueueAnchor queueAnchor, FoodItemController.Pool foodPool, CameraController cameraController, SignalBus signalBus)
        {
            _cameraController = cameraController;
            _signalBus = signalBus;
            _level = level;
            _tuning = tuning;
            _queueAnchor = queueAnchor;
            _foodPool = foodPool;
        }

        public void Initialize()
        {
            int laneCount = Mathf.Min(_level.Queues.Count, _queueAnchor.LanePositions.Length);
            for (int lane = 0; lane < laneCount; lane++)
            {
                var laneVisuals = new List<FoodItemController>();
                var lanePosition = _queueAnchor.LanePositions[lane];
                var entries = _level.Queues[lane].Entries;

                for (int row = 0; row < entries.Count; row++)
                {
                    var position = lanePosition + new Vector3(0f, 0f, -row * _tuning.QueueRowSpacing);
                    var visual = _foodPool.Spawn(position, entries[row], FoodVisualState.Opened);
                    visual.SetLocation(FoodLocation.Queue);
                    laneVisuals.Add(visual);
                    _slotTargets[visual] = position;
                    _introScales[visual] = visual.transform.localScale;
                    visual.transform.localScale = Vector3.zero;
                }

                _lanes.Add(laneVisuals);
            }

            _signalBus.Subscribe<LoadingHiddenSignal>(PlayIntro);
        }

        private bool _introPlayed;
        private readonly Dictionary<FoodItemController, Vector3> _introScales = new();
        private float _introFallbackTimer;

        // Level intro: the queue pops onto the table front-to-back once the loading screen lifts
        // (with a timeout, so playing GameplayScene directly in the editor still shows the queue).
        private void PlayIntro()
        {
            if (_introPlayed) return;
            _introPlayed = true;

            for (int lane = 0; lane < _lanes.Count; lane++)
                for (int row = 0; row < _lanes[lane].Count; row++)
                {
                    var visual = _lanes[lane][row];
                    if (_introScales.TryGetValue(visual, out var rest))
                        PopIn(visual.transform, rest, IntroDelay + (row * _lanes.Count + lane) * 0.018f);
                }

            _introScales.Clear();
        }

        private const float IntroDelay = 0.1f;
        private const float IntroFallbackSeconds = 2f;

        private static void PopIn(Transform target, Vector3 rest, float delay)
        {
            target.localScale = Vector3.zero;
            target.DOScale(rest, 0.32f).SetDelay(delay).SetEase(Ease.OutBack, 2f).SetTarget(target)
                .OnKill(() => { if (target != null) target.localScale = rest; });
        }

        public Vector3 GetSlotPosition(int lane, int row) =>
            _queueAnchor.LanePositions[lane] + new Vector3(0f, 0f, -row * _tuning.QueueRowSpacing);

        // When a slot frees up the rows behind it hop forward one after another (front first) and
        // land with a small squash, instead of sliding at a constant speed.
        public void Tick()
        {
            if (!_introPlayed && (_introFallbackTimer += Time.unscaledDeltaTime) > IntroFallbackSeconds) PlayIntro();

            for (int lane = 0; lane < _lanes.Count; lane++)
            {
                var laneVisuals = _lanes[lane];
                for (int row = 0; row < laneVisuals.Count; row++)
                {
                    var visual = laneVisuals[row];
                    if (_externallyAnimated.Contains(visual)) continue;

                    var target = GetSlotPosition(lane, row);
                    if (_slotTargets.TryGetValue(visual, out var current) && (current - target).sqrMagnitude < 0.0001f) continue;

                    _slotTargets[visual] = target;
                    HopTo(visual, target, row * RowStagger);
                }
            }
        }

        private void HopTo(FoodItemController visual, Vector3 target, float delay)
        {
            var visualTransform = visual.transform;
            visualTransform.DOKill(true);

            float distance = Vector3.Distance(visualTransform.position, target);
            float speed = Mathf.Max(0.01f, _tuning.QueueShiftSpeed);
            float duration = Mathf.Clamp(distance / speed, MinHopDuration, MaxHopDuration);
            var baseScale = visualTransform.localScale;

            DOTween.Sequence()
                .SetTarget(visualTransform)
                .AppendInterval(delay)
                .Append(ArcTween.To(visualTransform, target, _cameraController.Camera.transform.up, HopHeight, duration).SetEase(Ease.OutQuad))
                .Append(visualTransform.DOScale(new Vector3(baseScale.x * 1.12f, baseScale.y * 0.85f, baseScale.z * 1.12f), 0.06f))
                .Append(visualTransform.DOScale(baseScale, 0.14f).SetEase(Ease.OutBack));
        }

        // Shuffle hands the visuals to its own animation; Release gives them back once they've landed.
        public void ClaimForAnimation(FoodItemController visual)
        {
            visual.transform.DOKill(true);
            _externallyAnimated.Add(visual);
        }

        public void ReleaseFromAnimation(FoodItemController visual, Vector3 landedSlot)
        {
            _externallyAnimated.Remove(visual);
            _slotTargets[visual] = landedSlot;
        }

        public bool TryFindSlot(FoodItemController visual, out int lane, out int row)
        {
            for (lane = 0; lane < _lanes.Count; lane++)
            {
                row = _lanes[lane].IndexOf(visual);
                if (row >= 0) return true;
            }

            lane = row = -1;
            return false;
        }

        public IEnumerable<FoodItemController> AllVisuals()
        {
            foreach (var lane in _lanes)
                foreach (var visual in lane)
                    yield return visual;
        }

        public int GetLaneCount(int lane) => (lane >= 0 && lane < _lanes.Count) ? _lanes[lane].Count : 0;

        public int TotalCount
        {
            get
            {
                int total = 0;
                foreach (var lane in _lanes) total += lane.Count;
                return total;
            }
        }

        public bool TryGetVisual(int lane, int row, out FoodItemController visual)
        {
            if (lane < 0 || lane >= _lanes.Count || row < 0 || row >= _lanes[lane].Count)
            {
                visual = null;
                return false;
            }

            visual = _lanes[lane][row];
            return true;
        }

        public void PopAt(int lane, int row)
        {
            if (!TryDetachAt(lane, row, out var visual)) return;
            _foodPool.Despawn(visual);
        }

        // Removes the entry from its lane but keeps the visual alive, so the caller can keep flying the
        // very same object onto the belt instead of despawn + respawn (which would read as a teleport).
        public bool TryDetachAt(int lane, int row, out FoodItemController visual)
        {
            visual = null;
            if (lane < 0 || lane >= _lanes.Count) return false;

            var laneVisuals = _lanes[lane];
            if (row < 0 || row >= laneVisuals.Count) return false;

            visual = laneVisuals[row];
            laneVisuals.RemoveAt(row);
            visual.transform.DOKill(true);
            _slotTargets.Remove(visual);
            _externallyAnimated.Remove(visual);
            return true;
        }

        // Shuffle power-up: flattens every lane, shuffles, then redeals each item to the shortest lane.
        // Linked partners move as one unit and stay back-to-back in the same lane, so a pair is always
        // pickable once it reaches the front. Tick() animates each visual to its new slot next frame.
        public void ShuffleLanes()
        {
            if (_lanes.Count == 0) return;

            var units = new List<List<FoodItemController>>();
            var unitByLinkId = new Dictionary<int, List<FoodItemController>>();
            foreach (var lane in _lanes)
            {
                foreach (var visual in lane)
                {
                    int linkId = visual.Stack.LinkId;
                    if (linkId != 0 && unitByLinkId.TryGetValue(linkId, out var linkedUnit))
                    {
                        linkedUnit.Add(visual);
                        continue;
                    }

                    var unit = new List<FoodItemController> { visual };
                    units.Add(unit);
                    if (linkId != 0) unitByLinkId[linkId] = unit;
                }

                lane.Clear();
            }

            for (int i = 0; i < units.Count; i++)
            {
                int randomIndex = UnityEngine.Random.Range(i, units.Count);
                (units[i], units[randomIndex]) = (units[randomIndex], units[i]);
            }

            foreach (var unit in units)
            {
                int target = 0;
                for (int lane = 1; lane < _lanes.Count; lane++)
                    if (_lanes[lane].Count < _lanes[target].Count) target = lane;

                _lanes[target].AddRange(unit);
            }
        }

        public void Dispose()
        {
            _signalBus.TryUnsubscribe<LoadingHiddenSignal>(PlayIntro);
            foreach (var lane in _lanes)
                foreach (var visual in lane)
                {
                    visual.transform.DOKill(true);
                    _foodPool.Despawn(visual);
                }
            _lanes.Clear();
            _slotTargets.Clear();
            _externallyAnimated.Clear();
        }
    }
}
