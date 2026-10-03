namespace ProjectMemoryProxy.Core.Routing;

using System;
using Microsoft.Extensions.Logging;

/// <summary>
/// Coordinates project memory lifecycle across persistence and the filesystem.
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
    /// Initializes a new instance of the <see cref="projectRegistry"/> class.
    /// </summary>
    /// <param name="projectRegistry">The project registry.</param>
    /// <param name="logger">The logger.</param>
    public RoutingManager(IProjectRegistry projectRegistry, ILogger<RoutingManager> logger)
        : this(projectRegistry, logger, TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="projectRegistry"/> class with an explicit time provider.
    /// </summary>
    /// <param name="logger">The memory project registry.</param>
    /// <param name="projectRegistry">The project registry.</param>
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

    #endregion

    #region Private Methods

    #endregion
}
