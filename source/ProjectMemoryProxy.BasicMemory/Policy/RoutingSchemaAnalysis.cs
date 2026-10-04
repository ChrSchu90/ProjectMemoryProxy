namespace ProjectMemoryProxy.BasicMemory.Policy;

using System.Collections.Generic;

/// <summary>
/// Describes how an upstream MCP input schema can participate in server-controlled routing.
/// </summary>
internal sealed record RoutingSchemaAnalysis(RoutingSchemaStatus Status, ProjectSelectorKind ProjectSelector, bool InjectProjectName, IReadOnlyList<string> SuppressedProperties)
{
    /// <summary>
    /// Gets whether the schema can use the generic local project routing path.
    /// </summary>
    public bool IsAutomaticallyRoutable => Status == RoutingSchemaStatus.AutomaticallyRoutable;
}
