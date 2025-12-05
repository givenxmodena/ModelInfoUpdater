using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using ModelInfoUpdater.Services;

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
                System.Diagnostics.Debug.WriteLine($"[ModelInfoUpdater] Initializing update service for: {GitHubRepoUrl}");

                UpdateService = new VelopackUpdateService(GitHubRepoUrl);

                System.Diagnostics.Debug.WriteLine($"[ModelInfoUpdater] Current version: {UpdateService.CurrentVersion}");

                // Check for updates in the background
                bool updateAvailable = await UpdateService.CheckForUpdatesAsync();

                if (updateAvailable)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ModelInfoUpdater] Update available: v{UpdateService.AvailableVersion}");
                }
                else
                {
                    var lastError = (UpdateService as VelopackUpdateService)?.LastError;
                    if (!string.IsNullOrEmpty(lastError))
                    {
                        System.Diagnostics.Debug.WriteLine($"[ModelInfoUpdater] Update check issue: {lastError}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[ModelInfoUpdater] No updates available - already on latest version.");
                    }
                }
            }
            catch (Exception ex)
            {
                // Update check failed - don't disrupt the user
                System.Diagnostics.Debug.WriteLine(
                    $"[ModelInfoUpdater] Update check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows an update notification dialog if an update is available.
        /// Called from Command.Execute() to notify user at appropriate time.
        /// </summary>
        /// <returns>True if user chose to apply update, false otherwise.</returns>
        public static async Task<bool> ShowUpdateNotificationIfAvailableAsync()
        {
            // Debug: Show update check status
            System.Diagnostics.Debug.WriteLine($"[ModelInfoUpdater] ShowUpdateNotificationIfAvailableAsync called");
            System.Diagnostics.Debug.WriteLine($"[ModelInfoUpdater] UpdateService: {(UpdateService == null ? "null" : "initialized")}");

            if (UpdateService != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ModelInfoUpdater] IsUpdateAvailable: {UpdateService.IsUpdateAvailable}");
                System.Diagnostics.Debug.WriteLine($"[ModelInfoUpdater] CurrentVersion: {UpdateService.CurrentVersion}");
                System.Diagnostics.Debug.WriteLine($"[ModelInfoUpdater] AvailableVersion: {UpdateService.AvailableVersion ?? "none"}");
                System.Diagnostics.Debug.WriteLine($"[ModelInfoUpdater] LastError: {UpdateService.LastError ?? "none"}");
                System.Diagnostics.Debug.WriteLine($"[ModelInfoUpdater] IsLauncherInstalled: {UpdateService.IsLauncherInstalled}");
            }

            System.Diagnostics.Debug.WriteLine($"[ModelInfoUpdater] _updateNotificationShown: {_updateNotificationShown}");

            if (UpdateService == null || !UpdateService.IsUpdateAvailable || _updateNotificationShown)
                return false;

            _updateNotificationShown = true;

            // Check if Launcher is installed (Velopack installation)
            if (UpdateService.IsLauncherInstalled)
            {
                // Launcher found - use automatic update flow
                var dialog = new TaskDialog("Update Available")
                {
                    MainInstruction = $"A new version is available!",
                    MainContent = $"Current version: {UpdateService.CurrentVersion}\n" +
                                  $"New version: {UpdateService.AvailableVersion}\n\n" +
                                  "Would you like to update now?\n\n" +
                                  "Note: Close Revit after the update completes to apply changes.",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.Yes
                };

                if (dialog.Show() == TaskDialogResult.Yes)
                {
                    // Launch the updater with --update flag
                    if (UpdateService.LaunchUpdater(silent: false))
                    {
                        TaskDialog.Show("Update Started",
                            "The updater is running. Please close Revit when prompted to complete the update.");
                        return true;
                    }
                    else
                    {
                        TaskDialog.Show("Error",
                            UpdateService.LastError ?? "Failed to start the updater.");
                    }
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
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = downloadUrl,
                            UseShellExecute = true
                        });
                        return true;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ModelInfoUpdater] Failed to open browser: {ex.Message}");
                        TaskDialog.Show("Error",
                            $"Could not open browser. Please visit:\n{GitHubRepoUrl}/releases/latest");
                    }
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
            // Clean up update service reference
            UpdateService = null;
            _updateNotificationShown = false;
            return Result.Succeeded;
        }
    }
}

