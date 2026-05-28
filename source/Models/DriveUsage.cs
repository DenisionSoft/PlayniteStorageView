namespace PlayniteStorageView.Models
{
    /// <summary>
    /// Bytes-level breakdown of a single drive's space usage.
    /// All values are non-negative; <see cref="OtherBytes"/> is clamped at zero
    /// in <see cref="StorageScanner"/> so that GamesBytes + OtherBytes + FreeBytes
    /// always equals TotalBytes exactly (modulo the clamp adjustment).
    /// </summary>
    public sealed class DriveUsage
    {
        public ulong TotalBytes { get; }
        public ulong FreeBytes { get; }
        public ulong UsedBytes { get; }
        public ulong GamesBytes { get; }
        public ulong OtherBytes { get; }

        public string TotalDisplay => Services.SizeFormatter.Format(TotalBytes);
        public string FreeDisplay => Services.SizeFormatter.Format(FreeBytes);
        public string UsedDisplay => Services.SizeFormatter.Format(UsedBytes);
        public string GamesDisplay => Services.SizeFormatter.Format(GamesBytes);
        public string OtherDisplay => Services.SizeFormatter.Format(OtherBytes);

        public DriveUsage(ulong totalBytes, ulong freeBytes, ulong usedBytes, ulong gamesBytes, ulong otherBytes)
        {
            TotalBytes = totalBytes;
            FreeBytes = freeBytes;
            UsedBytes = usedBytes;
            GamesBytes = gamesBytes;
            OtherBytes = otherBytes;
        }
    }
}
