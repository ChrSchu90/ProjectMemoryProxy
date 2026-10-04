namespace ProjectMemoryProxy.Core.Routing;

using System;

/// <summary>
/// Represents a registered Basic Memory project routing.
/// </summary>
/// <param name="MemoryProjectId">The Basic Memory external project identifier.</param>
/// <param name="MemoryProjectName">The Basic Memory project name.</param>
/// <param name="Status">The routing status.</param>
public sealed record ProjectRouting(Guid MemoryProjectId, string MemoryProjectName, Status Status);
