using System;
using Autodesk.Revit.DB;
using ModelInfoUpdater.Models;

namespace ModelInfoUpdater.Services
{
    /// <summary>
    /// Service responsible for reading and writing Project Information to Revit.
    /// Single Responsibility Principle (SRP) - handles only Revit API interactions.
    /// </summary>
    public class ProjectInfoService : IProjectInfoService
    {
        private readonly Document _document;

        /// <summary>
        /// Initializes a new instance of ProjectInfoService.
        /// </summary>
        /// <param name="document">The Revit document to operate on.</param>
        /// <exception cref="ArgumentNullException">Thrown if document is null.</exception>
        public ProjectInfoService(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <inheritdoc/>
        public bool IsDocumentAvailable()
        {
            return _document != null && _document.IsValidObject;
        }

        /// <inheritdoc/>
        public ProjectInfoModel LoadProjectInfo()
        {
            if (!IsDocumentAvailable())
            {
                return null;
            }

            ProjectInfo projectInfo = _document.ProjectInformation;
            if (projectInfo == null)
            {
                return null;
            }

            return new ProjectInfoModel
            {
                ProjectName = GetParameterValue(projectInfo, BuiltInParameter.PROJECT_NAME),
                ProjectNumber = GetParameterValue(projectInfo, BuiltInParameter.PROJECT_NUMBER),
                ClientName = GetParameterValue(projectInfo, BuiltInParameter.CLIENT_NAME),
                ProjectStatus = GetParameterValue(projectInfo, BuiltInParameter.PROJECT_STATUS)
            };
        }

        /// <inheritdoc/>
        public bool SaveProjectInfo(ProjectInfoModel model)
        {
            if (!IsDocumentAvailable() || model == null)
            {
                return false;
            }

            try
            {
                using (Transaction transaction = new Transaction(_document, "Update Project Information"))
                {
                    transaction.Start();

                    ProjectInfo projectInfo = _document.ProjectInformation;
                    if (projectInfo == null)
                    {
                        transaction.RollBack();
                        return false;
                    }

                    SetParameterValue(projectInfo, BuiltInParameter.PROJECT_NAME, model.ProjectName);
                    SetParameterValue(projectInfo, BuiltInParameter.PROJECT_NUMBER, model.ProjectNumber);
                    SetParameterValue(projectInfo, BuiltInParameter.CLIENT_NAME, model.ClientName);
                    SetParameterValue(projectInfo, BuiltInParameter.PROJECT_STATUS, model.ProjectStatus);

                    transaction.Commit();
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a string parameter value from ProjectInfo.
        /// </summary>
        private string GetParameterValue(ProjectInfo projectInfo, BuiltInParameter paramId)
        {
            Parameter param = projectInfo.get_Parameter(paramId);
            if (param != null && param.HasValue)
            {
                return param.AsString() ?? string.Empty;
            }
            return string.Empty;
        }

        /// <summary>
        /// Sets a string parameter value on ProjectInfo.
        /// </summary>
        private bool SetParameterValue(ProjectInfo projectInfo, BuiltInParameter paramId, string value)
        {
            Parameter param = projectInfo.get_Parameter(paramId);
            if (param != null && !param.IsReadOnly)
            {
                return param.Set(value ?? string.Empty);
            }
            return false;
        }
    }
}

