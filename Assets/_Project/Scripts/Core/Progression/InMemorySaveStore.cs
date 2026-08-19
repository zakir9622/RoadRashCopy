using System.Collections.Generic;

namespace HighwayRenegade.Core.Progression
{
    /// <summary>In-memory fake for EditMode save corruption tests.</summary>
    public sealed class InMemorySaveStore : ISaveStore
    {
        private byte[] _primary;
        private byte[] _backup;

        public bool TryRead(out byte[] data)
        {
            data = _primary;
            return data != null;
        }

        public bool TryWrite(byte[] data)
        {
            _primary = data;
            return true;
        }

        public bool TryDeleteBackup()
        {
            _backup = null;
            return true;
        }

        public bool TryWriteBackup(byte[] data)
        {
            _backup = data;
            return true;
        }

        public bool TryReadBackup(out byte[] data)
        {
            data = _backup;
            return data != null;
        }
    }
}
