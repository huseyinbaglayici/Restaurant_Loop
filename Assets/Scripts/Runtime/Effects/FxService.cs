using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RestaurantLoop.Runtime.Effects
{
    // Pooled one-shot particle effects for the gameplay scene, one pool per FxType.
    public class FxService : IDisposable
    {
        private readonly FxSettings _settings;
        private readonly Dictionary<FxType, List<ParticleSystem>> _pools = new();
        private Transform _holder;

        public FxService(FxSettings settings)
        {
            _settings = settings;
        }

        public void Play(FxType type, Vector3 position, float scaleMultiplier = 1f)
        {
            var entry = _settings.Get(type);
            if (entry == null || entry.Prefab == null) return;

            var fx = Take(type, entry);
            var fxTransform = fx.transform;
            fxTransform.position = position;
            fxTransform.localScale = Vector3.one * (entry.Scale * scaleMultiplier);
            fx.gameObject.SetActive(true);
            // Play() is a no-op on a system that is still playing, so restart from a clean stop.
            fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            fx.Play(true);
        }

        private ParticleSystem Take(FxType type, FxSettings.Entry entry)
        {
            if (!_pools.TryGetValue(type, out var pool))
            {
                pool = new List<ParticleSystem>();
                _pools[type] = pool;
            }

            foreach (var fx in pool)
                if (fx != null && !fx.IsAlive(true)) return fx;

            if (_holder == null) _holder = new GameObject("Fx").transform;

            var instance = Object.Instantiate(entry.Prefab, _holder);
            var system = instance.GetComponent<ParticleSystem>() ?? instance.GetComponentInChildren<ParticleSystem>();
            if (system == null)
            {
                system = instance.AddComponent<ParticleSystem>();
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.None;
            pool.Add(system);
            return system;
        }

        public void Dispose()
        {
            if (_holder != null) Object.Destroy(_holder.gameObject);
            _pools.Clear();
        }
    }
}
