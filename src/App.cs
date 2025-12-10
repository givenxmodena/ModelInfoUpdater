using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ModelInfoUpdater.Services;
using ModelInfoUpdater.Logging;

namespace ModelInfoUpdater
{
    /// <summary>
    /// Implements IExternalApplication to create the ribbon tab, panel, and button
    /// when Revit loads this add-in.
    ///
    /// VELOPACK UPDATE WORKFLOW:
    /// -------------------------
    /// 1. On startup, check GitHub Releases for new versions (background, non-blocking)
    /// 2. If update available, set flag for notification
    /// 3. When user opens the command, show notification with option to download
    /// 4. User downloads update (background, while Revit runs)
    /// 5. Launch updater.exe which waits for Revit to close
    /// 6. When Revit closes, updater applies the update
    /// 7. Next Revit startup loads the new version
    /// </summary>
    public class App : IExternalApplication
    {
        // Tab and panel names as constants for consistency
        private const string TabName = "TESTER";
        private const string PanelName = "Tools";

        // GitHub repository URL for Velopack updates
        private const string GitHubRepoUrl = "https://github.com/givenxmodena/ModelInfoUpdater";

        // Static reference to update service for access from other parts of the add-in
        public static IUpdateService? UpdateService { get; private set; }

        // Flag to track if user has been notified about update this session
        private static bool _updateNotificationShown;

        /// <summary>
        /// Called when Revit starts up and loads this add-in.
        /// Creates the custom ribbon tab, panel, and button.
        /// Also initializes Velopack update checking in the background.
        /// </summary>
        /// <param name="application">The Revit UI controlled application.</param>
        /// <returns>Result indicating success or failure.</returns>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Initialize shared file-based logging for the add-in
                FileLogger.Initialize("ModelInfoUpdater");
                string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
                string revitVersion = application.ControlledApplication?.VersionNumber ?? "unknown";
                string extra = $"RevitVersion={revitVersion}";
                FileLogger.LogEnvironment("AddinStartup", version, extra);

                // Initialize Velopack update service (non-blocking)
                InitializeUpdateServiceAsync();

                // Create a custom ribbon tab named "TESTER"
                application.CreateRibbonTab(TabName);

                // Create a ribbon panel within the custom tab
                RibbonPanel ribbonPanel = application.CreateRibbonPanel(TabName, PanelName);

                // Get the path to this assembly for the button data
                string assemblyPath = Assembly.GetExecutingAssembly().Location;

                // Create push button data for the Model Info Updater command
                PushButtonData buttonData = new PushButtonData(
                    name: "ModelInfoUpdater",
                    text: "Model Info\nUpdater",
                    assemblyName: assemblyPath,
                    className: "ModelInfoUpdater.Command"
                );

                // Set button tooltip
                buttonData.ToolTip = "Opens a window to view and edit Project Information parameters.";
                buttonData.LongDescription = "Use this tool to quickly update Project Name, Project Number, " +
                                             "Client Name, and Project Status in your Revit model.";

                // Try to set button icon (optional - uses placeholder if not found)
                try
                {
                    string iconPath = Path.Combine(Path.GetDirectoryName(assemblyPath) ?? "", "icon.png");
                    if (File.Exists(iconPath))
                    {
                        buttonData.LargeImage = new BitmapImage(new Uri(iconPath));
                    }
                }
                catch
                {
                    // Icon is optional - continue without it
                }

                // Add the button to the ribbon panel
                ribbonPanel.AddItem(buttonData);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                FileLogger.LogException("AddinStartup", "OnStartup", ex);
                TaskDialog.Show("ModelInfoUpdater Error",
                    $"Failed to initialize add-in: {ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Initializes the Velopack update service and checks for updates asynchronously.
        /// This runs in the background and does not block Revit startup.
        /// </summary>
        private async void InitializeUpdateServiceAsync()
        {
            try
            {
                FileLogger.Log(LogLevel.Info, "UpdateInit", $"Initializing update service for: {GitHubRepoUrl}");

                UpdateService = new VelopackUpdateService(GitHubRepoUrl);

                FileLogger.Log(LogLevel.Info, "UpdateInit", $"Current version: {UpdateService.CurrentVersion}");

                // Check for updates in the background
                bool updateAvailable = await UpdateService.CheckForUpdatesAsync();

                if (updateAvailable)
                {
                    FileLogger.Log(LogLevel.Info, "UpdateInit",
                        $"Update available: v{UpdateService.AvailableVersion}");
                }
                else
                {
                    var lastError = (UpdateService as VelopackUpdateService)?.LastError;
                    if (!string.IsNullOrEmpty(lastError))
                    {
                        FileLogger.Log(LogLevel.Warning, "UpdateInit",
                            $"Update check reported a non-fatal issue: {lastError}");
                    }
                    else
                    {
                        FileLogger.Log(LogLevel.Info, "UpdateInit",
                            "No updates available - already on latest version.");
                    }
                }
            }
            catch (Exception ex)
            {
                // Update check failed - don't disrupt the user
                FileLogger.LogException("UpdateInit", "InitializeUpdateServiceAsync", ex);
            }
        }

        /// <summary>
        /// Shows an update notification dialog if an update is available.
        /// Called from Command.Execute() to notify user at appropriate time.
        /// </summary>
        /// <param name="uiApp">The Revit UI application (needed for seamless restart).</param>
        /// <returns>True if user chose to apply update, false otherwise.</returns>
        public static async Task<bool> ShowUpdateNotificationIfAvailableAsync(UIApplication? uiApp = null)
        {
            // Debug: Show update check status
            FileLogger.Log(LogLevel.Debug, "UpdateNotification", "ShowUpdateNotificationIfAvailableAsync called");
            FileLogger.Log(LogLevel.Debug, "UpdateNotification",
                $"UpdateService: {(UpdateService == null ? "null" : "initialized")}");

            if (UpdateService != null)
            {
                FileLogger.Log(LogLevel.Debug, "UpdateNotification",
                    $"IsUpdateAvailable: {UpdateService.IsUpdateAvailable}");
                FileLogger.Log(LogLevel.Debug, "UpdateNotification",
                    $"CurrentVersion: {UpdateService.CurrentVersion}");
                FileLogger.Log(LogLevel.Debug, "UpdateNotification",
                    $"AvailableVersion: {UpdateService.AvailableVersion ?? "none"}");
                FileLogger.Log(LogLevel.Debug, "UpdateNotification",
                    $"LastError: {UpdateService.LastError ?? "none"}");
                FileLogger.Log(LogLevel.Debug, "UpdateNotification",
                    $"IsLauncherInstalled: {UpdateService.IsLauncherInstalled}");
            }

            FileLogger.Log(LogLevel.Debug, "UpdateNotification",
                $"_updateNotificationShown: {_updateNotificationShown}");

            if (UpdateService == null || !UpdateService.IsUpdateAvailable || _updateNotificationShown)
                return false;

            _updateNotificationShown = true;

            // Check if Launcher is installed (Velopack installation)
            if (UpdateService.IsLauncherInstalled)
            {
                // Check for unsaved work before offering seamless update
                bool hasUnsavedWork = HasUnsavedDocuments(uiApp);

                if (hasUnsavedWork)
                {
                    // Unsaved work - use manual close flow
                    return ShowManualUpdateDialog();
                }
                else
                {
                    // No unsaved work - offer seamless update with auto-restart
                    return ShowSeamlessUpdateDialog(uiApp);
                }
            }
            else
            {
                // No Launcher - direct user to download page
                var dialog = new TaskDialog("Update Available")
                {
                    MainInstruction = $"A new version is available!",
                    MainContent = $"Current version: {UpdateService.CurrentVersion}\n" +
                                  $"New version: {UpdateService.AvailableVersion}\n\n" +
                                  "Would you like to open the download page?",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.Yes
                };

                if (dialog.Show() == TaskDialogResult.Yes)
                {
                    try
                    {
                        var downloadUrl = $"{GitHubRepoUrl}/releases/latest";
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = downloadUrl,
                            UseShellExecute = true
                        });
                        return true;
                    }
                    catch (Exception ex)
                    {
                FileLogger.LogException("UpdateNotification", "OpenBrowserForDownload", ex);
                        TaskDialog.Show("Error",
                            $"Could not open browser. Please visit:\n{GitHubRepoUrl}/releases/latest");
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if any open documents have unsaved changes.
        /// </summary>
        private static bool HasUnsavedDocuments(UIApplication uiApp)
        {
            if (uiApp == null) return false;

            try
            {
                var app = uiApp.Application;
                foreach (Document doc in app.Documents)
                {
                    if (doc.IsModified && !doc.IsFamilyDocument)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException("UpdateNotification", "HasUnsavedDocuments", ex);
            }

            return false;
        }

        /// <summary>
        /// Shows update dialog for seamless auto-restart flow (no unsaved work).
        /// </summary>
        private static bool ShowSeamlessUpdateDialog(UIApplication uiApp)
        {
            var dialog = new TaskDialog("Update Available")
            {
                MainInstruction = "🚀 A new version is available!",
                MainContent = $"Current version: {UpdateService.CurrentVersion}\n" +
                              $"New version: {UpdateService.AvailableVersion}\n\n" +
                              "Click 'Update Now' to download the update.\n" +
                              "Revit will close and restart automatically when complete.",
                CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                DefaultButton = TaskDialogResult.Yes
            };

            // Customize button text
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Update Now",
                "Download update, close Revit, and restart with new version");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Remind Me Later",
                "Continue working, update next time");

            var result = dialog.Show();

            if (result == TaskDialogResult.CommandLink1)
            {
                // Get Revit process info for auto-restart
                int revitPid = Process.GetCurrentProcess().Id;
                string revitExePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";

                FileLogger.Log(LogLevel.Info, "UpdateSeamless",
                    $"Launching updater with Revit PID: {revitPid}, Path: {revitExePath}");

                // Launch the updater with Revit restart parameters
                if (UpdateService.LaunchUpdater(silent: false, revitPid: revitPid, revitExePath: revitExePath))
                {
                    // Request Revit to close gracefully
                    // Note: This will trigger save prompts if there's unsaved work
                    try
                    {
                        var closeCmd = RevitCommandId.LookupPostableCommandId(PostableCommand.ExitRevit);
                        uiApp.PostCommand(closeCmd);
                    }
                    catch (Exception ex)
                    {
                        FileLogger.LogException("UpdateSeamless", "PostExitRevitCommand", ex);
                        // If PostCommand fails, the updater will wait for manual close
                    }

                    return true;
                }
                else
                {
                    TaskDialog.Show("Error", UpdateService.LastError ?? "Failed to start the updater.");
                }
            }

            return false;
        }

        /// <summary>
        /// Shows update dialog for manual close flow (has unsaved work).
        /// </summary>
        private static bool ShowManualUpdateDialog()
        {
            var dialog = new TaskDialog("Update Available")
            {
                MainInstruction = "🚀 A new version is available!",
                MainContent = $"Current version: {UpdateService.CurrentVersion}\n" +
                              $"New version: {UpdateService.AvailableVersion}\n\n" +
                              "⚠️ You have unsaved work in open documents.\n\n" +
                              "Would you like to update now?\n" +
                              "Please save your work and close Revit after the download completes.",
                CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                DefaultButton = TaskDialogResult.Yes
            };

            if (dialog.Show() == TaskDialogResult.Yes)
            {
                // Launch the updater without auto-restart (user will close manually)
                if (UpdateService.LaunchUpdater(silent: false))
                {
                    FileLogger.Log(LogLevel.Info, "UpdateManual",
                        "Updater launched successfully for manual update flow.");
                    TaskDialog.Show("Update Started",
                        "The updater is downloading the new version.\n\n" +
                        "When you see 'Deployment complete!' in the updater window:\n" +
                        "1. Save your work\n" +
                        "2. Close Revit\n" +
                        "3. Revit will restart with the new version");
                    return true;
                }
                else
                {
                    FileLogger.Log(LogLevel.Error, "UpdateManual",
                        UpdateService.LastError ?? "Failed to start the updater.");
                    TaskDialog.Show("Error", UpdateService.LastError ?? "Failed to start the updater.");
                }
            }

            return false;
        }

        /// <summary>
        /// Called when Revit shuts down.
        /// Performs any necessary cleanup.
        /// </summary>
        /// <param name="application">The Revit UI controlled application.</param>
        /// <returns>Result indicating success.</returns>
        public Result OnShutdown(UIControlledApplication application)
        {
            FileLogger.Log(LogLevel.Info, "AddinShutdown", "Revit is shutting down. Cleaning up update service.");
            // Clean up update service reference
            UpdateService = null;
            _updateNotificationShown = false;
            return Result.Succeeded;
        }
    }
}

