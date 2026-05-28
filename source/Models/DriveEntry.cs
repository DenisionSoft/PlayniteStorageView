using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Playnite.SDK;

namespace PlayniteStorageView.Models
{
    /// <summary>
    /// A single entry in the drive picker. Built once per scan.
    /// </summary>
    public sealed class DriveEntry
    {
        /// <summary>Root path as returned by <see cref="DriveInfo.Name"/>, e.g. "C:\".</summary>
        public string Root { get; }

        public string VolumeLabel { get; }
        public DriveType DriveType { get; }
        public string DriveFormat { get; }
        public DriveUsage Usage { get; }
        public IReadOnlyList<GameEntry> Games { get; }

        /// <summary>Game count where the install size is known and counted toward the totals.</summary>
        public int SizedGameCount { get; }

        /// <summary>Game count where the install size is unknown / zero (excluded from totals).</summary>
        public int UnknownSizeGameCount { get; }

        /// <summary>Friendly label for the picker, e.g. "C:\  System" or just "C:\" when no label.</summary>
        public string DisplayName =>
            string.IsNullOrEmpty(VolumeLabel) ? Root : $"{Root}  {VolumeLabel}";

        /// <summary>
        /// Localized caption shown under the legend when at least one game on this drive
        /// has no size data. Format string lives in the Localization resource dictionary
        /// (key LOCStorageViewUnknownNote, takes one {0} placeholder).
        /// </summary>
        public string UnknownSizeCaption
        {
            get
            {
                if (UnknownSizeGameCount <= 0) return string.Empty;
                string template = ResourceProvider.GetString("LOCStorageViewUnknownNote");
                if (string.IsNullOrEmpty(template))
                {
                    // Defensive fallback in case the localization file is missing the key.
                    template = "{0} game(s) on this drive have no size data — they are not counted.";
                }
                return string.Format(CultureInfo.CurrentCulture, template, UnknownSizeGameCount);
            }
        }

        public DriveEntry(
            string root,
            string volumeLabel,
            DriveType driveType,
            string driveFormat,
            DriveUsage usage,
            IReadOnlyList<GameEntry> games)
        {
            Root = root;
            VolumeLabel = volumeLabel ?? string.Empty;
            DriveType = driveType;
            DriveFormat = driveFormat ?? string.Empty;
            Usage = usage;
            Games = games ?? new List<GameEntry>();

            SizedGameCount = Games.Count(g => !g.HasUnknownSize);
            UnknownSizeGameCount = Games.Count(g => g.HasUnknownSize);
        }
    }
}
