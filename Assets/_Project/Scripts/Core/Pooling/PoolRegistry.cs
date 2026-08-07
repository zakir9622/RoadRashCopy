using System;
using System.Collections.Generic;
using UnityEngine;

namespace HighwayRenegade.Core.Pooling
{
    /// <summary>
    /// Centralized registry for <see cref="ObjectPool{T}"/> instances (IMPROVEMENTS.md §0.3).
    ///
    /// Manages pooled components (sparks, particle hits, floating text) without managed
    /// allocations at runtime.
    /// </summary>
    public static class PoolRegistry
    {
        private static readonly Dictionary<Type, object> Pools = new Dictionary<Type, object>();

        /// <summary>Registers a prewarmed pool into the registry.</summary>
        public static void RegisterPool<T>(ObjectPool<T> pool) where T : Component
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            Pools[typeof(T)] = pool;
        }

        /// <summary>Retrieves the pool for a given component type, or null.</summary>
        public static ObjectPool<T> GetPool<T>() where T : Component
        {
            if (Pools.TryGetValue(typeof(T), out object poolObj))
                return poolObj as ObjectPool<T>;
            return null;
        }

        /// <summary>Fetches an instance of T from its pool at the given position and rotation.</summary>
        public static T Spawn<T>(Vector3 position, Quaternion rotation) where T : Component
        {
            var pool = GetPool<T>();
            if (pool == null)
            {
                Debug.LogWarning($"[PoolRegistry] No registered pool for {typeof(T).Name}.");
                return null;
            }
            return pool.Get(position, rotation);
        }

        /// <summary>Recycles an instance of T back to its pool.</summary>
        public static void Recycle<T>(T instance) where T : Component
        {
            if (instance == null) return;
            var pool = GetPool<T>();
            if (pool == null)
            {
                Debug.LogWarning($"[PoolRegistry] No registered pool for {typeof(T).Name}.");
                return;
            }
            pool.Release(instance);
        }

        /// <summary>Clears the registry. Call when transitioning scenes.</summary>
        public static void Clear() => Pools.Clear();
    }
}
