namespace ProjectMemoryProxy.BasicMemory.Policy;

using System;
using System.Collections.Generic;
using System.Text.Json;
using ProjectMemoryProxy.BasicMemory;

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
        BasicMemoryToolNames.Project,
        BasicMemoryToolNames.ProjectDelete
    };

    private static readonly HashSet<string> GlobalAllowlistedTools = new(StringComparer.Ordinal)
    {
        BasicMemoryToolNames.Manual
    };

    // Keep intentionally excluded legacy compatibility tools blocked if a future upstream version reintroduces them.
    private static readonly HashSet<string> IntentionallyBlockedTools = new(StringComparer.Ordinal)
    {
        "fetch",
        "list_workspaces",
        "search"
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
