using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Draws a smooth sagging rope between every pair of queue items sharing a nonzero LinkId —
    // the in-game equivalent of QueueGridCanvas's editor-time link lines, so a linked pair reads
    // as linked once it's actually sitting in the gameplay queue.
    public class QueueLinkVisualizer : IInitializable, ITickable, IDisposable
    {
        private readonly QueueSpawner _queueSpawner;
        private readonly QueueAnchor _queueAnchor;
        private readonly ConveyorTuning _tuning;

        private readonly Dictionary<int, LineRenderer> _lines = new();
        private readonly HashSet<int> _usedThisTick = new();
        private readonly Dictionary<int, Vector3> _firstSeen = new();

        private Transform _container;
        private Material _lineMaterial;

        public QueueLinkVisualizer(QueueSpawner queueSpawner, QueueAnchor queueAnchor, ConveyorTuning tuning)
        {
            _queueSpawner = queueSpawner;
            _queueAnchor = queueAnchor;
            _tuning = tuning;
        }

        public void Initialize()
        {
            _container = new GameObject("QueueLinkLines").transform;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _lineMaterial = new Material(shader);
        }

        public void Tick()
        {
            _usedThisTick.Clear();
            _firstSeen.Clear();

            for (int lane = 0; lane < _queueAnchor.LanePositions.Length; lane++)
            {
                int count = _queueSpawner.GetLaneCount(lane);
                for (int row = 0; row < count; row++)
                {
                    if (!_queueSpawner.TryGetVisual(lane, row, out var visual)) continue;

                    int linkId = visual.Stack.LinkId;
                    if (linkId == 0) continue;

                    if (_firstSeen.TryGetValue(linkId, out var start))
                        DrawLink(linkId, start, visual.VisualCenter);
                    else
                        _firstSeen[linkId] = visual.VisualCenter;
                }
            }

            foreach (var kvp in _lines)
                kvp.Value.gameObject.SetActive(_usedThisTick.Contains(kvp.Key));
        }

        private void DrawLink(int linkId, Vector3 start, Vector3 end)
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

                var color = LinkColorFor(linkId);
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                line.colorGradient = gradient;

                _lines[linkId] = line;
            }

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

        // Same golden-ratio hue spread as QueueGridCanvas.LinkColorFor, so a link reads the same
        // color in the level editor and in the gameplay scene.
        private static Color LinkColorFor(int linkId)
        {
            float hue = (linkId * 0.618034f) % 1f;
            return Color.HSVToRGB(hue, 0.65f, 1f);
        }

        public void Dispose()
        {
            if (_container != null) UnityEngine.Object.Destroy(_container.gameObject);
            if (_lineMaterial != null) UnityEngine.Object.Destroy(_lineMaterial);
        }
    }
}
