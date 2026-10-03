namespace ProjectMemoryProxy.Core.Routing;

using System.Threading;

using System.Threading.Tasks;

/// <summary>
/// Provides persistent routing state for memory projects.
/// </summary>
public interface IProjectRegistry
{
    #region Events

    #endregion

    #region Properties

    #endregion

    #region Methods

    /// <summary>
    /// Finds the project route associated with a context.
    /// </summary>
    /// <param name="contextId">The context identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted route, or <see langword="null"/> when no binding exists.</returns>
    Task<ProjectRoute?> FindRouteAsync(ContextId contextId, CancellationToken cancellationToken = default);

    #endregion
}
