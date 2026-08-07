using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HighwayRenegade.Core.Pooling;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// Guards the zero-allocation contract in ARCHITECTURE.md §4.
    /// If these fail, the pool is allocating during gameplay and the GC will cause hitches.
    /// </summary>
    public sealed class ObjectPoolTests
    {
        private GameObject _prefabObject;
        private BoxCollider _prefab;
        private ObjectPool<BoxCollider> _pool;

        [SetUp]
        public void SetUp()
        {
            _prefabObject = new GameObject("PooledPrefab");
            _prefab = _prefabObject.AddComponent<BoxCollider>();
            _pool = new ObjectPool<BoxCollider>(_prefab, capacity: 4);
            _pool.Prewarm();
        }

        [TearDown]
        public void TearDown()
        {
            if (_prefabObject != null) Object.DestroyImmediate(_prefabObject);
        }

        [Test]
        public void Prewarm_CreatesFullCapacity()
        {
            Assert.AreEqual(4, _pool.AvailableCount);
            Assert.AreEqual(0, _pool.ActiveCount);
        }

        [Test]
        public void Get_MovesObjectFromAvailableToActive()
        {
            var instance = _pool.Get(Vector3.one, Quaternion.identity);

            Assert.IsNotNull(instance);
            Assert.IsTrue(instance.gameObject.activeSelf);
            Assert.AreEqual(Vector3.one, instance.transform.position);
            Assert.AreEqual(3, _pool.AvailableCount);
            Assert.AreEqual(1, _pool.ActiveCount);
        }

        [Test]
        public void Release_ReturnsObjectAndDeactivatesIt()
        {
            var instance = _pool.Get(Vector3.zero, Quaternion.identity);
            _pool.Release(instance);

            Assert.IsFalse(instance.gameObject.activeSelf);
            Assert.AreEqual(4, _pool.AvailableCount);
            Assert.AreEqual(0, _pool.ActiveCount);
        }

        [Test]
        public void Get_WhenExhausted_ReturnsNullRatherThanGrowing()
        {
            // Growing mid-gameplay would hide a tuning bug behind a frame hitch.
            for (int i = 0; i < 4; i++)
                Assert.IsNotNull(_pool.Get(Vector3.zero, Quaternion.identity));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Exhausted"));
            Assert.IsNull(_pool.Get(Vector3.zero, Quaternion.identity));
            Assert.AreEqual(4, _pool.Capacity, "Pool must never grow past its prewarmed capacity.");
        }

        [Test]
        public void Release_Twice_IsIgnored()
        {
            // A double-release would push the same reference twice, letting two callers
            // later receive the *same* object — a brutal class of bug to track down.
            var instance = _pool.Get(Vector3.zero, Quaternion.identity);
            _pool.Release(instance);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("double-release"));
            _pool.Release(instance);

            Assert.AreEqual(4, _pool.AvailableCount, "Double-release must not duplicate the entry.");
        }

        [Test]
        public void ReleaseAll_ReturnsEveryActiveObject()
        {
            for (int i = 0; i < 3; i++) _pool.Get(Vector3.zero, Quaternion.identity);
            _pool.ReleaseAll();

            Assert.AreEqual(0, _pool.ActiveCount);
            Assert.AreEqual(4, _pool.AvailableCount);
        }
    }
}
