namespace ProjectMemoryProxy.Core.Routing;

using System;

/// <summary>
/// Represents the result of resolving a context to a memory project.
/// </summary>
public sealed record ContextResolution(ContextResolutionStatus Status, Guid? MemoryProjectId);
