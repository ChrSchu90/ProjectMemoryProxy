namespace ProjectMemoryProxy.BasicMemory.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Provides a minimal routing-only <see cref="IProjectRegistry"/> stub for Basic Memory tests that need a fixed resolved route.
/// </summary>
internal sealed class StubProjectRegistry : IProjectRegistry
{
    #region Static Fields

    #endregion

    #region Private Fields

    private readonly ProjectRoute? _route;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="StubProjectRegistry"/> class.
    /// </summary>
    /// <param name="route">The route returned from <see cref="FindRouteAsync"/>.</param>
    public StubProjectRegistry(ProjectRoute? route)
    {
        _route = route;
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public Task<ProjectRoute?> FindRouteAsync(ContextId contextId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_route);
    }

    /// <inheritdoc />
    public Task<ProjectRouting?> FindByMemoryProjectIdAsync(Guid memoryProjectId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public Task<ProjectRouting?> FindByMemoryProjectNameAsync(string memoryProjectName, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public Task<ProjectRouting> CreateAsync(Guid memoryProjectId, string memoryProjectName, Status status, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public Task<bool> TryRemoveProjectAsync(Guid memoryProjectId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public Task<ContextBinding?> FindBindingAsync(ContextId contextId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public Task<ContextBinding> CreateBindingAsync(ContextId contextId, Guid memoryProjectId, Status status, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public Task<bool> TryUpdateProjectStatusAsync(Guid memoryProjectId, Status expectedStatus, Status newStatus, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public Task<bool> TryUpdateBindingStatusAsync(ContextId contextId, Status expectedStatus, Status newStatus, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public Task<bool> TryRemoveBindingAsync(ContextId contextId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ProjectRouting>> ListProjectsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ProjectRouting>>(Array.Empty<ProjectRouting>());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ContextBinding>> ListBindingsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ContextBinding>>(Array.Empty<ContextBinding>());
    }

    #endregion

    #region Private Methods

    #endregion
}
