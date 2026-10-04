namespace ProjectMemoryProxy.BasicMemory.Projects;

using ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Represents the result of registering an existing Basic Memory project with ProjectMemoryProxy.
/// </summary>
/// <param name="Status">The registration outcome.</param>
/// <param name="Routing">The registered routing for successful or idempotent outcomes.</param>
public sealed record ProjectRegistrationResult(ProjectRegistrationStatus Status, ProjectRouting? Routing);
