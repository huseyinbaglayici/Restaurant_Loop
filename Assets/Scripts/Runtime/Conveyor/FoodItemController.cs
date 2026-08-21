using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    /// <summary>
    /// Pooled visual for one food stack (a FoodStackData placement — one queue slot, one belt
    /// cell). The stack's Amount is shown as that many unit visuals stacked vertically, not a
    /// single object with a number on it — matches how the level editor and the old prototype
    /// (TestFoodStack) both represent a stack. Units are plain instantiated/destroyed children,
    /// not individually pooled — only the stack as a whole comes from PoolInstaller's pool.
    /// </summary>
    public class FoodItemController : MonoBehaviour, IPoolable<Vector3, FoodStackData>
    {
        [SerializeField] private GameObject _unitPrefab;
        [SerializeField] private float _stackSpacing = 1.05f;
        [SerializeField] private TextMesh _amountLabel;

        [Inject(Optional = true)] private FoodMeshSettings _foodMeshSettings;

        private readonly List<GameObject> _units = new();

        public void OnSpawned(Vector3 position, FoodStackData stack)
        {
            transform.position = position;
            ApplyStack(stack);
            gameObject.SetActive(true);
        }

        public void OnDespawned()
        {
            gameObject.SetActive(false);
        }

        private void ApplyStack(FoodStackData stack)
        {
            while (_units.Count < stack.Amount)
                _units.Add(Instantiate(_unitPrefab, transform));

            while (_units.Count > stack.Amount)
            {
                var top = _units[^1];
                _units.RemoveAt(_units.Count - 1);
                Destroy(top);
            }

            var color = FoodTypeUtil.ToColor(stack.Type);
            Mesh mesh = null;
            bool hasMesh = _foodMeshSettings != null && _foodMeshSettings.TryGetMesh(stack.Type, out mesh);

            for (int i = 0; i < _units.Count; i++)
            {
                _units[i].transform.localPosition = new Vector3(0f, i * _stackSpacing, 0f);

                var renderer = _units[i].GetComponentInChildren<Renderer>();
                if (renderer != null) renderer.material.color = color;

                if (hasMesh)
                {
                    var meshFilter = _units[i].GetComponentInChildren<MeshFilter>();
                    if (meshFilter != null) meshFilter.sharedMesh = mesh;
                }
            }

            if (_amountLabel != null)
            {
                _amountLabel.text = stack.Amount > 1 ? stack.Amount.ToString() : string.Empty;
                _amountLabel.transform.localPosition = new Vector3(0f, _units.Count * _stackSpacing + 0.2f, 0f);
            }
        }

        public class Pool : MonoPoolableMemoryPool<Vector3, FoodStackData, FoodItemController> { }
    }
}
