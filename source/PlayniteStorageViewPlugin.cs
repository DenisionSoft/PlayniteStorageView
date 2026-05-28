using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using PlayniteStorageView.ViewModels;

namespace PlayniteStorageView
{
    /// <summary>
    /// Storage View — a read-only Playnite sidebar page that shows, per local drive:
    ///   - a stacked bar of Games / Other / Free space
    ///   - a sortable list of installed Playnite games on that drive with their sizes
    /// </summary>
    public class PlayniteStorageViewPlugin : GenericPlugin
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        // GUID is permanent — never regenerate. Used by Playnite as the plugin identity
        // for data paths and inter-plugin references.
        public override Guid Id { get; } = Guid.Parse("b27a1f4c-d3e8-4a2b-9f6c-5e8a1b7d3c4e");

        /// <summary>
        /// Currently-bound viewmodel, if the storage page is open. The view sets this
        /// in its constructor and clears it on Unloaded. Used to push refresh requests
        /// from plugin lifecycle events without holding view references.
        /// </summary>
        internal StoragePageViewModel ActiveViewModel { get; set; }

        public PlayniteStorageViewPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties { HasSettings = false };
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            // Sidebar items only render in Desktop mode; skip allocating in Fullscreen.
            // Wrapped in try/catch in case ApplicationInfo isn't fully initialized yet during
            // very early Playnite startup paths.
            ApplicationMode mode;
            try
            {
                mode = PlayniteApi?.ApplicationInfo?.Mode ?? ApplicationMode.Desktop;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "PlayniteStorageView: failed to read ApplicationInfo.Mode; assuming Desktop.");
                mode = ApplicationMode.Desktop;
            }

            if (mode != ApplicationMode.Desktop)
            {
                yield break;
            }

            yield return new SidebarItem
            {
                Title = ResourceProvider.GetString("LOCStorageViewSidebarTitle"),
                Type = SiderbarItemType.View,
                Icon = BuildSidebarIcon(),
                Opened = () => new Views.StoragePage(this, PlayniteApi)
            };
        }

        /// <summary>
        /// Builds the sidebar glyph icon using Playnite's bundled IcoFont. Codepoint U+EF43 is
        /// the same glyph Playnite itself uses for install-size related UI.
        /// </summary>
        private static object BuildSidebarIcon()
        {
            try
            {
                return new TextBlock
                {
                    Text = "",
                    FontFamily = ResourceProvider.GetResource("FontIcoFont") as FontFamily
                };
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "PlayniteStorageView: failed to build IcoFont glyph icon; falling back to text.");
                return new TextBlock { Text = "S" };
            }
        }

        // ---- Lifecycle hooks: nudge the open viewmodel to refresh when state changes ----
        // ActiveViewModel.RequestRefresh debounces internally so a burst of installs
        // collapses into a single scan.

        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            ActiveViewModel?.RequestRefresh();
        }

        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {
            ActiveViewModel?.RequestRefresh();
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            ActiveViewModel?.RequestRefresh();
        }
    }
}
