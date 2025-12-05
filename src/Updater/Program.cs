using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace ModelInfoUpdater.Updater;

/// <summary>
/// Launcher/Updater for ModelInfoUpdater Revit add-in.
///
/// ARCHITECTURE:
/// - Velopack manages this Launcher in %LocalAppData%\ModelInfoUpdater\
/// - This Launcher deploys the add-in to Revit's add-in folders
/// - Revit add-in triggers this Launcher with --update when user wants to update
///
/// Usage:
///   ModelInfoUpdater.Updater.exe                    # Deploy only (first install)
///   ModelInfoUpdater.Updater.exe --update           # Check for updates, download, deploy
///   ModelInfoUpdater.Updater.exe --silent           # Silent mode (no console output)
/// </summary>
class Program
{
    private const string GitHubRepoUrl = "https://github.com/givenxmodena/ModelInfoUpdater";
    private const string AppName = "ModelInfoUpdater";

    // Revit versions to deploy to
    private static readonly string[] RevitVersions = { "2024", "2025", "2026" };

    static async Task<int> Main(string[] args)
    {
        bool silent = args.Contains("--silent");
        bool updateMode = args.Contains("--update");

        try
        {
            // Initialize Velopack - handles pending updates from previous runs
            VelopackApp.Build().Run();

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

            LogMessage("Deployment complete! Restart Revit to use the updated add-in.", silent);
            return 0;
        }
        catch (Exception ex)
        {
            LogMessage($"Error: {ex.Message}", silent);
            if (!silent)
            {
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
            return 1;
        }
    }

    /// <summary>
    /// Deploys the add-in files to all Revit add-in folders.
    /// </summary>
    private static void DeployToRevit(bool silent)
    {
        LogMessage("Deploying add-in to Revit...", silent);

        string appDir = AppContext.BaseDirectory;
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        // Files to copy to Revit
        string[] filesToCopy = {
            "ModelInfoUpdater.dll",
            "Velopack.dll",
            "NuGet.Versioning.dll"
        };

        foreach (var revitVersion in RevitVersions)
        {
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

                // Copy DLLs
                foreach (var file in filesToCopy)
                {
                    string source = Path.Combine(appDir, file);
                    string dest = Path.Combine(addinDir, file);

                    if (File.Exists(source))
                    {
                        File.Copy(source, dest, overwrite: true);
                        LogMessage($"  Copied {file} to Revit {revitVersion}", silent);
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

                LogMessage($"Deployed to Revit {revitVersion}", silent);
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to deploy to Revit {revitVersion}: {ex.Message}", silent);
            }
        }
    }

    /// <summary>
    /// Checks for updates and applies them if available.
    /// </summary>
    private static async Task<bool> CheckAndApplyUpdatesAsync(bool silent)
    {
        try
        {
            LogMessage("Checking for updates...", silent);

            var source = new GithubSource(GitHubRepoUrl, null, false);
            var updateManager = new UpdateManager(source);

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
                if (!silent && p % 20 == 0)
                {
                    Console.WriteLine($"  Download: {p}%");
                }
            });

            // Apply the update and restart (Velopack handles this)
            LogMessage("Applying update...", silent);
            updateManager.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease);

            // Won't reach here - ApplyUpdatesAndRestart exits
            return true;
        }
        catch (Exception ex)
        {
            LogMessage($"Update failed: {ex.Message}", silent);
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

