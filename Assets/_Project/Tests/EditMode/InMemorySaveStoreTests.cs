using NUnit.Framework;
using HighwayRenegade.Core.Progression;

namespace HighwayRenegade.Tests.EditMode
{
    public sealed class InMemorySaveStoreTests
    {
        [Test]
        public void RoundTrip_PrimaryAndBackup()
        {
            var store = new InMemorySaveStore();
            byte[] payload = new byte[] { 1, 2, 3 };

            Assert.IsTrue(store.TryWrite(payload));
            Assert.IsTrue(store.TryRead(out byte[] read));
            CollectionAssert.AreEqual(payload, read);

            Assert.IsTrue(store.TryWriteBackup(payload));
            Assert.IsTrue(store.TryReadBackup(out byte[] backup));
            CollectionAssert.AreEqual(payload, backup);
        }
    }
}
