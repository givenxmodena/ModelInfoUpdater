using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ModelInfoUpdater.Core;
using ModelInfoUpdater.Logging;

namespace ModelInfoUpdater.Updater
{
    /// <summary>
    /// ViewModel for the updater WPF window. Mirrors the console flow but
    /// exposes status/progress and commands for a simple MVVM GUI.
    /// </summary>
    public class UpdateViewModel : ViewModelBase
    {
        private readonly bool _updateMode;
        private readonly int? _revitPid;
        private readonly string? _revitExePath;
        private readonly Action _closeAction;

        private string _statusMessage = "Preparing to update...";
        private bool _isError;
        private int _progress;
        private string _progressText = "0%";
        private bool _isIndeterminate = true;
        private bool _isRestartRevitVisible;
        private bool _isCloseEnabled;

        private readonly RelayCommand _closeCommand;
        private readonly RelayCommand _restartRevitCommand;

        public int ExitCode { get; private set; } = 0;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsError
        {
            get => _isError;
            set => SetProperty(ref _isError, value);
        }

        public int Progress
        {
            get => _progress;
            set
            {
                if (SetProperty(ref _progress, value))
                {
                    ProgressText = $"{value}%";
                }
            }
        }

        public string ProgressText
        {
            get => _progressText;
            private set => SetProperty(ref _progressText, value);
        }

        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set => SetProperty(ref _isIndeterminate, value);
        }

        public bool IsRestartRevitVisible
        {
            get => _isRestartRevitVisible;
            set
            {
                if (SetProperty(ref _isRestartRevitVisible, value))
                {
                    _restartRevitCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsCloseEnabled
        {
            get => _isCloseEnabled;
            set
            {
                if (SetProperty(ref _isCloseEnabled, value))
                {
                    _closeCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand CloseCommand => _closeCommand;
        public ICommand RestartRevitCommand => _restartRevitCommand;

        public UpdateViewModel(bool updateMode, int? revitPid, string? revitExePath, Action closeAction)
        {
            _updateMode = updateMode;
            _revitPid = revitPid;
            _revitExePath = revitExePath;
            _closeAction = closeAction ?? throw new ArgumentNullException(nameof(closeAction));

            _closeCommand = new RelayCommand(_ => _closeAction(), _ => IsCloseEnabled);
            _restartRevitCommand = new RelayCommand(_ => RestartRevit(), _ => IsRestartRevitVisible);

            StatusMessage = "Starting updater...";
        }

        /// <summary>
        /// Kicks off the same lifecycle as the old console Main: wait for Revit,
        /// check/download/apply updates, deploy to Revit, then optionally restart.
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                if (_revitPid.HasValue)
                {
                    StatusMessage = "Waiting for Revit to close...";
                    IsIndeterminate = true;
                    await Program.WaitForRevitToClose(_revitPid.Value, silent: true);
                }

                if (_updateMode)
                {
                    IsIndeterminate = false;
                    Progress = 0;

                    bool updated = await Program.CheckAndApplyUpdatesAsync(
                        silent: true,
                        statusCallback: msg => Application.Current.Dispatcher.Invoke(() => StatusMessage = msg),
                        progressCallback: p => Application.Current.Dispatcher.Invoke(() =>
                        {
                            IsIndeterminate = false;
                            Progress = p;
                        }));

                    if (updated)
                    {
                        // Velopack restarts the app; nothing more to do here.
                        return;
                    }
                }

                StatusMessage = "Installing add-in into Revit add-ins folders...";
                IsIndeterminate = true;

                await Task.Run(() => Program.DeployToRevit(silent: true));

                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsIndeterminate = false;
                    Progress = 100;
                    StatusMessage = string.IsNullOrEmpty(_revitExePath)
                        ? "Update complete. Please restart Revit to use the updated add-in."
                        : "Update complete. Click 'Restart Revit' to relaunch Revit.";
                    IsRestartRevitVisible = !string.IsNullOrEmpty(_revitExePath);
                    IsCloseEnabled = true;
                });
            }
            catch (Exception ex)
            {
                FileLogger.LogException("Updater", "UpdateViewModel.StartAsync", ex);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsError = true;
                    StatusMessage = $"Error: {ex.Message}";
                    IsIndeterminate = false;
                    IsCloseEnabled = true;
                    ExitCode = 1;
                });
            }
        }

        private void RestartRevit()
        {
            if (string.IsNullOrEmpty(_revitExePath))
            {
                IsRestartRevitVisible = false;
                return;
            }

            try
            {
                Program.RestartRevit(_revitExePath, silent: true);
                _closeAction();
            }
            catch
            {
                // RestartRevit already logs; just ensure the user can close.
                IsCloseEnabled = true;
            }
        }
    }
}

