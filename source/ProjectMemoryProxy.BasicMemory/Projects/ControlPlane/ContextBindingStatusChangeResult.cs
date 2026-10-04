namespace ProjectMemoryProxy.BasicMemory.Projects.ControlPlane;

using ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Represents the result of changing a context-binding status.
/// </summary>
/// <param name="Status">The status-change outcome.</param>
/// <param name="Binding">The resulting binding for successful or idempotent outcomes.</param>
public sealed record ContextBindingStatusChangeResult(ContextBindingStatusChangeStatus Status, ContextBinding? Binding);
