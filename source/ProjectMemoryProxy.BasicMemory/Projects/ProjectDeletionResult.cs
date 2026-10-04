namespace ProjectMemoryProxy.BasicMemory.Projects;

/// <summary>
/// Represents the result of deleting a Basic Memory project and its ProjectMemoryProxy routing.
/// </summary>
/// <param name="Status">The project deletion outcome.</param>
public sealed record ProjectDeletionResult(ProjectDeletionStatus Status);
