using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;
using PlayniteStorageView.Models;

namespace PlayniteStorageView.Services
{
    /// <summary>
    /// Scans local drives and buckets installed Playnite games against them.
    /// All disk-touching work lives here; safe to call from a background thread.
    /// </summary>
    internal static class StorageScanner
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        public static List<DriveEntry> Scan(IPlayniteAPI api)
        {
            if (api == null) return new List<DriveEntry>();

            // Snapshot the games collection once — Playnite's collections are not guaranteed
            // safe to enumerate cross-thread, and we want a stable view for the scan.
            List<Game> games;
            try
            {
                games = api.Database.Games.ToList();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "PlayniteStorageView: failed to snapshot games collection.");
                games = new List<Game>();
            }

            // Group installed, non-hidden games by their root drive ("C:\", "D:\", ...).
            // GetInstallDrive() returns string.Empty for uninstalled or malformed paths (dropped here).
            // UNC install paths produce UNC roots that won't match any local DriveInfo entry, so
            // such games fall off the bucket grid naturally.
            //
            // Hidden games are excluded so the totals honor the user's curation — both Playnite's
            // built-in "Hide this game" and the DuplicateHider plugin's automatic hiding write to
            // the same Game.Hidden flag.
            var byDrive = games
                .Where(g => g.IsInstalled && !g.Hidden)
                .Select(g => new { Game = g, Drive = SafeGetInstallDrive(g) })
                .Where(x => !string.IsNullOrEmpty(x.Drive))
                .GroupBy(x => x.Drive, x => x.Game, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(grp => grp.Key, grp => grp.ToList(), StringComparer.OrdinalIgnoreCase);

            var result = new List<DriveEntry>();
            DriveInfo[] drives;
            try
            {
                drives = DriveInfo.GetDrives();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "PlayniteStorageView: DriveInfo.GetDrives() failed.");
                return result;
            }

            foreach (var d in drives)
            {
                if (!IsAcceptableDriveType(d.DriveType)) continue;

                // .IsReady can throw on misbehaving devices.
                bool ready;
                try { ready = d.IsReady; }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"PlayniteStorageView: drive '{d.Name}' threw on IsReady; skipping.");
                    continue;
                }
                if (!ready) continue;

                try
                {
                    ulong total = unchecked((ulong)Math.Max(0L, d.TotalSize));
                    ulong free  = unchecked((ulong)Math.Max(0L, d.AvailableFreeSpace));
                    ulong used  = total >= free ? total - free : 0UL;

                    byDrive.TryGetValue(d.Name, out var gamesOnDrive);
                    if (gamesOnDrive == null) gamesOnDrive = new List<Game>();

                    ulong gamesBytes = 0UL;
                    foreach (var g in gamesOnDrive)
                    {
                        if (g.InstallSize.HasValue) gamesBytes += g.InstallSize.Value;
                    }

                    // Defensive clamp: if reported game sizes ever exceed the drive's used space
                    // (stale scans, junction-pointed installs, etc.) keep OtherBytes at zero
                    // rather than going negative.
                    ulong otherBytes = used > gamesBytes ? used - gamesBytes : 0UL;

                    var usage = new DriveUsage(total, free, used, gamesBytes, otherBytes);

                    var entries = gamesOnDrive
                        .Select(g => GameEntry.From(g, api))
                        .OrderByDescending(g => g.InstallSizeBytes ?? 0UL)
                        .ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToList();

                    result.Add(new DriveEntry(
                        root: d.Name,
                        volumeLabel: SafeVolumeLabel(d),
                        driveType: d.DriveType,
                        driveFormat: SafeDriveFormat(d),
                        usage: usage,
                        games: entries));
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"PlayniteStorageView: skipping drive '{d.Name}' due to error.");
                }
            }

            // Stable sort: by root letter for predictable ordering in the picker.
            result.Sort((a, b) => string.Compare(a.Root, b.Root, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private static bool IsAcceptableDriveType(DriveType t)
        {
            // Skip network drives (can hang on disconnected shares), optical drives,
            // and anything we can't classify. Keep Fixed, Removable, and Ram drives.
            switch (t)
            {
                case DriveType.Network:
                case DriveType.CDRom:
                case DriveType.NoRootDirectory:
                case DriveType.Unknown:
                    return false;
                default:
                    return true;
            }
        }

        private static string SafeGetInstallDrive(Game g)
        {
            try { return g.GetInstallDrive(); }
            catch { return string.Empty; }
        }

        private static string SafeVolumeLabel(DriveInfo d)
        {
            try { return d.VolumeLabel ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static string SafeDriveFormat(DriveInfo d)
        {
            try { return d.DriveFormat ?? string.Empty; }
            catch { return string.Empty; }
        }
    }
}
