using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq;
using Velopack;
using Velopack.Sources;
using ModelInfoUpdater.Logging;

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
    ///
    /// NOTE: Repository must be PUBLIC for unauthenticated access to releases.
    /// For private repos, a GitHub Personal Access Token must be provided.
    /// </summary>
    public class VelopackUpdateService : IUpdateService
    {
        private readonly UpdateManager? _updateManager;
        private UpdateInfo? _updateInfo;
        private readonly string _githubRepoUrl;
        private bool _updateDownloaded;
        private string? _lastError;

        /// <summary>
        /// Gets whether an update is currently available.
        /// </summary>
        public bool IsUpdateAvailable => _updateInfo?.TargetFullRelease != null || _availableVersionFromHttp != null;

        /// <summary>
        /// Gets whether the update has been downloaded and is ready to apply.
        /// </summary>
        public bool IsUpdateDownloaded => _updateDownloaded;

        /// <summary>
        /// Gets the version of the available update, if any.
        /// </summary>
        public string? AvailableVersion => _updateInfo?.TargetFullRelease?.Version?.ToString() ?? _availableVersionFromHttp;

        /// <summary>
        /// Gets the current installed version (from assembly, since add-in is manually installed).
        /// </summary>
        public string CurrentVersion => GetAssemblyVersion();

        /// <summary>
        /// Gets the last error message if update check failed.
        /// </summary>
        public string? LastError => _lastError;

        /// <summary>
        /// Gets the URL for downloading the latest release.
        /// </summary>
        public string DownloadUrl => $"{_githubRepoUrl}/releases/latest";

        /// <summary>
        /// Initializes a new instance of the VelopackUpdateService.
        /// </summary>
        /// <param name="githubRepoUrl">GitHub repository URL (e.g., "https://github.com/owner/repo")</param>
        /// <param name="accessToken">Optional GitHub Personal Access Token for private repos.</param>
        public VelopackUpdateService(string githubRepoUrl, string? accessToken = null)
        {
            _githubRepoUrl = githubRepoUrl;
            _updateDownloaded = false;
            _lastError = null;

            try
            {
                // Use GitHub as the update source - Velopack will look for releases
                // For private repos, an access token is required
                var source = new GithubSource(_githubRepoUrl, accessToken, prerelease: false);
                _updateManager = new UpdateManager(source);
	                LogDebug($"UpdateManager initialized. Current version (assembly): {CurrentVersion}");

                // Note: For manually-installed Revit add-ins, Velopack can only CHECK for updates.
                // The UpdateManager.CurrentVersion will be null since the app wasn't installed via Velopack.
                // We use the assembly version instead and compare it against available releases.
            }
            catch (Exception ex)
            {
                // If Velopack isn't properly installed (e.g., running in debug),
                // the UpdateManager may fail. We handle this gracefully.
	                _lastError = $"Init failed: {ex.Message}";
	                FileLogger.LogException("VelopackUpdateService", "Constructor", ex);
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

	        private static void LogDebug(string message)
	        {
	            FileLogger.Log(LogLevel.Debug, "VelopackUpdateService", message);
	        }

        /// <summary>
        /// Checks for available updates asynchronously.
        /// For manually-installed Revit add-ins, this compares assembly version against GitHub releases.
        /// </summary>
        /// <returns>True if an update is available, false otherwise.</returns>
        public async Task<bool> CheckForUpdatesAsync()
        {
            try
            {
                LogDebug($"Checking for updates at: {_githubRepoUrl}");
                LogDebug($"Current version (assembly): {CurrentVersion}");

                // Try Velopack's built-in check first (works if app was installed via Velopack)
                if (_updateManager != null)
                {
                    try
                    {
                        _updateInfo = await _updateManager.CheckForUpdatesAsync();

                        if (_updateInfo?.TargetFullRelease != null)
                        {
                            var latestVersion = _updateInfo.TargetFullRelease.Version.ToString();
                            LogDebug($"Velopack found update: {latestVersion}");

                            // Manual version comparison since Velopack may not know our current version
                            if (IsNewerVersion(latestVersion, CurrentVersion))
                            {
                                LogDebug($"Update available: {latestVersion} > {CurrentVersion}");
                                _lastError = null;
                                return true;
                            }
                            else
                            {
                                LogDebug($"Already on latest version: {CurrentVersion} >= {latestVersion}");
                                _lastError = null;
                                return false;
                            }
                        }
                    }
                    catch (Exception veloEx)
                    {
	                        _lastError = $"Velopack check failed: {veloEx.Message}";
	                        FileLogger.LogException("VelopackUpdateService", "CheckForUpdatesAsync_Velopack", veloEx);
                        // Fall through to HTTP-based check
                    }
                }

                // Fallback: HTTP-based version check using GitHub API
                LogDebug("Falling back to HTTP-based version check...");
                return await CheckForUpdatesViaHttpAsync();
            }
            catch (Exception ex)
            {
                _lastError = $"Update check failed: {ex.Message}";
	                FileLogger.LogException("VelopackUpdateService", "CheckForUpdatesAsync", ex);
                _updateInfo = null;
                return false;
            }
        }

        /// <summary>
        /// Checks for updates by directly querying GitHub releases API.
        /// This works even when Velopack isn't installed.
        /// </summary>
        private async Task<bool> CheckForUpdatesViaHttpAsync()
        {
            try
            {
                // Extract owner/repo from URL
                var uri = new Uri(_githubRepoUrl);
                var pathParts = uri.AbsolutePath.Trim('/').Split('/');
                if (pathParts.Length < 2)
                {
                    _lastError = "Invalid GitHub URL format";
                    return false;
                }

                var apiUrl = $"https://api.github.com/repos/{pathParts[0]}/{pathParts[1]}/releases/latest";
                LogDebug($"Fetching: {apiUrl}");

                string response;
#if NET8_0_OR_GREATER
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "ModelInfoUpdater");
                response = await httpClient.GetStringAsync(apiUrl);
#else
                // Use WebClient for .NET Framework
                using (var webClient = new System.Net.WebClient())
                {
                    webClient.Headers.Add("User-Agent", "ModelInfoUpdater");
                    response = await Task.Run(() => webClient.DownloadString(apiUrl));
                }
#endif

                // Simple JSON parsing for tag_name
                var tagMatch = System.Text.RegularExpressions.Regex.Match(response, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                if (tagMatch.Success)
                {
                    var latestTag = tagMatch.Groups[1].Value;
                    var latestVersion = latestTag.TrimStart('v', 'V');
                    _availableVersionFromHttp = latestVersion;

                    LogDebug($"Latest release tag: {latestTag} (version: {latestVersion})");

                    if (IsNewerVersion(latestVersion, CurrentVersion))
                    {
                        LogDebug($"Update available via HTTP check: {latestVersion} > {CurrentVersion}");
                        _lastError = null;
                        return true;
                    }
                    else
                    {
                        LogDebug($"Already on latest version: {CurrentVersion} >= {latestVersion}");
                        _lastError = null;
                        return false;
                    }
                }

                _lastError = "Could not parse GitHub release response";
                return false;
            }
            catch (Exception ex)
            {
                _lastError = $"HTTP update check failed: {ex.Message}";
	                FileLogger.LogException("VelopackUpdateService", "CheckForUpdatesViaHttpAsync", ex);
                return false;
            }
        }

        private string? _availableVersionFromHttp;

        /// <summary>
        /// Compares two version strings. Returns true if newVersion is greater than currentVersion.
        /// </summary>
        private static bool IsNewerVersion(string newVersion, string currentVersion)
        {
            try
            {
                // Clean up versions
                newVersion = newVersion.TrimStart('v', 'V');
                currentVersion = currentVersion.TrimStart('v', 'V');

                var newVer = new Version(newVersion);
                var curVer = new Version(currentVersion);

                return newVer > curVer;
            }
            catch
            {
                // If parsing fails, do string comparison
                return string.Compare(newVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
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
	                FileLogger.LogException("VelopackUpdateService", "DownloadUpdateAsync", ex);
                _updateDownloaded = false;
                return false;
            }
        }

        /// <summary>
        /// Finds the Launcher executable.
        /// Search order:
        ///  1. Velopack install directory under %LocalAppData%\ModelInfoUpdater
        ///  2. Revit add-ins folders under %AppData%\Autodesk\Revit\Addins\<year>
        ///  3. Next to the executing assembly (development/manual install)
        /// </summary>
        private static string? FindLauncherPath()
        {
	            // 1. Primary location: Velopack install directory
	            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
	            string velopackDir = Path.Combine(localAppData, "ModelInfoUpdater");
	            LogDebug($"Searching for launcher. LocalAppData='{localAppData}', VelopackDir='{velopackDir}'");

	            // Velopack creates current\ symlink or we check app-* folders
	            string currentDir = Path.Combine(velopackDir, "current");
	            if (Directory.Exists(currentDir))
	            {
	                string launcherPath = Path.Combine(currentDir, "ModelInfoUpdater.Updater.exe");
	                if (File.Exists(launcherPath))
	                {
	                    LogDebug($"Launcher found in Velopack 'current' directory: {launcherPath}");
	                    return launcherPath;
	                }
	                else
	                {
	                    LogDebug($"No launcher in Velopack 'current' directory: {currentDir}");
	                }
	            }
	            else
	            {
	                LogDebug($"Velopack 'current' directory does not exist: {currentDir}");
	            }

	            // Fallback: Find latest app-* folder
	            if (Directory.Exists(velopackDir))
	            {
	                var appDirs = Directory.GetDirectories(velopackDir, "app-*");
	                if (appDirs.Length == 0)
	                {
	                    LogDebug("No Velopack app-* directories found.");
	                }
	                foreach (var dir in appDirs.OrderByDescending(d => d))
	                {
	                    string launcherPath = Path.Combine(dir, "ModelInfoUpdater.Updater.exe");
	                    if (File.Exists(launcherPath))
	                    {
	                        LogDebug($"Launcher found in Velopack app-* directory: {launcherPath}");
	                        return launcherPath;
	                    }
	                    else
	                    {
	                        LogDebug($"No launcher in Velopack app-* directory: {dir}");
	                    }
	                }
	            }
	            else
	            {
	                LogDebug($"Velopack directory does not exist: {velopackDir}");
	            }

	            // 2. Revit add-ins directories under %AppData%\\Autodesk\\Revit\\Addins\\<year>
	            string userAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
	            string revitRoot = Path.Combine(userAppData, "Autodesk", "Revit", "Addins");
	            if (Directory.Exists(revitRoot))
	            {
	                foreach (var year in new[] { "2026", "2025", "2024" })
	                {
	                    string addinDir = Path.Combine(revitRoot, year);
	                    string launcherPath = Path.Combine(addinDir, "ModelInfoUpdater.Updater.exe");
	                    if (File.Exists(launcherPath))
	                    {
	                        LogDebug($"Launcher found in Revit add-ins folder for {year}: {launcherPath}");
	                        return launcherPath;
	                    }
	                    else
	                    {
	                        LogDebug($"No launcher in Revit add-ins folder for {year}: {addinDir}");
	                    }
	                }
	            }
	            else
	            {
	                LogDebug($"Revit add-ins root directory does not exist: {revitRoot}");
	            }

	            // 3. Fallback: Next to add-in DLL (for development/manual install)
	            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
	            string localLauncher = Path.Combine(assemblyDir, "ModelInfoUpdater.Updater.exe");
	            if (File.Exists(localLauncher))
	            {
	                LogDebug($"Launcher found next to executing assembly: {localLauncher}");
	                return localLauncher;
	            }
	            else
	            {
	                LogDebug($"No launcher next to executing assembly. AssemblyDir='{assemblyDir}'");
	            }

	            LogDebug("Launcher not found in any known location.");
	            return null;
        }

        /// <summary>
        /// Gets whether the Launcher is installed (Velopack installation exists).
        /// </summary>
        public bool IsLauncherInstalled => FindLauncherPath() != null;

        /// <summary>
        /// Launches the updater executable to check for and apply updates.
        /// The Launcher downloads the update, then deploys files to Revit add-in folder.
        /// </summary>
        /// <param name="silent">If true, updater runs without console output.</param>
        /// <param name="revitPid">If provided, updater will wait for this Revit process to close.</param>
        /// <param name="revitExePath">If provided, updater will restart Revit at this path after update.</param>
        /// <returns>True if launcher was started successfully.</returns>
        public bool LaunchUpdater(bool silent = false, int? revitPid = null, string? revitExePath = null)
        {
            try
            {
                string? launcherPath = FindLauncherPath();

                if (launcherPath == null)
                {
	                    _lastError =
	                        "ModelInfoUpdater.Updater.exe could not be found. " +
	                        "Please run the installer or ensure the updater exists in the Velopack folder or Revit Addins folder.";
	                    FileLogger.Log(LogLevel.Error, "VelopackUpdateService", _lastError);
                    return false;
                }

                // Build arguments
                string args = "--update";
                if (silent) args += " --silent";

                // Add Revit restart parameters if provided
                if (revitPid.HasValue)
                {
                    args += $" --revit-pid {revitPid.Value}";
                }
                if (!string.IsNullOrEmpty(revitExePath))
                {
                    args += $" --revit-exe \"{revitExePath}\"";
                }

                // Launch the updater
                var startInfo = new ProcessStartInfo
                {
                    FileName = launcherPath,
                    Arguments = args,
                    UseShellExecute = true, // Show console window
                    WindowStyle = silent ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
                };

	                LogDebug($"Starting updater process. FileName='{startInfo.FileName}', Arguments='{startInfo.Arguments}', Silent={silent}");
	                Process.Start(startInfo);
	                LogDebug("Updater process was started successfully.");
                return true;
            }
            catch (Exception ex)
            {
                _lastError = $"Failed to launch updater: {ex.Message}";
	                FileLogger.LogException("VelopackUpdateService", "LaunchUpdater", ex);
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

