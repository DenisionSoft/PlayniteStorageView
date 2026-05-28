using System;
using System.Collections.Generic;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace PlayniteStorageView.Models
{
    /// <summary>
    /// One row in the per-drive game list. Built from a <see cref="Game"/>; we copy
    /// only the fields we need so the entry survives database churn.
    /// </summary>
    public sealed class GameEntry
    {
        public Guid Id { get; }
        public string Name { get; }
        public string IconPath { get; }
        public string SourceName { get; }
        public string PlatformsLabel { get; }
        public string InstallDirectory { get; }
        public ulong? InstallSizeBytes { get; }
        public string InstallSizeDisplay { get; }

        /// <summary>True when <see cref="InstallSizeBytes"/> is null or zero.</summary>
        public bool HasUnknownSize => !InstallSizeBytes.HasValue || InstallSizeBytes.Value == 0UL;

        /// <summary>True when the install directory exists on disk right now.</summary>
        public bool InstallDirectoryExists
        {
            get
            {
                if (string.IsNullOrWhiteSpace(InstallDirectory)) return false;
                try { return System.IO.Directory.Exists(InstallDirectory); }
                catch { return false; }
            }
        }

        private GameEntry(
            Guid id,
            string name,
            string iconPath,
            string sourceName,
            string platformsLabel,
            string installDirectory,
            ulong? installSizeBytes)
        {
            Id = id;
            Name = name;
            IconPath = iconPath;
            SourceName = sourceName;
            PlatformsLabel = platformsLabel;
            InstallDirectory = installDirectory;
            InstallSizeBytes = installSizeBytes;
            InstallSizeDisplay = !installSizeBytes.HasValue || installSizeBytes.Value == 0UL
                ? "—"
                : Services.SizeFormatter.Format(installSizeBytes.Value);
        }

        public static GameEntry From(Game game, IPlayniteAPI api)
        {
            string iconPath = ResolveIconPath(game, api);
            string sourceName = game.Source?.Name ?? "—";
            string platformsLabel = game.Platforms != null && game.Platforms.Any()
                ? string.Join(", ", game.Platforms.Select(p => p.Name))
                : string.Empty;

            return new GameEntry(
                id: game.Id,
                name: game.Name ?? string.Empty,
                iconPath: iconPath,
                sourceName: sourceName,
                platformsLabel: platformsLabel,
                installDirectory: game.InstallDirectory ?? string.Empty,
                installSizeBytes: game.InstallSize);
        }

        /// <summary>
        /// Resolves Playnite icon references into something WPF can display directly.
        /// Database file ids → resolved via Database API. Absolute file paths or HTTP URLs → returned as-is.
        /// </summary>
        private static string ResolveIconPath(Game game, IPlayniteAPI api)
        {
            string icon = game.Icon;
            if (string.IsNullOrWhiteSpace(icon)) return null;

            // HTTP/HTTPS pass through; WPF Image can load them but slowly. Keep them as is.
            if (icon.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                icon.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return icon;
            }

            // Absolute file path on disk → use as is.
            try
            {
                if (System.IO.Path.IsPathRooted(icon) && System.IO.File.Exists(icon))
                {
                    return icon;
                }
            }
            catch { /* malformed path — fall through to db resolver */ }

            // Otherwise treat as a database file id.
            try
            {
                string resolved = api?.Database?.GetFullFilePath(icon);
                if (!string.IsNullOrWhiteSpace(resolved) && System.IO.File.Exists(resolved))
                {
                    return resolved;
                }
            }
            catch { /* missing db file — fall through to null */ }

            return null;
        }
    }
}
