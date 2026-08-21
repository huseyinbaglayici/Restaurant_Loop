using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class ConveyorPieceController : MonoBehaviour, IPoolable<Vector3, float>
    {
        [SerializeField] private Renderer _renderer;
        [SerializeField] private string _texturePropertyName = "_MainTex";
        [SerializeField] private float _scrollSpeed = 0.5f;

        private float _baseTiltX;
        private float _baseTiltZ;
        private Vector2 _uvOffset;

        private void Awake()
        {
            var e = transform.localRotation.eulerAngles;
            _baseTiltX = e.x;
            _baseTiltZ = e.z;
        }

        public void OnSpawned(Vector3 position, float worldYRotation)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(_baseTiltX, worldYRotation, _baseTiltZ));
            _uvOffset = Vector2.zero;
            gameObject.SetActive(true);
        }

        public void OnDespawned()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            _uvOffset.y += _scrollSpeed * Time.deltaTime;
            _renderer.material.SetTextureOffset(_texturePropertyName, _uvOffset);
        }

        public class Pool : MonoPoolableMemoryPool<Vector3, float, ConveyorPieceController>
        {
        }
    }
}