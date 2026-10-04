namespace ProjectMemoryProxy.BasicMemory.Projects;

/// <summary>
/// Describes the outcome reported by Basic Memory when creating a project.
/// </summary>
public enum BasicMemoryProjectCreationStatus
{
    /// <summary>
    /// Basic Memory created the project.
    /// </summary>
    Created,

    /// <summary>
    /// Basic Memory found an existing project with the requested name.
    /// </summary>
    AlreadyExists
}
