using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class ConveyorPieceController : MonoBehaviour, IPoolable<Vector3, float>
    {
        [SerializeField] private Renderer _renderer;
        [SerializeField] private string _texturePropertyName = "_MainTex";

        [SerializeField] private float _scrollSpeed = 0.5f;
        [SerializeField] private float _scrollDirection = 1f;

        [SerializeField] private bool _rotateUV;
        [SerializeField] private Vector2 _uvPivot = Vector2.zero;

        private float _baseTiltX;
        private float _baseTiltZ;
        private Vector2 _uvOffset;
        private float _axisSign;
        private float _uvAngle;

        private Mesh _mesh;
        private Vector2[] _baseUV;
        private Vector2[] _workingUV;

        private void Awake()
        {
            var e = transform.localRotation.eulerAngles;
            _baseTiltX = e.x;
            _baseTiltZ = e.z;

            if (_rotateUV)
            {
                _mesh = GetComponent<MeshFilter>().mesh;
                _baseUV = _mesh.uv;
                _workingUV = new Vector2[_baseUV.Length];
            }
        }

        public void OnSpawned(Vector3 position, float worldYRotation)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(_baseTiltX, worldYRotation, _baseTiltZ));
            _uvOffset = Vector2.zero;
            _uvAngle = 0f;
            // axis flip at 90/270
            _axisSign = Mathf.Cos(2f * worldYRotation * Mathf.Deg2Rad);
            gameObject.SetActive(true);
        }

        public void OnDespawned()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_rotateUV)
            {
                _uvAngle += _axisSign * _scrollDirection * _scrollSpeed * Time.deltaTime * 360f;
                float rad = _uvAngle * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);
                for (int i = 0; i < _baseUV.Length; i++)
                {
                    Vector2 p = _baseUV[i] - _uvPivot;
                    _workingUV[i] = _uvPivot + new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos);
                }
                _mesh.SetUVs(0, _workingUV);
            }
            else
            {
                _uvOffset.y += _axisSign * _scrollDirection * _scrollSpeed * Time.deltaTime;
                _renderer.material.SetTextureOffset(_texturePropertyName, _uvOffset);
            }
        }

        public class Pool : MonoPoolableMemoryPool<Vector3, float, ConveyorPieceController>
        {
        }
    }
}