using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace ModelInfoUpdater.Updater;

/// <summary>
/// Standalone updater executable for ModelInfoUpdater Revit add-in.
///
/// This application:
/// 1. Is launched by the add-in when an update is downloaded and user confirms
/// 2. Waits for all Revit processes to close (since the DLL is locked)
/// 3. Applies the pending update using Velopack
/// 4. Exits (optionally notifying user of success)
///
/// Usage: ModelInfoUpdater.Updater.exe [--silent] [--wait-pid=1234]
/// </summary>
class Program
{
    private const string GitHubRepoUrl = "https://github.com/givenxmodena/ModelInfoUpdater";
    private const int MaxWaitTimeMinutes = 30;

    static async Task<int> Main(string[] args)
    {
        bool silent = args.Contains("--silent");
        int? waitForPid = null;

        // Parse --wait-pid argument
        var pidArg = args.FirstOrDefault(a => a.StartsWith("--wait-pid="));
        if (pidArg != null && int.TryParse(pidArg.Split('=')[1], out int pid))
        {
            waitForPid = pid;
        }

        try
        {
            // Initialize Velopack - this handles pending updates from previous runs
            VelopackApp.Build().Run();

            // Wait for Revit to close
            await WaitForRevitToCloseAsync(waitForPid, silent);

            // Check for and apply updates
            var updateApplied = await CheckAndApplyUpdatesAsync(silent);

            if (updateApplied)
            {
                LogMessage("Update applied successfully! The new version will be loaded next time Revit starts.", silent);
                return 0;
            }
            else
            {
                LogMessage("No pending updates to apply.", silent);
                return 0;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Update failed: {ex.Message}", silent);
            return 1;
        }
    }

    /// <summary>
    /// Waits for all Revit processes to close, or a specific process if PID is provided.
    /// </summary>
    private static async Task WaitForRevitToCloseAsync(int? specificPid, bool silent)
    {
        var startTime = DateTime.Now;
        var maxWait = TimeSpan.FromMinutes(MaxWaitTimeMinutes);

        LogMessage("Waiting for Revit to close...", silent);

        while (DateTime.Now - startTime < maxWait)
        {
            bool revitRunning = false;

            if (specificPid.HasValue)
            {
                // Wait for specific process
                try
                {
                    var process = Process.GetProcessById(specificPid.Value);
                    revitRunning = !process.HasExited;
                }
                catch (ArgumentException)
                {
                    // Process no longer exists
                    revitRunning = false;
                }
            }
            else
            {
                // Wait for any Revit process
                var revitProcesses = Process.GetProcessesByName("Revit");
                revitRunning = revitProcesses.Length > 0;
            }

            if (!revitRunning)
            {
                LogMessage("Revit has closed. Applying update...", silent);
                // Give a moment for file handles to be released
                await Task.Delay(2000);
                return;
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"Timed out waiting for Revit to close after {MaxWaitTimeMinutes} minutes.");
    }

    /// <summary>
    /// Checks for updates and applies them if available.
    /// </summary>
    private static async Task<bool> CheckAndApplyUpdatesAsync(bool silent)
    {
        try
        {
            var source = new GithubSource(GitHubRepoUrl, null, false);
            var updateManager = new UpdateManager(source);

            // Check for updates
            var updateInfo = await updateManager.CheckForUpdatesAsync();

            if (updateInfo?.TargetFullRelease == null)
            {
                return false;
            }

            LogMessage($"Found update: v{updateInfo.TargetFullRelease.Version}", silent);

            // Download the update
            LogMessage("Downloading update...", silent);
            await updateManager.DownloadUpdatesAsync(updateInfo, p =>
            {
                if (!silent && p % 10 == 0)
                {
                    Console.WriteLine($"Download progress: {p}%");
                }
            });

            // Apply the update (this will replace files and exit)
            LogMessage("Applying update...", silent);
            updateManager.ApplyUpdatesAndExit(updateInfo.TargetFullRelease);

            // This line shouldn't be reached as ApplyUpdatesAndExit exits the process
            return true;
        }
        catch (Exception ex)
        {
            LogMessage($"Update check/apply failed: {ex.Message}", silent);
            throw;
        }
    }

    private static void LogMessage(string message, bool silent)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logLine = $"[{timestamp}] {message}";

        if (!silent)
        {
            Console.WriteLine(logLine);
        }

        // Always write to debug output
        Debug.WriteLine($"[ModelInfoUpdater.Updater] {logLine}");
    }
}

