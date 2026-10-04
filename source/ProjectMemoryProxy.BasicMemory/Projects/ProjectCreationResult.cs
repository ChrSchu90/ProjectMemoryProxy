namespace ProjectMemoryProxy.BasicMemory.Projects;

using ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Represents the result of creating and registering a Basic Memory project.
/// </summary>
/// <param name="Status">The project creation outcome.</param>
/// <param name="Routing">The registered routing for successful or idempotent outcomes.</param>
public sealed record ProjectCreationResult(ProjectCreationStatus Status, ProjectRouting? Routing);
