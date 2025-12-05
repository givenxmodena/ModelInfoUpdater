using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ModelInfoUpdater.UI;

namespace ModelInfoUpdater
{
    /// <summary>
    /// Implements IExternalCommand to handle the button click event.
    /// Opens the WPF window for editing project information.
    ///
    /// This class follows the Single Responsibility Principle (SRP):
    /// - It only handles the command execution and window creation
    /// - Business logic is delegated to the ViewModel and Services
    ///
    /// Note: In larger applications, consider using a DI container (e.g., Autofac)
    /// to manage dependencies. For this simple add-in, we use constructor injection
    /// manually in the MainWindow.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        /// <summary>
        /// Executes the command when the ribbon button is clicked.
        /// Creates the MainWindow with the active document.
        /// The window's code-behind handles ViewModel creation with proper dependencies.
        /// </summary>
        /// <param name="commandData">Data related to the command execution.</param>
        /// <param name="message">Message to display if command fails.</param>
        /// <param name="elements">Elements to highlight if command fails.</param>
        /// <returns>Result indicating success, failure, or cancellation.</returns>
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                // Get the active UI application and document
                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;

                // Validate document availability
                if (uiDoc == null || uiDoc.Document == null)
                {
                    TaskDialog.Show("Model Info Updater",
                        "Please open a Revit document before using this tool.");
                    return Result.Cancelled;
                }

                Document doc = uiDoc.Document;

                // Check for updates and notify user (non-blocking, runs in background)
                // Fire-and-forget - we don't want to block the UI
                _ = App.ShowUpdateNotificationIfAvailableAsync();

                // Create the WPF window - dependency injection happens in MainWindow constructor
                // MainWindow creates its ViewModel and Service internally
                MainWindow mainWindow = new MainWindow(doc);

                // Set the owner to the Revit main window for proper modal behavior
                System.Windows.Interop.WindowInteropHelper helper =
                    new System.Windows.Interop.WindowInteropHelper(mainWindow);
                helper.Owner = uiApp.MainWindowHandle;

                // Show the window as a modal dialog
                mainWindow.ShowDialog();

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = $"An error occurred: {ex.Message}";
                TaskDialog.Show("Model Info Updater Error", message);
                return Result.Failed;
            }
        }
    }
}

