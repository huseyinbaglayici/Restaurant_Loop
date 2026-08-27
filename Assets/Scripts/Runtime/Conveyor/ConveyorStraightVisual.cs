using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class ConveyorStraightVisual : MonoBehaviour, IConveyorPieceVisual
    {
        [SerializeField] private Renderer _renderer;
        [SerializeField] private string _texturePropertyName = "_BaseMap";
        [SerializeField] private float _scrollSpeed = 0.5f;
        [SerializeField] private float _scrollDirection = 1f;

        private MaterialPropertyBlock _propertyBlock;
        private int _stPropertyId;
        private Vector2 _tiling;
        private Vector2 _uvOffset;
        private float _axisSign = 1f;

        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
            }

            _stPropertyId = Shader.PropertyToID(_texturePropertyName + "_ST");
            _tiling = _renderer.sharedMaterial.GetTextureScale(_texturePropertyName);
            _propertyBlock = new MaterialPropertyBlock();
        }

        public void OnPieceSpawned(float axisSign)
        {
            _axisSign = axisSign;
            _uvOffset = Vector2.zero;
        }

        private void Update()
        {
            _uvOffset.y += _axisSign * _scrollDirection * _scrollSpeed * Time.deltaTime;

            _renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetVector(_stPropertyId, new Vector4(_tiling.x, _tiling.y, _uvOffset.x, _uvOffset.y));
            _renderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
