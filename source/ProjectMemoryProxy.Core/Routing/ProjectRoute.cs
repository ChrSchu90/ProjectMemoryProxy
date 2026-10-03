namespace ProjectMemoryProxy.Core.Routing;

using System;

/// <summary>
/// Represents the persisted routing state for a context.
/// </summary>
public sealed record ProjectRoute(Guid MemoryProjectId, string MemoryProjectName, Status ProjectRoutingStatus, Status BindingStatus);
