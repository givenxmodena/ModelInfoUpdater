using System;
using System.Threading.Tasks;

namespace ModelInfoUpdater.Services
{
    /// <summary>
    /// Interface for application update service.
    /// Abstracts Velopack update functionality for testability and SOLID compliance.
    /// </summary>
    public interface IUpdateService
    {
        /// <summary>
        /// Gets whether an update is currently available.
        /// </summary>
        bool IsUpdateAvailable { get; }

        /// <summary>
        /// Gets whether the update has been downloaded and is ready to apply.
        /// </summary>
        bool IsUpdateDownloaded { get; }

        /// <summary>
        /// Gets the version of the available update, if any.
        /// </summary>
        string? AvailableVersion { get; }

        /// <summary>
        /// Gets the current installed version.
        /// </summary>
        string CurrentVersion { get; }

        /// <summary>
        /// Gets the last error message if update check failed.
        /// </summary>
        string? LastError { get; }

        /// <summary>
        /// Gets whether the Launcher is installed (Velopack installation exists).
        /// </summary>
        bool IsLauncherInstalled { get; }

        /// <summary>
        /// Checks for available updates asynchronously.
        /// </summary>
        /// <returns>True if an update is available, false otherwise.</returns>
        Task<bool> CheckForUpdatesAsync();

        /// <summary>
        /// Downloads the available update asynchronously.
        /// </summary>
        /// <param name="progress">Optional progress callback (0-100).</param>
        /// <returns>True if download was successful, false otherwise.</returns>
        Task<bool> DownloadUpdateAsync(Action<int>? progress = null);

        /// <summary>
        /// Launches the updater to apply updates after Revit closes.
        /// For Revit add-ins, this is the recommended way to apply updates.
        /// </summary>
        /// <param name="silent">If true, updater runs without user interaction.</param>
        /// <returns>True if updater was launched successfully.</returns>
        bool LaunchUpdater(bool silent = true);

        /// <summary>
        /// Applies the downloaded update.
        /// Note: For Revit add-ins, this launches the updater process.
        /// </summary>
        /// <param name="restartApp">Whether to restart the application after update.</param>
        void ApplyUpdate(bool restartApp = false);
    }
}

