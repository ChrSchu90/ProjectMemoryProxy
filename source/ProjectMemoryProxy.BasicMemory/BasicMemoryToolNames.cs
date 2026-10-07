namespace ProjectMemoryProxy.BasicMemory;

/// <summary>
/// Contains canonical upstream Basic Memory MCP tool names used by ProjectMemoryProxy.
/// </summary>
internal static class BasicMemoryToolNames
{
    #region Static Fields

    /// <summary>
    /// The upstream Basic Memory diagnostics tool.
    /// </summary>
    internal const string Diagnostics = "basic_memory_diagnostics";

    /// <summary>
    /// The upstream Basic Memory project-listing tool.
    /// </summary>
    internal const string ListMemoryProjects = "list_memory_projects";

    /// <summary>
    /// The upstream Basic Memory project-creation tool.
    /// </summary>
    internal const string CreateMemoryProject = "create_memory_project";

    /// <summary>
    /// The upstream Basic Memory project-deletion tool.
    /// </summary>
    internal const string DeleteProject = "delete_project";

    /// <summary>
    /// The upstream Basic Memory workspace-listing tool.
    /// </summary>
    internal const string ListWorkspaces = "list_workspaces";

    /// <summary>
    /// The upstream Basic Memory OpenAI-compatible search tool.
    /// </summary>
    internal const string Search = "search";

    /// <summary>
    /// The upstream Basic Memory OpenAI-compatible fetch tool.
    /// </summary>
    internal const string Fetch = "fetch";

    #endregion
}
