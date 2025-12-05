namespace ModelInfoUpdater.Models
{
    /// <summary>
    /// Data Transfer Object representing Project Information.
    /// Encapsulates project-related data for transfer between layers.
    /// </summary>
    public class ProjectInfoModel
    {
        /// <summary>
        /// Gets or sets the project name.
        /// </summary>
        public string ProjectName { get; set; }

        /// <summary>
        /// Gets or sets the project number.
        /// </summary>
        public string ProjectNumber { get; set; }

        /// <summary>
        /// Gets or sets the client name.
        /// </summary>
        public string ClientName { get; set; }

        /// <summary>
        /// Gets or sets the project status.
        /// </summary>
        public string ProjectStatus { get; set; }

        /// <summary>
        /// Initializes a new instance of ProjectInfoModel with default values.
        /// </summary>
        public ProjectInfoModel()
        {
            ProjectName = string.Empty;
            ProjectNumber = string.Empty;
            ClientName = string.Empty;
            ProjectStatus = string.Empty;
        }

        /// <summary>
        /// Creates a deep copy of this ProjectInfoModel.
        /// </summary>
        /// <returns>A new ProjectInfoModel with the same values.</returns>
        public ProjectInfoModel Clone()
        {
            return new ProjectInfoModel
            {
                ProjectName = this.ProjectName,
                ProjectNumber = this.ProjectNumber,
                ClientName = this.ClientName,
                ProjectStatus = this.ProjectStatus
            };
        }
    }
}

