namespace ProjectMemoryProxy.BasicMemory;

/// <summary>
/// Contains canonical upstream Basic Memory MCP tool names used by ProjectMemoryProxy.
/// </summary>
internal static class BasicMemoryToolNames
{
    #region Static Fields

    /// <summary>
    /// The upstream Basic Memory project listing, creation and recent-activity tool.
    /// This tool is reserved for the PMP control plane and never mirrored publicly.
    /// </summary>
    internal const string Project = "bm_project";

    /// <summary>
    /// The separately enabled upstream Basic Memory project deletion tool.
    /// </summary>
    internal const string ProjectDelete = "bm_project_delete";

    /// <summary>
    /// The upstream read-only manual tool, explicitly hidden from the PMP surface.
    /// </summary>
    internal const string Manual = "bm_man";

    #endregion
}
