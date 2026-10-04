namespace ProjectMemoryProxy.BasicMemory.Policy;

using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Classifies discovered Basic Memory tools according to the approved routing policy.
/// </summary>
internal sealed class BasicMemoryToolClassifier
{
    #region Static Fields

    private static readonly HashSet<string> ExplicitAdapterTools = new(StringComparer.Ordinal)
    {
        "some_placeholder_tool"
    };

    private static readonly HashSet<string> ProjectLifecycleTools = new(StringComparer.Ordinal)
    {
        "create_memory_project",
        "delete_project",
        "list_memory_projects"
    };

    private static readonly HashSet<string> GlobalAllowlistedTools = new(StringComparer.Ordinal)
    {
        "basic_memory_diagnostics"
    };

    private static readonly HashSet<string> IntentionallyBlockedTools = new(StringComparer.Ordinal)
    {
        "list_workspaces",  // Cloud workspace/tenant routing is outside the local-only ProjectMemoryProxy MVP.
        "fetch",            // OpenAI compatibility tool; its standard fetch(id) contract cannot carry the explicit context_id required for fail-closed project routing.
        "search"            // OpenAI compatibility tool; its standard search(query) contract cannot carry the explicit context_id required for fail-closed project routing.
    };

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
        return Classify(toolName, inputSchema, out _);
    }

    /// <summary>
    /// Classifies a discovered tool using its canonical upstream name and input schema
    /// and returns the routing analysis when schema-based classification was required.
    /// </summary>
    public ToolRoutingClassification Classify(string toolName, JsonElement inputSchema, out RoutingSchemaAnalysis? routingAnalysis)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        routingAnalysis = null;

        if (ExplicitAdapterTools.Contains(toolName))
            return ToolRoutingClassification.ExplicitAdapter;

        if (ProjectLifecycleTools.Contains(toolName))
            return ToolRoutingClassification.ProjectLifecycle;

        if (GlobalAllowlistedTools.Contains(toolName))
        {
            routingAnalysis = _routingSchemaGuard.Analyze(inputSchema);
            return ToolRoutingClassification.GlobalAllowlisted;
        }

        if (IntentionallyBlockedTools.Contains(toolName))
            return ToolRoutingClassification.IntentionallyBlocked;

        routingAnalysis = _routingSchemaGuard.Analyze(inputSchema);
        return routingAnalysis.IsAutomaticallyRoutable
                   ? ToolRoutingClassification.AutomaticallyRouted
                   : ToolRoutingClassification.Blocked;
    }

    #endregion

    #region Private Methods

    #endregion
}
