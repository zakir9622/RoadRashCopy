namespace HighwayRenegade.Core.Progression
{
    /// <summary>Persistence seam for testing corruption and backup recovery without disk I/O.</summary>
    public interface ISaveStore
    {
        bool TryRead(out byte[] data);
        bool TryWrite(byte[] data);
        bool TryDeleteBackup();
        bool TryWriteBackup(byte[] data);
        bool TryReadBackup(out byte[] data);
    }
}
