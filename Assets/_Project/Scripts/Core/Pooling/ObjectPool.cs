using System.Collections.Generic;
using UnityEngine;

namespace HighwayRenegade.Core.Pooling
{
    /// <summary>
    /// Fixed-capacity pool of pre-instantiated components.
    ///
    /// Enforces the zero-allocation mandate (ARCHITECTURE.md §4): every object this pool
    /// will ever hand out is created during <see cref="Prewarm"/>, which must run on a
    /// loading screen. Once gameplay starts, Get/Release only move references between
    /// a stack and an active set — no Instantiate, no managed allocation, no GC pressure.
    /// </summary>
    /// <typeparam name="T">Pooled component type.</typeparam>
    public sealed class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _available;
        private readonly HashSet<T> _active;
        private readonly int _capacity;

        /// <summary>Objects currently checked out by callers.</summary>
        public int ActiveCount => _active.Count;

        /// <summary>Objects sitting idle and ready to be handed out.</summary>
        public int AvailableCount => _available.Count;

        /// <summary>Total objects created at prewarm time. Never grows.</summary>
        public int Capacity => _capacity;

        public ObjectPool(T prefab, int capacity, Transform parent = null)
        {
            if (prefab == null)
                throw new System.ArgumentNullException(nameof(prefab));
            if (capacity <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(capacity));

            _prefab = prefab;
            _parent = parent;
            _capacity = capacity;

            // Collections sized up-front so they never rehash/resize mid-gameplay.
            _available = new Stack<T>(capacity);
            _active = new HashSet<T>();
        }

        /// <summary>
        /// Instantiates the full capacity. Call from a loading screen only — this is the
        /// single point where allocation is permitted.
        /// </summary>
        public void Prewarm()
        {
            for (int i = 0; i < _capacity; i++)
            {
                T instance = Object.Instantiate(_prefab, _parent);
                instance.gameObject.SetActive(false);
                _available.Push(instance);
            }
        }

        /// <summary>
        /// Checks out an object, or returns <c>null</c> if the pool is exhausted.
        /// Returning null rather than growing is deliberate: a starved pool is a tuning
        /// bug that must surface in the Profiler, not get papered over by a hitch-inducing
        /// mid-gameplay Instantiate.
        /// </summary>
        public T Get(Vector3 position, Quaternion rotation)
        {
            if (_available.Count == 0)
            {
                Debug.LogWarning($"[ObjectPool<{typeof(T).Name}>] Exhausted at capacity {_capacity}. Increase prewarm count.");
                return null;
            }

            T instance = _available.Pop();
            Transform t = instance.transform;
            t.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);
            _active.Add(instance);
            return instance;
        }

        /// <summary>
        /// Returns an object to the pool. Double-releases are ignored — releasing an object
        /// twice would corrupt the stack by making the same reference available twice.
        /// </summary>
        public void Release(T instance)
        {
            if (instance == null) return;

            if (!_active.Remove(instance))
            {
                Debug.LogWarning($"[ObjectPool<{typeof(T).Name}>] Release of an object this pool does not own (or a double-release). Ignored.");
                return;
            }

            instance.gameObject.SetActive(false);
            instance.transform.SetParent(_parent, false);
            _available.Push(instance);
        }

        /// <summary>Returns every active object. Safe to call between races.</summary>
        public void ReleaseAll()
        {
            if (_active.Count == 0) return;

            // Copy first: Release mutates _active, so we cannot enumerate it directly.
            var buffer = new T[_active.Count];
            _active.CopyTo(buffer);
            for (int i = 0; i < buffer.Length; i++)
                Release(buffer[i]);
        }
    }
}
