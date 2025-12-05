using System;
using System.Windows;
using System.Windows.Input;
using ModelInfoUpdater.Core;
using ModelInfoUpdater.Models;
using ModelInfoUpdater.Services;

namespace ModelInfoUpdater.ViewModels
{
    /// <summary>
    /// ViewModel for the MainWindow.
    /// Handles presentation logic and commands, separating UI from business logic.
    /// Follows Interface Segregation Principle (ISP) - exposes only what view needs.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly IProjectInfoService _projectInfoService;
        private readonly Action _closeAction;

        private string _projectName;
        private string _projectNumber;
        private string _clientName;
        private string _projectStatus;
        private string _statusMessage;
        private bool _isStatusError;

        #region Properties

        /// <summary>Gets or sets the project name.</summary>
        public string ProjectName
        {
            get => _projectName;
            set => SetProperty(ref _projectName, value);
        }

        /// <summary>Gets or sets the project number.</summary>
        public string ProjectNumber
        {
            get => _projectNumber;
            set => SetProperty(ref _projectNumber, value);
        }

        /// <summary>Gets or sets the client name.</summary>
        public string ClientName
        {
            get => _clientName;
            set => SetProperty(ref _clientName, value);
        }

        /// <summary>Gets or sets the project status.</summary>
        public string ProjectStatus
        {
            get => _projectStatus;
            set => SetProperty(ref _projectStatus, value);
        }

        /// <summary>Gets or sets the status message displayed to user.</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>Gets or sets whether the status message indicates an error.</summary>
        public bool IsStatusError
        {
            get => _isStatusError;
            set => SetProperty(ref _isStatusError, value);
        }

        #endregion

        #region Commands

        /// <summary>Command to load current values from document.</summary>
        public ICommand LoadCurrentCommand { get; }

        /// <summary>Command to save values to document.</summary>
        public ICommand SaveCommand { get; }

        /// <summary>Command to close the window.</summary>
        public ICommand CloseCommand { get; }

        #endregion

        /// <summary>
        /// Initializes a new instance of MainViewModel.
        /// </summary>
        /// <param name="projectInfoService">Service for Revit operations.</param>
        /// <param name="closeAction">Action to close the window.</param>
        public MainViewModel(IProjectInfoService projectInfoService, Action closeAction)
        {
            _projectInfoService = projectInfoService ?? throw new ArgumentNullException(nameof(projectInfoService));
            _closeAction = closeAction ?? throw new ArgumentNullException(nameof(closeAction));

            // Initialize commands
            LoadCurrentCommand = new RelayCommand(ExecuteLoadCurrent, CanExecuteLoadCurrent);
            SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
            CloseCommand = new RelayCommand(ExecuteClose);

            // Load initial values
            ExecuteLoadCurrent(null);
        }

        private bool CanExecuteLoadCurrent(object parameter)
        {
            return _projectInfoService.IsDocumentAvailable();
        }

        private void ExecuteLoadCurrent(object parameter)
        {
            try
            {
                ProjectInfoModel model = _projectInfoService.LoadProjectInfo();
                if (model != null)
                {
                    ProjectName = model.ProjectName;
                    ProjectNumber = model.ProjectNumber;
                    ClientName = model.ClientName;
                    ProjectStatus = model.ProjectStatus;
                    SetStatus("Values loaded successfully.", false);
                }
                else
                {
                    SetStatus("Could not load project information.", true);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error loading: {ex.Message}", true);
            }
        }

        private bool CanExecuteSave(object parameter)
        {
            return _projectInfoService.IsDocumentAvailable();
        }

        private void ExecuteSave(object parameter)
        {
            try
            {
                var model = new ProjectInfoModel
                {
                    ProjectName = ProjectName,
                    ProjectNumber = ProjectNumber,
                    ClientName = ClientName,
                    ProjectStatus = ProjectStatus
                };

                if (_projectInfoService.SaveProjectInfo(model))
                {
                    MessageBox.Show("Project information updated successfully!",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    _closeAction?.Invoke();
                }
                else
                {
                    SetStatus("Failed to save project information.", true);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error saving: {ex.Message}", true);
            }
        }

        private void ExecuteClose(object parameter)
        {
            _closeAction?.Invoke();
        }

        private void SetStatus(string message, bool isError)
        {
            StatusMessage = message;
            IsStatusError = isError;
        }
    }
}

