using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using System.Threading;
using Velopack;
using Velopack.Sources;
using ModelInfoUpdater.Logging;

namespace ModelInfoUpdater.Updater;

/// <summary>
/// Launcher/Updater for ModelInfoUpdater Revit add-in.
///
/// ARCHITECTURE:
/// - Velopack manages this Launcher in %LocalAppData%\ModelInfoUpdater\
/// - This Launcher deploys the add-in to Revit's add-in folders
/// - Revit add-in triggers this Launcher with --update when user wants to update
///
/// MULTI-TARGETING:
/// - Revit 2026: Uses .NET 8.0 (deploy from net8.0-windows folder)
/// - Revit 2024/2025: Uses .NET Framework 4.8 (deploy from net48 folder)
///
/// Usage:
///   ModelInfoUpdater.Updater.exe                              # Deploy only (first install)
///   ModelInfoUpdater.Updater.exe --update                     # Check for updates, download, deploy
///   ModelInfoUpdater.Updater.exe --update --revit-pid 1234 --revit-exe "C:\...\Revit.exe"
///                                                             # Update with auto-restart Revit
///   ModelInfoUpdater.Updater.exe --silent                     # Silent mode (no console output)
/// </summary>
	class Program
	{
	    private const string GitHubRepoUrl = "https://github.com/givenxmodena/ModelInfoUpdater";
	    private const string AppName = "ModelInfoUpdater";
	
	    // Revit version to .NET framework mapping
	    private static readonly Dictionary<string, string> RevitFrameworkMap = new()
	    {
	        { "2024", "net48" },
	        { "2025", "net48" },
	        { "2026", "net8.0-windows" }
	    };

	    /// <summary>
	    /// Entry point. In silent mode we run the update flow headlessly; otherwise
	    /// we spin up a WPF window that shows status and progress instead of a console.
	    /// </summary>
	    [STAThread]
	    public static int Main(string[] args)
	    {
	        bool silent = args.Contains("--silent");
	        bool updateMode = args.Contains("--update");
	
	        // Parse Revit auto-restart parameters
	        int? revitPid = GetArgInt(args, "--revit-pid");
	        string? revitExePath = GetArgString(args, "--revit-exe");
	
	        // Initialize centralized file logging
	        string argsJoined = string.Join(" ", args ?? Array.Empty<string>());
	        string assemblyVersion = GetAssemblyVersion();
	        FileLogger.Initialize("ModelInfoUpdater.Updater");
	        FileLogger.LogEnvironment("UpdaterStartup", assemblyVersion,
	            $"Args=\"{argsJoined}\", RevitPid={revitPid?.ToString() ?? "null"}, RevitExePath=\"{revitExePath ?? string.Empty}\"");
	        FileLogger.Log(LogLevel.Info, "UpdaterStartup",
	            $"Starting updater. updateMode={updateMode}, silent={silent}");
	
	        try
	        {
	            // Initialize Velopack - handles pending updates from previous runs
	            VelopackApp.Build().Run();
	
	            if (silent)
	            {
	                // Headless flow used when launched with --silent
	                return RunSilentAsync(updateMode, revitPid, revitExePath).GetAwaiter().GetResult();
	            }
	
	            // GUI flow: show WPF window that mirrors the main add-in look & feel
	            var app = new System.Windows.Application();
	            var window = new UpdateWindow(updateMode, revitPid, revitExePath);
	            app.Run(window);
	
	            return window.ExitCode;
	        }
	        catch (Exception ex)
	        {
	            FileLogger.LogException("UpdaterMain", "Main", ex);
	            LogMessage($"Error: {ex.Message}", silent: true, LogLevel.Error);
	            return 1;
	        }
	    }

	    /// <summary>
	    /// Original console-based flow, now used only for fully silent runs.
	    /// </summary>
	    internal static async Task<int> RunSilentAsync(bool updateMode, int? revitPid, string? revitExePath)
	    {
	        const bool silent = true;
	
	        try
	        {
	            // If we have Revit restart parameters, wait for Revit to close first
	            if (revitPid.HasValue)
	            {
	                await WaitForRevitToClose(revitPid.Value, silent);
	            }
	
	            if (updateMode)
	            {
	                // Check for updates and apply if available
	                var updated = await CheckAndApplyUpdatesAsync(silent);
	
	                if (updated)
	                {
	                    // Velopack will restart the app after update
	                    // DeployToRevit will be called on restart
	                    return 0;
	                }
	                else
	                {
	                    LogMessage("No updates available. You're on the latest version.", silent);
	                }
	            }
	
	            // Always deploy to Revit (ensures files are in place)
	            DeployToRevit(silent);
	
	            LogMessage("Deployment complete!", silent);
	
	            // Restart Revit if we have the path
	            if (!string.IsNullOrEmpty(revitExePath))
	            {
	                RestartRevit(revitExePath, silent);
	            }
	            else
	            {
	                LogMessage("Restart Revit to use the updated add-in.", silent);
	            }
	
	            return 0;
	        }
	        catch (Exception ex)
	        {
	            FileLogger.LogException("UpdaterMain", "RunSilentAsync", ex);
	            LogMessage($"Error: {ex.Message}", silent, LogLevel.Error);
	            return 1;
	        }
	    }

	    private static string GetAssemblyVersion()
	    {
	        try
	        {
	            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
	        }
	        catch
	        {
	            return "1.0.0.0";
	        }
	    }

    /// <summary>
    /// Parses a command line argument value as string.
    /// </summary>
    private static string? GetArgString(string[] args, string argName)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(argName, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }

    /// <summary>
    /// Parses a command line argument value as int.
    /// </summary>
    private static int? GetArgInt(string[] args, string argName)
    {
        var value = GetArgString(args, argName);
        if (value != null && int.TryParse(value, out int result))
        {
            return result;
        }
        return null;
    }

	    /// <summary>
	    /// Waits for the Revit process to close (max 60 seconds).
	    /// </summary>
	    internal static async Task WaitForRevitToClose(int revitPid, bool silent)
    {
        LogMessage($"Waiting for Revit (PID: {revitPid}) to close...", silent);

        try
        {
            var revitProcess = Process.GetProcessById(revitPid);

            // Wait up to 60 seconds for Revit to close
            int waited = 0;
            while (!revitProcess.HasExited && waited < 60000)
            {
                await Task.Delay(1000);
                waited += 1000;

                if (waited % 10000 == 0)
                {
                    LogMessage($"Still waiting for Revit to close... ({waited / 1000}s)", silent);
                }
            }

            if (!revitProcess.HasExited)
            {
                LogMessage("Revit didn't close in time. Attempting to continue anyway...", silent);
            }
            else
            {
                LogMessage("Revit has closed.", silent);
            }
        }
        catch (ArgumentException)
        {
            // Process already exited
            LogMessage("Revit has already closed.", silent);
        }

        // Give a moment for file handles to be released
        await Task.Delay(2000);
    }

	    /// <summary>
	    /// Restarts Revit after the update is deployed.
	    /// </summary>
	    internal static void RestartRevit(string revitExePath, bool silent)
    {
        try
        {
            LogMessage($"Restarting Revit...", silent);

            Process.Start(new ProcessStartInfo
            {
                FileName = revitExePath,
                UseShellExecute = true
            });

            LogMessage("Revit has been restarted. The update is complete!", silent);
        }
        catch (Exception ex)
        {
	            FileLogger.LogException("Updater", "RestartRevit", ex);
	            LogMessage($"Failed to restart Revit: {ex.Message}", silent, LogLevel.Error);
            LogMessage("Please start Revit manually to use the updated add-in.", silent);
        }
    }

	    /// <summary>
	    /// Deploys the add-in files to all Revit add-in folders.
	    /// Uses the correct .NET framework build for each Revit version.
	    /// </summary>
	    internal static void DeployToRevit(bool silent)
    {
        LogMessage("Deploying add-in to Revit...", silent);

        string appDir = AppContext.BaseDirectory;
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        // Files to copy for .NET 8.0 (Revit 2026)
        // Includes deps.json for runtime dependency resolution
        string[] net8Files = {
            "ModelInfoUpdater.dll",
            "ModelInfoUpdater.deps.json",
            "Velopack.dll",
            "NuGet.Versioning.dll"
        };

        // Files to copy for .NET Framework 4.8 (Revit 2024/2025)
        // Note: Velopack.dll not needed because Revit add-ins don't do update checking -
        // the Launcher handles that. The Revit add-in just needs ModelInfoUpdater.dll.
        string[] net48Files = {
            "ModelInfoUpdater.dll"
        };

        foreach (var kvp in RevitFrameworkMap)
        {
            string revitVersion = kvp.Key;
            string framework = kvp.Value;

            try
            {
                string revitAddinRoot = Path.Combine(programData, "Autodesk", "Revit", "Addins", revitVersion);

                // Skip if Revit version folder doesn't exist (Revit not installed)
                if (!Directory.Exists(revitAddinRoot))
                {
                    LogMessage($"Skipping Revit {revitVersion} (not installed)", silent);
                    continue;
                }

                string addinDir = Path.Combine(revitAddinRoot, AppName);
                Directory.CreateDirectory(addinDir);

                // Determine source directory based on framework
                // The Velopack package contains framework-specific folders
                string sourceDir = Path.Combine(appDir, framework);

                // Fallback to appDir if framework subfolder doesn't exist (single-framework package)
                if (!Directory.Exists(sourceDir))
                {
                    sourceDir = appDir;
                    LogMessage($"  Using root directory for Revit {revitVersion} (no {framework} subfolder)", silent);
                }

                // Select appropriate files based on framework
                string[] filesToCopy = framework == "net8.0-windows" ? net8Files : net48Files;

                // Copy DLLs with retry logic for locked files
                foreach (var file in filesToCopy)
                {
                    string source = Path.Combine(sourceDir, file);
                    string dest = Path.Combine(addinDir, file);

                    if (File.Exists(source))
                    {
                        CopyFileWithRetry(source, dest, silent, revitVersion, framework);
                        LogMessage($"  Copied {file} to Revit {revitVersion} ({framework})", silent);
                    }
                    else
                    {
                        LogMessage($"  Warning: {file} not found in {sourceDir}", silent);
                    }
                }

                // Generate .addin file from template
                string templatePath = Path.Combine(appDir, "ModelInfoUpdater.addin.template");
                if (File.Exists(templatePath))
                {
                    string template = File.ReadAllText(templatePath);
                    string addinContent = template.Replace("{APPDIR}", addinDir);
                    string addinPath = Path.Combine(revitAddinRoot, "ModelInfoUpdater.addin");
                    File.WriteAllText(addinPath, addinContent);
                    LogMessage($"  Generated .addin file for Revit {revitVersion}", silent);
                }

                LogMessage($"Deployed to Revit {revitVersion} ({framework})", silent);
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to deploy to Revit {revitVersion}: {ex.Message}", silent);
	                FileLogger.LogException("Updater", $"DeployToRevit_{revitVersion}", ex);
            }
        }
    }

	    /// <summary>
	    /// Checks for updates and applies them if available.
	    /// Optional callbacks allow the caller (e.g. WPF UI) to receive
	    /// human-readable status messages and download progress.
	    /// </summary>
	    internal static async Task<bool> CheckAndApplyUpdatesAsync(
	        bool silent,
	        Action<string>? statusCallback = null,
	        Action<int>? progressCallback = null)
	    {
	        try
	        {
	            statusCallback?.Invoke("Checking for updates...");
	            LogMessage("Checking for updates...", silent);
	
	            var source = new GithubSource(GitHubRepoUrl, null, false);
	            var updateManager = new UpdateManager(source);
	
	            var updateInfo = await updateManager.CheckForUpdatesAsync();
	
	            if (updateInfo?.TargetFullRelease == null)
	            {
	                return false;
	            }
	
	            string foundMsg = $"Found update: v{updateInfo.TargetFullRelease.Version}";
	            statusCallback?.Invoke(foundMsg);
	            LogMessage(foundMsg, silent);
	
	            // Download the update
	            statusCallback?.Invoke("Downloading update...");
	            LogMessage("Downloading update...", silent);
	
	            await updateManager.DownloadUpdatesAsync(updateInfo, p =>
	            {
	                progressCallback?.Invoke(p);
	
	                if (!silent && p % 20 == 0)
	                {
	                    Console.WriteLine($"  Download: {p}%");
	                }
	            });
	
	            // Apply the update and restart (Velopack handles this)
	            statusCallback?.Invoke("Installing update...");
	            LogMessage("Applying update...", silent);
	            updateManager.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease);
	
	            // Won't reach here - ApplyUpdatesAndRestart exits
	            return true;
	        }
	        catch (Exception ex)
	        {
	            FileLogger.LogException("Updater", "CheckAndApplyUpdatesAsync", ex);
	            LogMessage($"Update failed: {ex.Message}", silent, LogLevel.Error);
	            throw;
	        }
	    }

	    private static void LogMessage(string message, bool silent, LogLevel level = LogLevel.Info, string category = "Updater")
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logLine = $"[{timestamp}] {message}";

        if (!silent)
        {
            Console.WriteLine(logLine);
        }
	
	        // Persist to centralized log file and Debug output
	        FileLogger.Log(level, category, message);
    }

    /// <summary>
    /// Copies a file with retry logic to handle locked files (e.g., when Revit is still running).
    /// Waits up to 30 seconds for the file to become available.
    /// </summary>
    private static void CopyFileWithRetry(string source, string dest, bool silent, string revitVersion, string framework)
    {
        const int maxRetries = 6;
        const int retryDelayMs = 5000; // 5 seconds between retries

	        for (int attempt = 1; attempt <= maxRetries; attempt++)
	        {
	            try
	            {
	                File.Copy(source, dest, overwrite: true);
	                return; // Success
	            }
	            catch (IOException) when (attempt < maxRetries)
	            {
	                // File is likely locked by Revit
	                LogMessage($"  File locked for Revit {revitVersion}. Waiting for Revit to close... (attempt {attempt}/{maxRetries})", silent);
	                Thread.Sleep(retryDelayMs);
	            }
	        }

        // Final attempt - let exception propagate if it fails
        File.Copy(source, dest, overwrite: true);
    }
}

