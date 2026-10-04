namespace ProjectMemoryProxy.BasicMemory.Projects;

using ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Represents the result of changing a project-routing status.
/// </summary>
/// <param name="Status">The status-change outcome.</param>
/// <param name="Routing">The resulting routing for successful or idempotent outcomes.</param>
public sealed record ProjectRoutingStatusChangeResult(ProjectRoutingStatusChangeStatus Status, ProjectRouting? Routing);
