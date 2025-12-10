using System.Threading.Tasks;
using System.Windows;

namespace ModelInfoUpdater.Updater
{
    /// <summary>
    /// Thin WPF shell for the updater. All logic lives in the ViewModel; this
    /// class just wires up the window and kicks off the async workflow.
    /// </summary>
    public partial class UpdateWindow : Window
    {
        private readonly UpdateViewModel _viewModel;

        public int ExitCode => _viewModel.ExitCode;

        public UpdateWindow(bool updateMode, int? revitPid, string? revitExePath)
        {
            InitializeComponent();

            // In this application the window is run as the main WPF window via
            // Application.Run(window), not as a modal dialog (ShowDialog). Using
            // DialogResult in that scenario throws InvalidOperationException.
            // We simply close the window via the provided callback and let the
            // view model communicate the final ExitCode back to Program.Main.
            _viewModel = new UpdateViewModel(updateMode, revitPid, revitExePath, () =>
            {
                Close();
            });

            DataContext = _viewModel;

            Loaded += async (_, __) => await OnLoadedAsync();
        }

        private async Task OnLoadedAsync()
        {
            await _viewModel.StartAsync();
        }
    }
}

