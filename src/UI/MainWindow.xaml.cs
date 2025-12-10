using System.Windows;
using ModelInfoUpdater.Services;
using ModelInfoUpdater.ViewModels;
using Autodesk.Revit.DB;

namespace ModelInfoUpdater.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// This is a thin code-behind following MVVM pattern.
    /// All business logic resides in the ViewModel.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// Sets up the ViewModel with required dependencies.
        /// </summary>
        /// <param name="document">The active Revit document.</param>
        public MainWindow(Document document)
        {
            InitializeComponent();

            // Set dynamic window title based on the current assembly version
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                Title = $"Model Info Updater v{version.Major}.{version.Minor}.{version.Build}";
            }

            // Create service (could be injected via DI container in larger applications)
            IProjectInfoService projectInfoService = new ProjectInfoService(document);

            // Create ViewModel with dependencies
            // Pass a close action delegate to allow ViewModel to close the window
            var viewModel = new MainViewModel(
                projectInfoService,
                () =>
                {
                    DialogResult = true;
                    Close();
                }
            );

            // Set the DataContext for data binding
            DataContext = viewModel;
        }
    }
}

