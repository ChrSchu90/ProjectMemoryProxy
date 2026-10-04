namespace ProjectMemoryProxy.Core.Routing;

using System;
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

    /// <summary>
    /// Finds a registered project routing by its Basic Memory external identifier.
    /// </summary>
    /// <param name="memoryProjectId">The Basic Memory external project identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The registered project routing, or <see langword="null"/> when no routing exists.</returns>
    Task<ProjectRouting?> FindByMemoryProjectIdAsync(Guid memoryProjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a registered project routing by its Basic Memory project name.
    /// </summary>
    /// <param name="memoryProjectName">The Basic Memory project name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The registered project routing, or <see langword="null"/> when no routing exists.</returns>
    Task<ProjectRouting?> FindByMemoryProjectNameAsync(string memoryProjectName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a registered project routing.
    /// </summary>
    /// <param name="memoryProjectId">The Basic Memory external project identifier.</param>
    /// <param name="memoryProjectName">The Basic Memory project name.</param>
    /// <param name="status">The initial routing status.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created project routing.</returns>
    Task<ProjectRouting> CreateAsync(Guid memoryProjectId, string memoryProjectName, Status status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the registered binding for an exact technical context identifier.
    /// </summary>
    /// <param name="contextId">The exact context identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The registered binding, or <see langword="null"/> when no binding exists.</returns>
    Task<ContextBinding?> FindBindingAsync(ContextId contextId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a context binding for an existing registered project routing.
    /// </summary>
    /// <param name="contextId">The exact context identifier.</param>
    /// <param name="memoryProjectId">The target Basic Memory project identifier.</param>
    /// <param name="status">The initial binding status.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created binding.</returns>
    Task<ContextBinding> CreateBindingAsync(ContextId contextId, Guid memoryProjectId, Status status, CancellationToken cancellationToken = default);

    #endregion
}
