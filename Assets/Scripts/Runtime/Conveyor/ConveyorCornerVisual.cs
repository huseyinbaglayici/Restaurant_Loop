using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RestaurantLoop.Runtime.Conveyor
{
    public class ConveyorCornerVisual : MonoBehaviour, IConveyorPieceVisual
    {
        public enum CornerAnchor
        {
            BottomLeft,
            BottomRight,
            TopLeft,
            TopRight,
            ManualCustom
        }

        public enum ProjectionPlane
        {
            XZ_TopDown,
            XY_Vertical
        }

        [SerializeField] private MeshFilter _targetMeshFilter;
        [SerializeField] private ProjectionPlane _projectionPlane = ProjectionPlane.XZ_TopDown;
        [SerializeField] private CornerAnchor _cornerAnchor = CornerAnchor.BottomLeft;
        [SerializeField] private float _textureScale = 1f;
        [SerializeField] private Vector2 _cornerUvOffset = Vector2.zero;
        [SerializeField] private Vector2 _manualPivot = new Vector2(0.5f, 0.5f);

        [SerializeField] private float _scrollSpeed = 0.5f;
        [SerializeField] private float _scrollDirection = 1f;

        private Mesh _mesh;
        private Vector2[] _baseUV;
        private Vector2[] _workingUV;
        private float _uvAngle;
        private float _axisSign = 1f;

        private void Awake()
        {
            if (_targetMeshFilter == null)
            {
                _targetMeshFilter = GetComponent<MeshFilter>() ?? GetComponentInChildren<MeshFilter>();
            }

            if (_targetMeshFilter == null || _targetMeshFilter.sharedMesh == null) return;
            if (!_targetMeshFilter.sharedMesh.isReadable) return;

            _mesh = _targetMeshFilter.mesh;
            _baseUV = _cornerAnchor == CornerAnchor.ManualCustom ? _mesh.uv : GenerateCornerUV(_mesh.vertices);
            _workingUV = new Vector2[_baseUV.Length];
        }

        private Vector2[] GenerateCornerUV(Vector3[] verts)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < verts.Length; i++)
            {
                float vx = verts[i].x;
                float vz = _projectionPlane == ProjectionPlane.XZ_TopDown ? verts[i].z : verts[i].y;

                minX = Mathf.Min(minX, vx);
                maxX = Mathf.Max(maxX, vx);
                minZ = Mathf.Min(minZ, vz);
                maxZ = Mathf.Max(maxZ, vz);
            }

            float spanX = maxX - minX > 0.001f ? maxX - minX : 1f;
            float spanZ = maxZ - minZ > 0.001f ? maxZ - minZ : 1f;
            float radius = Mathf.Max(spanX, spanZ);

            Vector2 origin;
            Vector2 dirSign;

            switch (_cornerAnchor)
            {
                case CornerAnchor.BottomRight:
                    origin = new Vector2(maxX, minZ);
                    dirSign = new Vector2(-1f, 1f);
                    break;
                case CornerAnchor.TopLeft:
                    origin = new Vector2(minX, maxZ);
                    dirSign = new Vector2(1f, -1f);
                    break;
                case CornerAnchor.TopRight:
                    origin = new Vector2(maxX, maxZ);
                    dirSign = new Vector2(-1f, -1f);
                    break;
                default: // BottomLeft
                    origin = new Vector2(minX, minZ);
                    dirSign = new Vector2(1f, 1f);
                    break;
            }

            var uv = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                float vx = verts[i].x;
                float vz = _projectionPlane == ProjectionPlane.XZ_TopDown ? verts[i].z : verts[i].y;

                float normX = ((vx - origin.x) / radius) * dirSign.x * _textureScale;
                float normZ = ((vz - origin.y) / radius) * dirSign.y * _textureScale;

                float u = 0.5f + normX * 0.5f + _cornerUvOffset.x;
                float v = 0.5f + normZ * 0.5f + _cornerUvOffset.y;

                uv[i] = new Vector2(u, v);
            }

            return uv;
        }

        public void OnPieceSpawned(float axisSign)
        {
            _axisSign = axisSign;
            _uvAngle = 0f;
        }

        private void Update()
        {
            if (this == null) return;
            if (_mesh == null || _baseUV == null) return;

            _uvAngle += _axisSign * _scrollDirection * _scrollSpeed * Time.deltaTime * 360f;
            float rad = _uvAngle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            Vector2 pivot = _cornerAnchor == CornerAnchor.ManualCustom ? _manualPivot : new Vector2(0.5f, 0.5f);

            for (int i = 0; i < _baseUV.Length; i++)
            {
                Vector2 p = _baseUV[i] - pivot;
                _workingUV[i] = pivot + new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos);
            }

            _mesh.SetUVs(0, _workingUV);
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
