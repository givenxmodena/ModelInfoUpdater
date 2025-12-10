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

        // Current (read-only) values from Revit
        private string _currentProjectName;
        private string _currentProjectNumber;
        private string _currentClientName;
        private string _currentProjectStatus;

        // New (editable) values
        private string _newProjectName;
        private string _newProjectNumber;
        private string _newClientName;
        private string _newProjectStatus;

        private string _statusMessage;
        private bool _isStatusError;

        #region Current Value Properties (Read-Only Display)

        /// <summary>Gets the current project name from Revit.</summary>
        public string CurrentProjectName
        {
            get => _currentProjectName;
            private set => SetProperty(ref _currentProjectName, value);
        }

        /// <summary>Gets the current project number from Revit.</summary>
        public string CurrentProjectNumber
        {
            get => _currentProjectNumber;
            private set => SetProperty(ref _currentProjectNumber, value);
        }

        /// <summary>Gets the current client name from Revit.</summary>
        public string CurrentClientName
        {
            get => _currentClientName;
            private set => SetProperty(ref _currentClientName, value);
        }

        /// <summary>Gets the current project status from Revit.</summary>
        public string CurrentProjectStatus
        {
            get => _currentProjectStatus;
            private set => SetProperty(ref _currentProjectStatus, value);
        }

        #endregion

        #region New Value Properties (Editable)

        /// <summary>Gets or sets the new project name.</summary>
        public string NewProjectName
        {
            get => _newProjectName;
            set
            {
                if (SetProperty(ref _newProjectName, value))
                {
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets or sets the new project number.</summary>
        public string NewProjectNumber
        {
            get => _newProjectNumber;
            set
            {
                if (SetProperty(ref _newProjectNumber, value))
                {
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets or sets the new client name.</summary>
        public string NewClientName
        {
            get => _newClientName;
            set
            {
                if (SetProperty(ref _newClientName, value))
                {
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets or sets the new project status.</summary>
        public string NewProjectStatus
        {
            get => _newProjectStatus;
            set
            {
                if (SetProperty(ref _newProjectStatus, value))
                {
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        #endregion

        #region Status Properties

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
                    // Set current (read-only) values
                    CurrentProjectName = model.ProjectName;
                    CurrentProjectNumber = model.ProjectNumber;
                    CurrentClientName = model.ClientName;
                    CurrentProjectStatus = model.ProjectStatus;

                    // Clear the editable fields to show placeholders
                    NewProjectName = string.Empty;
                    NewProjectNumber = string.Empty;
                    NewClientName = string.Empty;
                    NewProjectStatus = string.Empty;

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
            // Can save if document is available and at least one field has been modified
            return _projectInfoService.IsDocumentAvailable() &&
                   (!string.IsNullOrWhiteSpace(NewProjectName) ||
                    !string.IsNullOrWhiteSpace(NewProjectNumber) ||
                    !string.IsNullOrWhiteSpace(NewClientName) ||
                    !string.IsNullOrWhiteSpace(NewProjectStatus));
        }

        private void ExecuteSave(object parameter)
        {
            try
            {
                // Use the new (editable) values for saving, but keep current values if fields are empty
                var model = new ProjectInfoModel
                {
                    ProjectName = string.IsNullOrWhiteSpace(NewProjectName) ? CurrentProjectName : NewProjectName,
                    ProjectNumber = string.IsNullOrWhiteSpace(NewProjectNumber) ? CurrentProjectNumber : NewProjectNumber,
                    ClientName = string.IsNullOrWhiteSpace(NewClientName) ? CurrentClientName : NewClientName,
                    ProjectStatus = string.IsNullOrWhiteSpace(NewProjectStatus) ? CurrentProjectStatus : NewProjectStatus
                };

                if (_projectInfoService.SaveProjectInfo(model))
                {
                    // Update current values to reflect the saved changes
                    CurrentProjectName = model.ProjectName;
                    CurrentProjectNumber = model.ProjectNumber;
                    CurrentClientName = model.ClientName;
                    CurrentProjectStatus = model.ProjectStatus;

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

