namespace ProjectMemoryProxy.BasicMemory;

using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Classifies discovered Basic Memory tools according to the approved routing policy.
/// </summary>
internal sealed class BasicMemoryToolClassifier
{
    #region Static Fields

    private static readonly string[] ProjectSelectors =
        [
            "project",
            "project_id"
        ];

    private static readonly string[] ProjectWorkspaceSelectors =
        [
            "project",
            "project_id",
            "workspace"
        ];

    private static readonly string[] SearchSelectors =
        [
            "project",
            "project_id",
            "search_all_projects"
        ];

    private static readonly Dictionary<string, IReadOnlyCollection<string>> AutomaticallyRoutedTools = new(StringComparer.Ordinal)
    {
        ["build_context"] = ProjectSelectors,
        ["delete_note"] = ProjectSelectors,
        ["edit_note"] = ProjectWorkspaceSelectors,
        ["list_directory"] = ProjectSelectors,
        ["move_note"] = ProjectSelectors,
        ["read_content"] = ProjectSelectors,
        ["read_note"] = ProjectSelectors,
        ["recent_activity"] = ProjectSelectors,
        ["schema_diff"] = ProjectSelectors,
        ["schema_infer"] = ProjectSelectors,
        ["schema_validate"] = ProjectSelectors,
        ["search_notes"] = SearchSelectors,
        ["view_note"] = ProjectSelectors,
        ["write_note"] = ProjectWorkspaceSelectors
    };

    private static readonly HashSet<string> ExplicitAdapterTools = new(StringComparer.Ordinal)
    {
        "fetch",
        "search"
    };

    private static readonly HashSet<string> ProjectLifecycleTools = new(StringComparer.Ordinal)
    {
        "create_memory_project",
        "delete_project",
        "list_memory_projects",
        "list_workspaces"
    };

    private static readonly HashSet<string> GlobalAllowlistedTools = new(StringComparer.Ordinal);

    #endregion

    #region Private Fields

    private readonly RoutingSchemaGuard _routingSchemaGuard = new();

    #endregion

    #region Constructors

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <summary>
    /// Classifies a discovered tool using its canonical upstream name and input schema.
    /// </summary>
    public ToolRoutingClassification Classify(string toolName, JsonElement inputSchema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (ExplicitAdapterTools.Contains(toolName))
            return ToolRoutingClassification.ExplicitAdapter;

        if (ProjectLifecycleTools.Contains(toolName))
            return ToolRoutingClassification.ProjectLifecycle;

        if (GlobalAllowlistedTools.Contains(toolName))
            return ToolRoutingClassification.GlobalAllowlisted;

        if (!AutomaticallyRoutedTools.TryGetValue(toolName, out var expectedSelectors))
            return ToolRoutingClassification.Blocked;

        return _routingSchemaGuard.Analyze(inputSchema, expectedSelectors) ==
               RoutingSchemaGuardStatus.Compatible ? 
                   ToolRoutingClassification.AutomaticallyRouted : 
                   ToolRoutingClassification.Blocked;
    }

    #endregion

    #region Private Methods

    #endregion
}
