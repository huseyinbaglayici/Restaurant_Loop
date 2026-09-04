using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
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
        [SerializeField] private float _speedMultiplier = 60f; // Calibrated to match straight conveyor linear speed
        [SerializeField] private bool _followPieceAxis = false; // Prevents 90/270 degree corners from flipping in reverse

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
                    : GenerateCornerUV(_mesh.vertices);

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

        private Vector2[] GenerateCornerUV(Vector3[] verts)
        {
            if (verts == null || verts.Length == 0) return null;

            int axisU = 0; // X
            int axisV = 2; // Z

            if (_projectionPlane == ProjectionPlane.XY_Vertical)
            {
                axisU = 0;
                axisV = 1;
            }
            else if (_projectionPlane == ProjectionPlane.AutoDetect)
            {
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;

                for (int i = 0; i < verts.Length; i++)
                {
                    minX = Mathf.Min(minX, verts[i].x); maxX = Mathf.Max(maxX, verts[i].x);
                    minY = Mathf.Min(minY, verts[i].y); maxY = Mathf.Max(maxY, verts[i].y);
                    minZ = Mathf.Min(minZ, verts[i].z); maxZ = Mathf.Max(maxZ, verts[i].z);
                }

                float spanX = maxX - minX;
                float spanY = maxY - minY;
                float spanZ = maxZ - minZ;

                if (spanY <= spanX && spanY <= spanZ)
                {
                    axisU = 0; axisV = 2; // XZ plane
                }
                else if (spanZ <= spanX && spanZ <= spanY)
                {
                    axisU = 0; axisV = 1; // XY plane (e.g. -90X imported models)
                }
                else
                {
                    axisU = 1; axisV = 2; // YZ plane
                }
            }

            float minU = float.MaxValue, maxU = float.MinValue;
            float minV = float.MaxValue, maxV = float.MinValue;

            for (int i = 0; i < verts.Length; i++)
            {
                float uVal = GetAxis(verts[i], axisU);
                float vVal = GetAxis(verts[i], axisV);
                minU = Mathf.Min(minU, uVal); maxU = Mathf.Max(maxU, uVal);
                minV = Mathf.Min(minV, vVal); maxV = Mathf.Max(maxV, vVal);
            }

            float spanU = maxU - minU > 0.0001f ? maxU - minU : 1f;
            float spanV = maxV - minV > 0.0001f ? maxV - minV : 1f;
            float radius = Mathf.Max(spanU, spanV);

            Vector2 origin = Vector2.zero;
            Vector2 dirSign = Vector2.one;

            switch (_cornerAnchor)
            {
                case CornerAnchor.BottomRight:
                    origin = new Vector2(maxU, minV);
                    dirSign = new Vector2(-1f, 1f);
                    break;
                case CornerAnchor.TopLeft:
                    origin = new Vector2(minU, maxV);
                    dirSign = new Vector2(1f, -1f);
                    break;
                case CornerAnchor.TopRight:
                    origin = new Vector2(maxU, maxV);
                    dirSign = new Vector2(-1f, -1f);
                    break;
                case CornerAnchor.Center:
                    origin = new Vector2((minU + maxU) * 0.5f, (minV + maxV) * 0.5f);
                    dirSign = new Vector2(1f, 1f);
                    break;
                case CornerAnchor.ManualCustom:
                    origin = _manualPivot;
                    dirSign = Vector2.one;
                    break;
                default: // BottomLeft
                    origin = new Vector2(minU, minV);
                    dirSign = new Vector2(1f, 1f);
                    break;
            }

            var uv = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                float uVal = GetAxis(verts[i], axisU);
                float vVal = GetAxis(verts[i], axisV);

                float normU = ((uVal - origin.x) / radius) * dirSign.x * _textureScale;
                float normV = ((vVal - origin.y) / radius) * dirSign.y * _textureScale;

                float u = 0.5f + normU * 0.5f + _cornerUvOffset.x;
                float v = 0.5f + normV * 0.5f + _cornerUvOffset.y;

                uv[i] = new Vector2(u, v);
            }

            return uv;
        }

        private static float GetAxis(Vector3 v, int axis)
        {
            return axis switch
            {
                0 => v.x,
                1 => v.y,
                _ => v.z
            };
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
