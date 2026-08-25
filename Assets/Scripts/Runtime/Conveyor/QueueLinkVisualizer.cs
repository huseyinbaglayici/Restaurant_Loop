using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Draws a smooth sagging rope between every pair of items sharing a nonzero LinkId, wherever they
    // currently are (queue lane or conveyor belt) — the in-game equivalent of QueueGridCanvas's
    // editor-time link lines. A rope only renders when BOTH halves are found this tick, so it stays
    // intact as a linked pair rides the belt together and vanishes the instant one half is fully
    // consumed (removed from the belt) while the other isn't.
    public class QueueLinkVisualizer : IInitializable, ITickable, IDisposable
    {
        private readonly QueueSpawner _queueSpawner;
        private readonly QueueAnchor _queueAnchor;
        private readonly ConveyorManager _conveyorManager;
        private readonly ConveyorTuning _tuning;

        private readonly Dictionary<int, LineRenderer> _lines = new();
        private readonly HashSet<int> _usedThisTick = new();
        private readonly Dictionary<int, Vector3> _firstSeen = new();
        private readonly Dictionary<int, FoodType> _firstSeenType = new();

        private Transform _container;
        private Material _lineMaterial;

        public QueueLinkVisualizer(
            QueueSpawner queueSpawner,
            QueueAnchor queueAnchor,
            ConveyorManager conveyorManager,
            ConveyorTuning tuning)
        {
            _queueSpawner = queueSpawner;
            _queueAnchor = queueAnchor;
            _conveyorManager = conveyorManager;
            _tuning = tuning;
        }

        public void Initialize()
        {
            _container = new GameObject("QueueLinkLines").transform;
            // Sprites/Default multiplies its output by per-vertex color, which is what LineRenderer.colorGradient
            // writes — URP/Unlit ignores vertex color entirely (only reads _BaseColor), which is why the rope
            // rendered flat white despite the gradient being set.
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            _lineMaterial = new Material(shader);
        }

        public void Tick()
        {
            _usedThisTick.Clear();
            _firstSeen.Clear();
            _firstSeenType.Clear();

            for (int lane = 0; lane < _queueAnchor.LanePositions.Length; lane++)
            {
                int count = _queueSpawner.GetLaneCount(lane);
                for (int row = 0; row < count; row++)
                {
                    if (_queueSpawner.TryGetVisual(lane, row, out var visual))
                        ProcessVisual(visual);
                }
            }

            for (int i = 0; i < _conveyorManager.ItemCount; i++)
            {
                var visual = _conveyorManager.GetVisualAt(i);
                if (visual != null) ProcessVisual(visual);
            }

            foreach (var kvp in _lines)
                kvp.Value.gameObject.SetActive(_usedThisTick.Contains(kvp.Key));
        }

        private void ProcessVisual(FoodItemController visual)
        {
            int linkId = visual.Stack.LinkId;
            if (linkId == 0) return;

            if (_firstSeen.TryGetValue(linkId, out var start))
                DrawLink(linkId, start, visual.VisualCenter, _firstSeenType[linkId], visual.Stack.Type);
            else
            {
                _firstSeen[linkId] = visual.VisualCenter;
                _firstSeenType[linkId] = visual.Stack.Type;
            }
        }

        private void DrawLink(int linkId, Vector3 start, Vector3 end, FoodType startType, FoodType endType)
        {
            _usedThisTick.Add(linkId);

            if (!_lines.TryGetValue(linkId, out var line))
            {
                var go = new GameObject($"Link_{linkId}");
                go.transform.SetParent(_container, false);

                line = go.AddComponent<LineRenderer>();
                line.material = _lineMaterial;
                line.useWorldSpace = true;
                line.numCapVertices = 4;
                line.widthMultiplier = _tuning.LinkLineWidth;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;

                _lines[linkId] = line;
            }

            // Rope reads as belonging to both linked items: first half tinted the start item's color,
            // second half the end item's, split sharply at the midpoint instead of smearing across it.
            var startColor = FoodTypeUtil.ToColor(startType);
            var endColor = FoodTypeUtil.ToColor(endType);
            const float splitEpsilon = 0.001f;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(startColor, 0f),
                    new GradientColorKey(startColor, 0.5f - splitEpsilon),
                    new GradientColorKey(endColor, 0.5f + splitEpsilon),
                    new GradientColorKey(endColor, 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            line.colorGradient = gradient;

            line.gameObject.SetActive(true);

            int segments = Mathf.Max(2, _tuning.LinkLineSegments);
            line.positionCount = segments + 1;

            Vector3 mid = (start + end) * 0.5f;
            mid.y -= _tuning.LinkLineSag;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                line.SetPosition(i, QuadraticBezier(start, mid, end, t));
            }
        }

        private static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        public void Dispose()
        {
            if (_container != null) UnityEngine.Object.Destroy(_container.gameObject);
            if (_lineMaterial != null) UnityEngine.Object.Destroy(_lineMaterial);
        }
    }
}
