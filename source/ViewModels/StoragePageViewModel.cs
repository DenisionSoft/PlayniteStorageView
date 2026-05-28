using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Playnite.SDK;
using PlayniteStorageView.Models;
using PlayniteStorageView.Services;

namespace PlayniteStorageView.ViewModels
{
    /// <summary>
    /// Observable backing model for the Storage page.
    ///
    /// Responsibilities:
    ///   - Hold the list of drives and the currently selected drive.
    ///   - Run scans off the UI thread, debounced when triggered by burst events
    ///     (install/uninstall/library-updated).
    ///   - Expose RelayCommands for Refresh and OpenInstallFolder.
    /// </summary>
    public sealed class StoragePageViewModel : ObservableObject, IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        private readonly IPlayniteAPI _api;
        private readonly object _debounceLock = new object();
        private CancellationTokenSource _debounceCts;
        private bool _disposed;

        public ObservableCollection<DriveEntry> Drives { get; } = new ObservableCollection<DriveEntry>();

        private DriveEntry _selectedDrive;
        public DriveEntry SelectedDrive
        {
            get => _selectedDrive;
            set
            {
                if (!ReferenceEquals(_selectedDrive, value))
                {
                    _selectedDrive = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _hasNoDrives;
        public bool HasNoDrives
        {
            get => _hasNoDrives;
            private set
            {
                if (_hasNoDrives != value)
                {
                    _hasNoDrives = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand OpenInstallFolderCommand { get; }

        public StoragePageViewModel(IPlayniteAPI api)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));

            RefreshCommand = new RelayCommand(() => RequestRefresh(immediate: true));
            OpenInstallFolderCommand = new RelayCommand<GameEntry>(OpenInstallFolder, CanOpenInstallFolder);

            // Kick off the initial scan immediately so the page is populated by the time
            // the user finishes pointing their mouse at it.
            RequestRefresh(immediate: true);
        }

        /// <summary>
        /// Schedules a scan. When called from a burst of events (game installed/uninstalled),
        /// later calls collapse with earlier ones — only the last one within the debounce
        /// window actually runs. Pass <paramref name="immediate"/> to bypass debouncing
        /// (used by the Refresh button and the initial load).
        /// </summary>
        public void RequestRefresh(bool immediate = false)
        {
            if (_disposed) return;

            CancellationToken token;
            CancellationTokenSource old;
            lock (_debounceLock)
            {
                old = _debounceCts;
                _debounceCts = new CancellationTokenSource();
                token = _debounceCts.Token;
            }

            // Cancel and release the previous CTS once a new one is in place. CancellationTokenSource
            // owns a kernel handle; abandoning it without Dispose() leaves it to the GC's finalizer.
            try { old?.Cancel(); } catch { /* CTS already disposed */ }
            old?.Dispose();

            int delayMs = immediate ? 0 : 750;

            // Fire and forget; exceptions are logged.
            _ = Task.Run(async () =>
            {
                try
                {
                    if (delayMs > 0)
                    {
                        await Task.Delay(delayMs, token).ConfigureAwait(false);
                    }
                    token.ThrowIfCancellationRequested();
                    await DoScanAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Superseded by a newer request — nothing to do.
                }
                catch (ObjectDisposedException)
                {
                    // The page closed mid-flight and the CancellationTokenSource was disposed.
                    // Not a bug; just stop quietly.
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "PlayniteStorageView: scan failed.");
                    SetIsLoadingOnUi(false);
                }
            });
        }

        private async Task DoScanAsync(CancellationToken token)
        {
            SetIsLoadingOnUi(true);
            try
            {
                var drives = await Task.Run(() => StorageScanner.Scan(_api), token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();

                var dispatcher = _api.MainView?.UIDispatcher;
                if (dispatcher == null)
                {
                    // No dispatcher available — apply directly. This is a degenerate path
                    // (Playnite not in Desktop mode) but we handle it for safety.
                    ApplyScanResult(drives);
                    return;
                }

                dispatcher.Invoke(() => ApplyScanResult(drives));
            }
            finally
            {
                SetIsLoadingOnUi(false);
            }
        }

        private void ApplyScanResult(List<DriveEntry> drives)
        {
            string previouslySelectedRoot = SelectedDrive?.Root;

            Drives.Clear();
            foreach (var d in drives)
            {
                Drives.Add(d);
            }

            HasNoDrives = Drives.Count == 0;

            if (Drives.Count == 0)
            {
                SelectedDrive = null;
                return;
            }

            // Restore previous selection if the drive still exists.
            DriveEntry next = null;
            if (!string.IsNullOrEmpty(previouslySelectedRoot))
            {
                next = Drives.FirstOrDefault(d =>
                    string.Equals(d.Root, previouslySelectedRoot, StringComparison.OrdinalIgnoreCase));
            }

            // Default: drive with most installed games (tie-break by total size).
            if (next == null)
            {
                next = Drives
                    .OrderByDescending(d => d.Games.Count)
                    .ThenByDescending(d => d.Usage.TotalBytes)
                    .First();
            }

            SelectedDrive = next;
        }

        private void SetIsLoadingOnUi(bool value)
        {
            var dispatcher = _api.MainView?.UIDispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => IsLoading = value);
            }
            else
            {
                IsLoading = value;
            }
        }

        private bool CanOpenInstallFolder(GameEntry game)
        {
            return game != null && game.InstallDirectoryExists;
        }

        private void OpenInstallFolder(GameEntry game)
        {
            if (game == null) return;
            string dir = game.InstallDirectory;
            if (string.IsNullOrWhiteSpace(dir)) return;

            if (!Directory.Exists(dir))
            {
                Logger.Warn($"PlayniteStorageView: install directory does not exist: {dir}");
                return;
            }

            // Invoke explorer.exe directly with the path as an argument. Trailing slashes get
            // stripped so a quoted path like "C:\Games\Foo\" doesn't end with \" which explorer
            // would parse as an escaped quote.
            string trimmedDir = dir.TrimEnd('\\', '/');
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + trimmedDir + "\"",
                    UseShellExecute = false
                });
                return;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"PlayniteStorageView: explorer.exe launch failed for '{dir}'; trying shell-execute fallback.");
            }

            // Fallback: hand the directory to the shell with its default verb. Less precise than
            // invoking explorer.exe, but works in environments where explorer.exe is not on PATH.
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = trimmedDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"PlayniteStorageView: failed to open install folder '{dir}'.");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            CancellationTokenSource ctsToDispose;
            lock (_debounceLock)
            {
                ctsToDispose = _debounceCts;
                _debounceCts = null;
            }

            try { ctsToDispose?.Cancel(); } catch { /* already disposed */ }
            ctsToDispose?.Dispose();
        }
    }
}
