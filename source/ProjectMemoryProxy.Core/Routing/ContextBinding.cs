namespace ProjectMemoryProxy.Core.Routing;

using System;

/// <summary>
/// Represents a registered technical context binding.
/// </summary>
/// <param name="ContextId">The exact technical context identifier.</param>
/// <param name="MemoryProjectId">The target Basic Memory project identifier.</param>
/// <param name="Status">The binding status.</param>
public sealed record ContextBinding(ContextId ContextId, Guid MemoryProjectId, Status Status);
