using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Playnite.SDK;
using PlayniteStorageView.Models;
using PlayniteStorageView.ViewModels;

namespace PlayniteStorageView.Views
{
    /// <summary>
    /// The Storage sidebar page. Pure data binding for most of the UI;
    /// code-behind only handles two WPF-specific quirks:
    ///   1. Rebuilding the stacked bar's column widths when SelectedDrive changes,
    ///      because ColumnDefinitions don't inherit DataContext.
    ///   2. Click-to-sort on the game-list column headers (lightweight ICollectionView sorting).
    /// </summary>
    public partial class StoragePage : UserControl
    {
        private readonly PlayniteStorageViewPlugin _plugin;
        private readonly StoragePageViewModel _vm;

        private string _currentSortColumn;
        private ListSortDirection _currentSortDirection;
        private GridViewColumnHeader _currentSortHeader;
        // Cached original Content of the header currently decorated with the sort arrow.
        // We restore this on the previous header before decorating a new one (and on the
        // current header when SelectedDrive changes), so the localized DynamicResource
        // string is never permanently lost.
        private object _currentSortHeaderOriginalContent;

        private const string SortArrowAsc = "  ▲";   // ▲
        private const string SortArrowDesc = "  ▼";  // ▼

        public StoragePage(PlayniteStorageViewPlugin plugin, IPlayniteAPI api)
        {
            InitializeComponent();

            _plugin = plugin;
            _vm = new StoragePageViewModel(api);
            DataContext = _vm;

            _vm.PropertyChanged += Vm_PropertyChanged;
            _plugin.ActiveViewModel = _vm;

            Unloaded += StoragePage_Unloaded;
            Loaded += (_, __) => RebuildBar(); // first-time paint in case selection arrived before Loaded
        }

        private void StoragePage_Unloaded(object sender, RoutedEventArgs e)
        {
            _vm.PropertyChanged -= Vm_PropertyChanged;
            if (_plugin != null && ReferenceEquals(_plugin.ActiveViewModel, _vm))
            {
                _plugin.ActiveViewModel = null;
            }
            // Cancel any in-flight scan and release the CancellationTokenSource.
            _vm.Dispose();
        }

        private void Vm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StoragePageViewModel.SelectedDrive))
            {
                RebuildBar();
                ResetSort();
            }
        }

        // ---- Stacked bar -----------------------------------------------------

        private void RebuildBar()
        {
            if (BarGrid == null) return;
            BarGrid.ColumnDefinitions.Clear();

            var usage = _vm.SelectedDrive?.Usage;
            if (usage == null)
            {
                // Empty bar — three zero-star columns so the rectangles collapse to nothing.
                for (int i = 0; i < 3; i++)
                {
                    BarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) });
                }
                return;
            }

            // GridUnitType.Star with value 0 is fine — that column simply gets no space.
            BarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = StarOf(usage.GamesBytes) });
            BarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = StarOf(usage.OtherBytes) });
            BarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = StarOf(usage.FreeBytes) });

            // Edge case: drive reports zero total (or some weirdness made all three zero).
            // Give the "Free" segment a single star so the bar isn't an invisible 0px strip.
            if (usage.TotalBytes == 0UL)
            {
                BarGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            }
        }

        private static GridLength StarOf(ulong bytes)
        {
            return new GridLength(bytes, GridUnitType.Star);
        }

        // ---- Column header sorting -------------------------------------------

        private void GameList_HeaderClicked(object sender, RoutedEventArgs e)
        {
            var header = e.OriginalSource as GridViewColumnHeader;
            if (header == null) return;

            // Skip the dummy icon-column header (Tag="noSort") and the gripper header (no Tag).
            var tag = header.Tag as string;
            if (string.IsNullOrEmpty(tag) || string.Equals(tag, "noSort", StringComparison.Ordinal)) return;

            ListSortDirection nextDir;
            if (string.Equals(_currentSortColumn, tag, StringComparison.Ordinal))
            {
                nextDir = _currentSortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }
            else
            {
                // Default first click: descending for numeric Size, ascending for everything else.
                nextDir = string.Equals(tag, nameof(GameEntry.InstallSizeBytes), StringComparison.Ordinal)
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }

            ApplySort(tag, nextDir);
            UpdateHeaderArrow(header, nextDir);
            _currentSortColumn = tag;
            _currentSortDirection = nextDir;
            _currentSortHeader = header;
        }

        /// <summary>
        /// Decorates <paramref name="newHeader"/>'s Content with an arrow glyph reflecting
        /// <paramref name="direction"/>, and restores the previous sort header's Content if any.
        /// We keep the per-header original cached so the localized DynamicResource string can
        /// be restored verbatim when sort changes or the page resets.
        /// </summary>
        private void UpdateHeaderArrow(GridViewColumnHeader newHeader, ListSortDirection direction)
        {
            if (newHeader == null) return;

            // If sorting moved to a different header, restore the previous one first.
            if (_currentSortHeader != null && _currentSortHeader != newHeader)
            {
                _currentSortHeader.Content = _currentSortHeaderOriginalContent;
                _currentSortHeaderOriginalContent = null;
            }

            // Cache the new header's original Content the first time it becomes the sort header.
            if (_currentSortHeader != newHeader || _currentSortHeaderOriginalContent == null)
            {
                _currentSortHeaderOriginalContent = newHeader.Content;
            }

            string baseText = _currentSortHeaderOriginalContent as string ?? _currentSortHeaderOriginalContent?.ToString() ?? string.Empty;
            string arrow = direction == ListSortDirection.Ascending ? SortArrowAsc : SortArrowDesc;
            newHeader.Content = baseText + arrow;
        }

        private void ApplySort(string property, ListSortDirection direction)
        {
            if (GameList == null) return;
            var view = CollectionViewSource.GetDefaultView(GameList.ItemsSource);
            if (view == null) return;

            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(property, direction));
            view.Refresh();
        }

        /// <summary>
        /// Called when the selected drive changes — the ItemsSource was replaced under us,
        /// so we drop the sort state. Default ordering of the list comes from the scanner
        /// (size descending, then name).
        /// </summary>
        private void ResetSort()
        {
            // Restore the previously decorated header's original Content so it doesn't
            // keep showing a stale arrow while the underlying ItemsSource has switched
            // back to the scanner's default ordering.
            if (_currentSortHeader != null && _currentSortHeaderOriginalContent != null)
            {
                _currentSortHeader.Content = _currentSortHeaderOriginalContent;
            }
            _currentSortColumn = null;
            _currentSortHeader = null;
            _currentSortHeaderOriginalContent = null;
        }
    }
}
