using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace ModelInfoUpdater
{
    /// <summary>
    /// Implements IExternalApplication to create the ribbon tab, panel, and button
    /// when Revit loads this add-in.
    /// </summary>
    public class App : IExternalApplication
    {
        // Tab and panel names as constants for consistency
        private const string TabName = "TESTER";
        private const string PanelName = "Tools";

        /// <summary>
        /// Called when Revit starts up and loads this add-in.
        /// Creates the custom ribbon tab, panel, and button.
        /// </summary>
        /// <param name="application">The Revit UI controlled application.</param>
        /// <returns>Result indicating success or failure.</returns>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
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
                    // Create a simple colored icon as placeholder
                    // In production, replace with actual icon file
                    string iconPath = Path.Combine(Path.GetDirectoryName(assemblyPath), "icon.png");
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
                // Log error and return failed result
                TaskDialog.Show("ModelInfoUpdater Error", 
                    $"Failed to initialize add-in: {ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Called when Revit shuts down.
        /// Performs any necessary cleanup.
        /// </summary>
        /// <param name="application">The Revit UI controlled application.</param>
        /// <returns>Result indicating success.</returns>
        public Result OnShutdown(UIControlledApplication application)
        {
            // No cleanup needed for this add-in
            return Result.Succeeded;
        }
    }
}

