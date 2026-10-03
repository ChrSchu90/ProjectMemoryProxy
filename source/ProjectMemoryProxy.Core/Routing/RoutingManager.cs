namespace ProjectMemoryProxy.Core.Routing;

using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Resolves technical context identifiers to authorized memory project routings.
/// </summary>
public sealed class RoutingManager
{
    #region Static Fields

    #endregion

    #region Private Fields

    private readonly IProjectRegistry _projectRegistry;
    private readonly ILogger<RoutingManager> _logger;
    private readonly TimeProvider _timeProvider;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RoutingManager"/> class.
    /// </summary>
    /// <param name="projectRegistry">The project registry.</param>
    /// <param name="logger">The logger.</param>
    public RoutingManager(IProjectRegistry projectRegistry, ILogger<RoutingManager> logger)
        : this(projectRegistry, logger, TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RoutingManager"/> class with an explicit time provider.
    /// </summary>
    /// <param name="projectRegistry">The project registry.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="timeProvider">The time provider.</param>
    internal RoutingManager(IProjectRegistry projectRegistry, ILogger<RoutingManager> logger, TimeProvider timeProvider)
    {
        _projectRegistry = projectRegistry ?? throw new ArgumentNullException(nameof(projectRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <summary>
    /// Resolves a context to an active memory project routing.
    /// </summary>
    /// <param name="contextId">The context identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The context resolution result.</returns>
    public async Task<ContextResolution> ResolveContextAsync(ContextId contextId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contextId);

        var route = await _projectRegistry.FindRouteAsync(contextId, cancellationToken).ConfigureAwait(false);
        if (route == null)
            return new ContextResolution(ContextResolutionStatus.NotBound, null, null);

        if (route.ProjectRoutingStatus != Status.Active)
            return new ContextResolution(ContextResolutionStatus.ProjectRoutingInactive, null, null);

        if (route.BindingStatus != Status.Active)
            return new ContextResolution(ContextResolutionStatus.BindingInactive, null, null);

        return new ContextResolution(ContextResolutionStatus.Resolved, route.MemoryProjectId, route.MemoryProjectName);
    }

    #endregion

    #region Private Methods

    #endregion
}
