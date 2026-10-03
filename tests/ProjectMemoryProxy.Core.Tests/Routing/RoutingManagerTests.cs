namespace ProjectMemoryProxy.Core.Tests.Routing;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectMemoryProxy.Core.Routing;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Tests for <see cref="RoutingManager"/>
/// </summary>
[TestClass]
public sealed class RoutingManagerTests
{
    #region Private Fields

    private static readonly ContextId TestContextId = CreateContextId("git:github.com/ChrSchu90/ProjectMemoryProxy");

    #endregion

    #region Tests Constructors

    #endregion

    #region Test Preparation

    #endregion

    #region Tests

    /// <summary>
    /// Verifies that a context without a persisted binding is reported as not bound and does not expose a memory project identifier.
    /// </summary>
    [TestMethod]
    public async Task ResolveContextAsyncReturnsNotBoundWhenRouteDoesNotExist()
    {
        var registry = new StubProjectRegistry((ProjectRoute?)null);
        var manager = CreateManager(registry);

        var result = await manager.ResolveContextAsync(TestContextId, CancellationToken.None);
        Assert.AreEqual(ContextResolutionStatus.NotBound, result.Status);
        Assert.IsNull(result.MemoryProjectId);
        Assert.IsNull(result.MemoryProjectName);
    }

    /// <summary>
    /// Verifies that an inactive project routing fails closed even when the context binding itself is active.
    /// </summary>
    [TestMethod]
    public async Task ResolveContextAsyncReturnsProjectRoutingInactive()
    {
        var route = new ProjectRoute(
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            Status.Inactive,
            Status.Active);

        var registry = new StubProjectRegistry(route);
        var manager = CreateManager(registry);

        var result = await manager.ResolveContextAsync(TestContextId, CancellationToken.None);
        Assert.AreEqual(ContextResolutionStatus.ProjectRoutingInactive, result.Status);
        Assert.IsNull(result.MemoryProjectId);
        Assert.IsNull(result.MemoryProjectName);
    }

    /// <summary>
    /// Verifies that an inactive context binding fails closed even when the target project routing is active.
    /// </summary>
    [TestMethod]
    public async Task ResolveContextAsyncReturnsBindingInactive()
    {
        var route = new ProjectRoute(
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            Status.Active,
            Status.Inactive);

        var registry = new StubProjectRegistry(route);
        var manager = CreateManager(registry);

        var result = await manager.ResolveContextAsync(TestContextId, CancellationToken.None);

        Assert.AreEqual(ContextResolutionStatus.BindingInactive, result.Status);
        Assert.IsNull(result.MemoryProjectId);
        Assert.IsNull(result.MemoryProjectName);
    }

    /// <summary>
    /// Verifies that an active project routing with an active context binding resolves to the exact persisted memory project identifier.
    /// </summary>
    [TestMethod]
    public async Task ResolveContextAsyncReturnsResolvedRoute()
    {
        var memoryProjectId = Guid.NewGuid();
        var memoryProjectName = Guid.NewGuid().ToString("N");
        var route = new ProjectRoute(
            memoryProjectId,
            memoryProjectName,
            Status.Active,
            Status.Active);

        var registry = new StubProjectRegistry(route);
        var manager = CreateManager(registry);

        var result = await manager.ResolveContextAsync(TestContextId, CancellationToken.None);

        Assert.AreEqual(ContextResolutionStatus.Resolved, result.Status);
        Assert.AreEqual(memoryProjectId, result.MemoryProjectId);
        Assert.AreEqual(memoryProjectName, result.MemoryProjectName);
    }

    /// <summary>
    /// Verifies that cancellation requested while resolving a context is propagated to the caller.
    /// </summary>
    [TestMethod]
    public async Task ResolveContextAsyncPropagatesCancellation()
    {
        var registry = new StubProjectRegistry(
            static (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<ProjectRoute?>(null);
            });

        var manager = CreateManager(registry);

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => manager.ResolveContextAsync(TestContextId, cancellationTokenSource.Token));
    }

    /// <summary>
    /// Verifies that an unsupported project routing status fails closed instead of being treated as routable.
    /// </summary>
    [TestMethod]
    public async Task ResolveContextAsyncRejectsUnknownProjectRoutingStatus()
    {
        var route = new ProjectRoute(
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            (Status)int.MaxValue,
            Status.Active);

        var registry = new StubProjectRegistry(route);
        var manager = CreateManager(registry);

        var result = await manager.ResolveContextAsync(TestContextId, CancellationToken.None);
        Assert.AreEqual(ContextResolutionStatus.ProjectRoutingInactive, result.Status);
        Assert.IsNull(result.MemoryProjectId);
        Assert.IsNull(result.MemoryProjectName);
    }

    /// <summary>
    /// Verifies that an unsupported binding status fails closed instead of being treated as routable.
    /// </summary>
    [TestMethod]
    public async Task ResolveContextAsyncRejectsUnknownBindingStatus()
    {
        var route = new ProjectRoute(
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            Status.Active,
            (Status)int.MaxValue);

        var registry = new StubProjectRegistry(route);
        var manager = CreateManager(registry);

        var result = await manager.ResolveContextAsync(TestContextId, CancellationToken.None);
        Assert.AreEqual(ContextResolutionStatus.BindingInactive, result.Status);
        Assert.IsNull(result.MemoryProjectId);
        Assert.IsNull(result.MemoryProjectName);
    }

    #endregion

    #region Private Methods

    private static RoutingManager CreateManager(IProjectRegistry projectRegistry)
    {
        return new RoutingManager(
            projectRegistry,
            NullLogger<RoutingManager>.Instance);
    }

    private static ContextId CreateContextId(string value)
    {
        if (!ContextId.TryParse(value, out var contextId))
            throw new InvalidOperationException($"The test context identifier '{value}' is invalid.");

        return contextId;
    }


    #endregion

    #region Test Classes

    private sealed class StubProjectRegistry : IProjectRegistry
    {
        #region Private Fields

        private readonly Func<ContextId, CancellationToken, Task<ProjectRoute?>> _findRoute;

        #endregion

        #region Constructors

        public StubProjectRegistry(ProjectRoute? route)
            : this((_, _) => Task.FromResult(route))
        {
        }

        public StubProjectRegistry(Func<ContextId, CancellationToken, Task<ProjectRoute?>> findRoute)
        {
            _findRoute = findRoute ?? throw new ArgumentNullException(nameof(findRoute));
        }

        #endregion

        #region Public Methods

        public Task<ProjectRoute?> FindRouteAsync(ContextId contextId, CancellationToken cancellationToken = default)
        {
            return _findRoute(contextId, cancellationToken);
        }

        #endregion
    }

    #endregion
}
