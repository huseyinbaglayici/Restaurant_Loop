using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Draws a solid 3D rectangular prism between every pair of items sharing a nonzero LinkId,
    // wherever they currently are (queue lane, conveyor belt, or exit stack). The prism is sharply
    // split half-and-half with the distinct colors of each linked food type (configured in FoodMeshSettings).
    public class QueueLinkVisualizer : IInitializable, ITickable, IDisposable
    {
        private class LinkPrism
        {
            public GameObject Root;
            public Transform HalfATransform;
            public MeshRenderer HalfARenderer;
            public Transform HalfBTransform;
            public MeshRenderer HalfBRenderer;
            public MaterialPropertyBlock BlockA = new();
            public MaterialPropertyBlock BlockB = new();
        }

        private readonly QueueSpawner _queueSpawner;
        private readonly QueueAnchor _queueAnchor;
        private readonly ConveyorManager _conveyorManager;
        private readonly ConveyorTuning _tuning;
        private readonly ExitStackAnchor _exitStackAnchor;
        private readonly FoodMeshSettings _foodMeshSettings;

        private readonly Dictionary<int, LinkPrism> _prisms = new();
        private readonly HashSet<int> _usedThisTick = new();
        private readonly Dictionary<int, Vector3> _firstSeen = new();
        private readonly Dictionary<int, FoodType> _firstSeenType = new();

        private Transform _container;
        private Mesh _cubeMesh;
        private Material _prismMaterial;

        public QueueLinkVisualizer(
            QueueSpawner queueSpawner,
            QueueAnchor queueAnchor,
            ConveyorManager conveyorManager,
            ConveyorTuning tuning,
            [Inject(Optional = true)] ExitStackAnchor exitStackAnchor,
            FoodMeshSettings foodMeshSettings)
        {
            _queueSpawner = queueSpawner;
            _queueAnchor = queueAnchor;
            _conveyorManager = conveyorManager;
            _tuning = tuning;
            _exitStackAnchor = exitStackAnchor;
            _foodMeshSettings = foodMeshSettings;
        }

        public void Initialize()
        {
            _container = new GameObject("QueueLinkPrisms").transform;

            var tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cubeMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(tempCube);

            var loadedMat = Resources.Load<Material>("Materials/M_LinkPrism");
            if (loadedMat != null)
            {
                _prismMaterial = new Material(loadedMat);
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Sprites/Default")
                             ?? Shader.Find("Unlit/Color");
                _prismMaterial = new Material(shader);
            }
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

            if (_exitStackAnchor != null)
            {
                for (int i = 0; i < _exitStackAnchor.SlotCount; i++)
                {
                    if (_exitStackAnchor.TryGetOccupant(i, out var visual) && visual != null)
                        ProcessVisual(visual);
                }
            }

            foreach (var kvp in _prisms)
                kvp.Value.Root.SetActive(_usedThisTick.Contains(kvp.Key));
        }

        private void ProcessVisual(FoodItemController visual)
        {
            int linkId = visual.Stack.LinkId;
            if (linkId == 0) return;

            Vector3 basePos = visual.transform.position;
            if (_firstSeen.TryGetValue(linkId, out var start))
                DrawLink(linkId, start, basePos, _firstSeenType[linkId], visual.Stack.Type);
            else
            {
                _firstSeen[linkId] = basePos;
                _firstSeenType[linkId] = visual.Stack.Type;
            }
        }

        private LinkPrism CreatePrism(int linkId)
        {
            var root = new GameObject($"LinkPrism_{linkId}");
            root.transform.SetParent(_container, false);

            var halfA = new GameObject("HalfA");
            halfA.transform.SetParent(root.transform, false);
            var mfA = halfA.AddComponent<MeshFilter>();
            mfA.sharedMesh = _cubeMesh;
            var mrA = halfA.AddComponent<MeshRenderer>();
            mrA.sharedMaterial = _prismMaterial;
            mrA.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrA.receiveShadows = false;

            var halfB = new GameObject("HalfB");
            halfB.transform.SetParent(root.transform, false);
            var mfB = halfB.AddComponent<MeshFilter>();
            mfB.sharedMesh = _cubeMesh;
            var mrB = halfB.AddComponent<MeshRenderer>();
            mrB.sharedMaterial = _prismMaterial;
            mrB.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrB.receiveShadows = false;

            return new LinkPrism
            {
                Root = root,
                HalfATransform = halfA.transform,
                HalfARenderer = mrA,
                HalfBTransform = halfB.transform,
                HalfBRenderer = mrB
            };
        }

        private void DrawLink(int linkId, Vector3 start, Vector3 end, FoodType startType, FoodType endType)
        {
            _usedThisTick.Add(linkId);

            if (!_prisms.TryGetValue(linkId, out var prism))
            {
                prism = CreatePrism(linkId);
                _prisms[linkId] = prism;
            }

            Vector3 offset = Vector3.up * (_tuning != null ? _tuning.LinkPrismYOffset : 0f);
            Vector3 pStart = start + offset;
            Vector3 pEnd = end + offset;
            Vector3 delta = pEnd - pStart;
            float length = delta.magnitude;

            if (length < 0.0001f)
            {
                prism.Root.SetActive(false);
                return;
            }

            Vector3 dir = delta / length;
            Quaternion rot = (dir != Vector3.up && dir != Vector3.down)
                ? Quaternion.LookRotation(dir, Vector3.up)
                : Quaternion.LookRotation(dir, Vector3.forward);

            float width = _tuning != null && _tuning.LinkPrismWidth > 0f ? _tuning.LinkPrismWidth : 0.22f;
            float height = _tuning != null && _tuning.LinkPrismHeight > 0f ? _tuning.LinkPrismHeight : 0.16f;
            float halfLength = length * 0.5f;

            prism.HalfATransform.position = pStart + dir * (halfLength * 0.5f);
            prism.HalfATransform.rotation = rot;
            prism.HalfATransform.localScale = new Vector3(width, height, halfLength);

            prism.HalfBTransform.position = pStart + dir * (halfLength * 1.5f);
            prism.HalfBTransform.rotation = rot;
            prism.HalfBTransform.localScale = new Vector3(width, height, halfLength);

            Color startColor = _foodMeshSettings != null ? _foodMeshSettings.GetColor(startType) : FoodTypeUtil.ToColor(startType);
            Color endColor = _foodMeshSettings != null ? _foodMeshSettings.GetColor(endType) : FoodTypeUtil.ToColor(endType);

            prism.BlockA.SetColor("_BaseColor", startColor);
            prism.BlockA.SetColor("_Color", startColor);
            prism.HalfARenderer.SetPropertyBlock(prism.BlockA);

            prism.BlockB.SetColor("_BaseColor", endColor);
            prism.BlockB.SetColor("_Color", endColor);
            prism.HalfBRenderer.SetPropertyBlock(prism.BlockB);

            prism.Root.SetActive(true);
        }

        public void Dispose()
        {
            if (_container != null) UnityEngine.Object.Destroy(_container.gameObject);
            if (_prismMaterial != null) UnityEngine.Object.Destroy(_prismMaterial);
        }
    }
}
