using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace ModelInfoUpdater.Services
{
    /// <summary>
    /// Velopack-based update service for the Revit add-in.
    ///
    /// ARCHITECTURE:
    /// - Uses GitHub Releases as the update source
    /// - Downloads updates while Revit is running (DLL is locked)
    /// - Launches a separate Updater.exe that waits for Revit to close
    /// - Updater.exe applies the update after Revit closes
    /// - New version is loaded on next Revit startup
    /// </summary>
    public class VelopackUpdateService : IUpdateService
    {
        private readonly UpdateManager? _updateManager;
        private UpdateInfo? _updateInfo;
        private readonly string _githubRepoUrl;
        private bool _updateDownloaded;

        /// <summary>
        /// Gets whether an update is currently available.
        /// </summary>
        public bool IsUpdateAvailable => _updateInfo?.TargetFullRelease != null;

        /// <summary>
        /// Gets whether the update has been downloaded and is ready to apply.
        /// </summary>
        public bool IsUpdateDownloaded => _updateDownloaded;

        /// <summary>
        /// Gets the version of the available update, if any.
        /// </summary>
        public string? AvailableVersion => _updateInfo?.TargetFullRelease?.Version?.ToString();

        /// <summary>
        /// Gets the current installed version.
        /// </summary>
        public string CurrentVersion => _updateManager?.CurrentVersion?.ToString() ?? GetAssemblyVersion();

        /// <summary>
        /// Initializes a new instance of the VelopackUpdateService.
        /// </summary>
        /// <param name="githubRepoUrl">GitHub repository URL (e.g., "https://github.com/owner/repo")</param>
        public VelopackUpdateService(string githubRepoUrl)
        {
            _githubRepoUrl = githubRepoUrl;
            _updateDownloaded = false;

            try
            {
                // Use GitHub as the update source - Velopack will look for releases
                var source = new GithubSource(_githubRepoUrl, null, false);
                _updateManager = new UpdateManager(source);
            }
            catch (Exception ex)
            {
                // If Velopack isn't properly installed (e.g., running in debug),
                // the UpdateManager may fail. We handle this gracefully.
                System.Diagnostics.Debug.WriteLine($"[VelopackUpdateService] Init failed: {ex.Message}");
                _updateManager = null;
            }
        }

        private static string GetAssemblyVersion()
        {
            try
            {
                return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }

        /// <summary>
        /// Checks for available updates asynchronously.
        /// </summary>
        /// <returns>True if an update is available, false otherwise.</returns>
        public async Task<bool> CheckForUpdatesAsync()
        {
            if (_updateManager == null)
                return false;

            try
            {
                _updateInfo = await _updateManager.CheckForUpdatesAsync();
                return IsUpdateAvailable;
            }
            catch (Exception)
            {
                // Network error, invalid feed, etc.
                _updateInfo = null;
                return false;
            }
        }

        /// <summary>
        /// Downloads the available update asynchronously.
        /// </summary>
        /// <param name="progress">Optional progress callback (0-100).</param>
        /// <returns>True if download was successful, false otherwise.</returns>
        public async Task<bool> DownloadUpdateAsync(Action<int>? progress = null)
        {
            if (_updateManager == null || _updateInfo == null)
                return false;

            try
            {
                await _updateManager.DownloadUpdatesAsync(
                    _updateInfo,
                    p => progress?.Invoke(p)
                );
                _updateDownloaded = true;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VelopackUpdateService] Download failed: {ex.Message}");
                _updateDownloaded = false;
                return false;
            }
        }

        /// <summary>
        /// Launches the updater executable to apply the update after Revit closes.
        /// This is the recommended way to apply updates for Revit add-ins.
        /// </summary>
        /// <param name="silent">If true, updater runs without console output.</param>
        /// <returns>True if updater was launched successfully.</returns>
        public bool LaunchUpdater(bool silent = true)
        {
            try
            {
                // Find the updater executable next to our DLL
                string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string updaterPath = Path.Combine(assemblyDir, "ModelInfoUpdater.Updater.exe");

                if (!File.Exists(updaterPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[VelopackUpdateService] Updater not found at: {updaterPath}");
                    return false;
                }

                // Get current Revit process ID so updater knows what to wait for
                int revitPid = Process.GetCurrentProcess().Id;

                // Build arguments
                string args = $"--wait-pid={revitPid}";
                if (silent) args += " --silent";

                // Launch the updater
                var startInfo = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = silent,
                    WindowStyle = silent ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
                };

                Process.Start(startInfo);
                System.Diagnostics.Debug.WriteLine($"[VelopackUpdateService] Launched updater: {updaterPath} {args}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VelopackUpdateService] Failed to launch updater: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Applies the downloaded update.
        ///
        /// WARNING: For Revit add-ins, this will NOT work while Revit is running
        /// because the DLL is locked. Use LaunchUpdater() instead, which starts
        /// a separate process that waits for Revit to close before applying.
        /// </summary>
        /// <param name="restartApp">Whether to restart. Not applicable for Revit add-ins.</param>
        public void ApplyUpdate(bool restartApp = false)
        {
            // For Revit add-ins, we launch the updater instead of trying to apply directly
            LaunchUpdater(silent: true);
        }
    }
}

