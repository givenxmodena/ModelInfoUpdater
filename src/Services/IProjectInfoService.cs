using ModelInfoUpdater.Models;

namespace ModelInfoUpdater.Services
{
    /// <summary>
    /// Interface for Project Information service operations.
    /// Abstracts Revit API interactions for better testability and adherence to
    /// Dependency Inversion Principle (DIP) - depend on abstractions, not concretions.
    /// </summary>
    public interface IProjectInfoService
    {
        /// <summary>
        /// Loads the current project information from the Revit document.
        /// </summary>
        /// <returns>A ProjectInfoModel containing current values, or null if unavailable.</returns>
        ProjectInfoModel LoadProjectInfo();

        /// <summary>
        /// Saves the project information to the Revit document.
        /// </summary>
        /// <param name="projectInfo">The project information to save.</param>
        /// <returns>True if save was successful; otherwise, false.</returns>
        bool SaveProjectInfo(ProjectInfoModel projectInfo);

        /// <summary>
        /// Checks if the document is available and editable.
        /// </summary>
        /// <returns>True if the document can be modified; otherwise, false.</returns>
        bool IsDocumentAvailable();
    }
}

