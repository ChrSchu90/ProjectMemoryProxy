namespace ProjectMemoryProxy.Server.Tools;

using ModelContextProtocol.Server;
using ProjectMemoryProxy.Core.Routing;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides MCP tools for inspecting project memory routing.
/// </summary>
[McpServerToolType]
internal sealed class RoutingTools
{
    #region Static Fields

    #endregion

    #region Private Fields

    private readonly RoutingManager _routingManager;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RoutingTools"/> class.
    /// </summary>
    /// <param name="routingManager">The routing manager.</param>
    public RoutingTools(RoutingManager routingManager)
    {
        _routingManager = routingManager ?? throw new ArgumentNullException(nameof(routingManager));
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <summary>
    /// Resolves a technical context identifier to its configured project routing.
    /// </summary>
    /// <param name="context_id">The canonical technical context identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The routing resolution result.</returns>
    [McpServerTool(Name = "resolve_context", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Resolves a canonical technical context identifier to its configured Basic Memory project routing.")]
    public async Task<ResolveContextResult> ResolveContextAsync(
        [Description("Canonical technical context identifier, for example 'git:github.com/ChrSchu90/ProjectMemoryProxy'.")] string context_id,
        CancellationToken cancellationToken = default)
    {
        if (!ContextId.TryParse(context_id, out var parsedContextId))
            return new ResolveContextResult(context_id, "invalid_context", null);

        var resolution = await _routingManager.ResolveContextAsync(parsedContextId, cancellationToken).ConfigureAwait(false);
        return new ResolveContextResult(parsedContextId.ToString(), GetExternalStatus(resolution.Status), resolution.MemoryProjectId);
    }

    #endregion

    #region Private Methods

    private static string GetExternalStatus(ContextResolutionStatus status)
    {
        return status switch
            {
                ContextResolutionStatus.Resolved => "resolved",
                ContextResolutionStatus.NotBound => "not_bound",
                ContextResolutionStatus.ProjectRoutingInactive => "project_routing_inactive",
                ContextResolutionStatus.BindingInactive => "binding_inactive",
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
            };
    }

    #endregion
}
