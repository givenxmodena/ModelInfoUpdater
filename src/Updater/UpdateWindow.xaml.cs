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

            _viewModel = new UpdateViewModel(updateMode, revitPid, revitExePath, () =>
            {
                DialogResult = true;
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

