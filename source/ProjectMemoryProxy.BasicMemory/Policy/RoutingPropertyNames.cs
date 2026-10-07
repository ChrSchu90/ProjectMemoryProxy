namespace ProjectMemoryProxy.BasicMemory.Policy;

/// <summary>
/// Contains routing-sensitive MCP argument and schema property names controlled by ProjectMemoryProxy.
/// </summary>
internal static class RoutingPropertyNames
{
    #region Static Fields

    /// <summary>
    /// The public ProjectMemoryProxy context-routing property.
    /// </summary>
    internal const string ContextId = "context_id";

    /// <summary>
    /// The upstream Basic Memory project-name selector.
    /// </summary>
    internal const string Project = "project";

    /// <summary>
    /// The upstream Basic Memory project-identifier selector.
    /// </summary>
    internal const string ProjectId = "project_id";

    #endregion
}
