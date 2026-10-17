using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Animates a corner belt by rotating its UVs around the corner pivot (UVs built by CornerUvGenerator)
    // on a per-instance copy of the mesh.
    public class ConveyorCornerVisual : MonoBehaviour, IConveyorPieceVisual
    {
        public enum ProjectionPlane
        {
            AutoDetect,
            XZ_TopDown,
            XY_Vertical
        }

        public enum CornerAnchor
        {
            BottomLeft,
            BottomRight,
            TopLeft,
            TopRight,
            Center,
            ManualCustom
        }

        [Header("Target & Mapping")]
        [SerializeField] private MeshFilter _targetMeshFilter;
        [SerializeField] private ProjectionPlane _projectionPlane = ProjectionPlane.AutoDetect;
        [SerializeField] private CornerAnchor _cornerAnchor = CornerAnchor.BottomLeft;
        [SerializeField] private float _textureScale = 1f;
        [SerializeField] private Vector2 _cornerUvOffset = Vector2.zero;
        [SerializeField] private Vector2 _manualPivot = new Vector2(0.5f, 0.5f);

        [Header("Animation")]
        [SerializeField] private float _scrollSpeed = 0.5f;
        [SerializeField] private float _scrollDirection = 1f;
        [SerializeField] private float _speedMultiplier = 60f; // matches the straight belt's linear speed
        [SerializeField] private bool _followPieceAxis = false; // unused, kept for serialized prefabs

        private Mesh _mesh;
        private Vector2[] _baseUV;
        private Vector2[] _workingUV;
        private float _uvAngle;
        private float _axisSign = 1f;
        private bool _isInitialized = false;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (!_isInitialized)
            {
                Initialize();
            }
        }

        private void Initialize()
        {
            if (_targetMeshFilter == null)
            {
                _targetMeshFilter = GetComponent<MeshFilter>() ?? GetComponentInChildren<MeshFilter>();
            }

            if (_targetMeshFilter == null || _targetMeshFilter.sharedMesh == null) return;

            try
            {
                var sourceMesh = _targetMeshFilter.sharedMesh;
                if (_mesh == null)
                {
                    _mesh = Instantiate(sourceMesh);
                    _mesh.name = sourceMesh.name + "_CornerInstance";
                    _targetMeshFilter.mesh = _mesh;
                }

                _baseUV = _cornerAnchor == CornerAnchor.ManualCustom && _mesh.uv != null && _mesh.uv.Length > 0
                    ? _mesh.uv
                    : CornerUvGenerator.Generate(_mesh.vertices, _projectionPlane, _cornerAnchor, _textureScale, _cornerUvOffset, _manualPivot);

                if (_baseUV != null && _baseUV.Length > 0)
                {
                    _workingUV = new Vector2[_baseUV.Length];
                    _isInitialized = true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ConveyorCornerVisual] Could not initialize corner mesh: {e.Message}", this);
            }
        }

        public void OnPieceSpawned(float axisSign)
        {
            _axisSign = Mathf.Abs(axisSign) > 0.001f ? Mathf.Sign(axisSign) : 1f;
            _uvAngle = 0f;
            if (!_isInitialized) Initialize();
        }

        private void Update()
        {
            if (this == null || !_isInitialized || _mesh == null || _baseUV == null) return;

            float speedMult = ConveyorPieceController.GlobalSpeedMultiplier;
            float dir = _scrollDirection * _axisSign;
            _uvAngle += dir * _scrollSpeed * _speedMultiplier * speedMult * Time.deltaTime;
            float rad = _uvAngle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            Vector2 pivot = _cornerAnchor == CornerAnchor.ManualCustom ? _manualPivot : new Vector2(0.5f, 0.5f);

            for (int i = 0; i < _baseUV.Length; i++)
            {
                Vector2 p = _baseUV[i] - pivot;
                _workingUV[i] = pivot + new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos);
            }

            _mesh.uv = _workingUV;
        }

        private void OnDestroy()
        {
            if (Application.isPlaying && _mesh != null)
            {
                Destroy(_mesh);
            }
        }
    }
}
