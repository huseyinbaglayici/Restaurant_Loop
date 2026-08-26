using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class ConveyorStraightVisual : MonoBehaviour, IConveyorPieceVisual
    {
        [SerializeField] private Renderer _renderer;
        [SerializeField] private string _texturePropertyName = "_BaseMap";
        [SerializeField] private float _scrollSpeed = 0.5f;
        [SerializeField] private float _scrollDirection = 1f;

        private Material _material;
        private Vector2 _uvOffset;
        private float _axisSign = 1f;

        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
            }

            _material = _renderer.material;
        }

        public void OnPieceSpawned(float axisSign)
        {
            _axisSign = axisSign;
            _uvOffset = Vector2.zero;
        }

        private void Update()
        {
            _uvOffset.y += _axisSign * _scrollDirection * _scrollSpeed * Time.deltaTime;
            _material.SetTextureOffset(_texturePropertyName, _uvOffset);
        }
    }
}
