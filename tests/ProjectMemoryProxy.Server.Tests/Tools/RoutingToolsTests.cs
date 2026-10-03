namespace ProjectMemoryProxy.Server.Tests.Tools;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectMemoryProxy.Core.Routing;
using ProjectMemoryProxy.Server.Tools;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Tests for <see cref="RoutingTools"/>
/// </summary>
[TestClass]
public sealed class RoutingToolsTests
{
    #region Static Fields

    private const string TestContextId = "git:github.com/ChrSchu90/ProjectMemoryProxy";

    #endregion
    
    #region Private Fields

    #endregion

    #region Tests Constructors

    #endregion

    #region Test Preparation

    #endregion

    #region Tests

    /// <summary>
    /// Verifies that a malformed context identifier is rejected before accessing the project registry.
    /// </summary>
    [TestMethod]
    public async Task ResolveContextAsyncReturnsInvalidContextWithoutRegistryLookup()
    {
        var registry = new StubProjectRegistry((ProjectRoute?)null);
        var tools = CreateTools(registry);

        var result = await tools.ResolveContextAsync("invalid-context", CancellationToken.None);
        Assert.AreEqual("invalid-context", result.ContextId);
        Assert.AreEqual("invalid_context", result.Status);
        Assert.IsNull(result.ProjectId);
        Assert.AreEqual(0, registry.FindRouteCallCount);
    }

    /// <summary>
    /// Verifies that a valid context without a persisted binding is reported as not bound.
    /// </summary>
    [TestMethod]
    public async Task ResolveContextAsyncReturnsNotBound()
    {
        var registry = new StubProjectRegistry((ProjectRoute?)null);
        var tools = CreateTools(registry);

        var result = await tools.ResolveContextAsync(TestContextId, CancellationToken.None);
        Assert.AreEqual(TestContextId, result.ContextId);
        Assert.AreEqual("not_bound", result.Status);
        Assert.IsNull(result.ProjectId);
        Assert.AreEqual(1, registry.FindRouteCallCount);
    }

    /// <summary>
    /// Verifies that an inactive project routing is exposed using the public project routing inactive status.
    /// </summary>
    [TestMethod]
    public async Task ResolveContextAsyncReturnsProjectRoutingInactive()
    {
        var route = new ProjectRoute(Guid.NewGuid(), Status.Inactive, Status.Active);
        var tools = CreateTools(new StubProjectRegistry(route));

        var result = await tools.ResolveContextAsync(TestContextId, CancellationToken.None);
        Assert.AreEqual(TestContextId, result.ContextId);
        Assert.AreEqual("project_routing_inactive", result.Status);
        Assert.IsNull(result.ProjectId);
    }

    /// <summary>
    /// Verifies that an inactive context binding is exposed using the public binding inactive status.
    /// </summary>
    [TestMethod]
    public async Task ResolveContextAsyncReturnsBindingInactive()
    {
        var route = new ProjectRoute(Guid.NewGuid(), Status.Active, Status.Inactive);
        var tools = CreateTools(new StubProjectRegistry(route));

        var result = await tools.ResolveContextAsync(TestContextId, CancellationToken.None);
        Assert.AreEqual(TestContextId, result.ContextId);
        Assert.AreEqual("binding_inactive", result.Status);
        Assert.IsNull(result.ProjectId);
    }

    /// <summary>
    /// Verifies that an active route is exposed with the exact resolved Basic Memory project identifier.
    /// </summary>
    [TestMethod]
    public async Task ResolveContextAsyncReturnsResolvedProject()
    {
        var memoryProjectId = Guid.NewGuid();
        var route = new ProjectRoute(memoryProjectId, Status.Active, Status.Active);
        var tools = CreateTools(new StubProjectRegistry(route));

        var result = await tools.ResolveContextAsync(TestContextId, CancellationToken.None);
        Assert.AreEqual(TestContextId, result.ContextId);
        Assert.AreEqual("resolved", result.Status);
        Assert.AreEqual(memoryProjectId, result.ProjectId);
    }

    /// <summary>
    /// Verifies that cancellation while resolving a valid context is propagated to the caller.
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

        var tools = CreateTools(registry);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => tools.ResolveContextAsync(TestContextId, cancellationTokenSource.Token));
    }

    #endregion

    #region Private Methods

    private static RoutingTools CreateTools(IProjectRegistry projectRegistry)
    {
        var routingManager = new RoutingManager(projectRegistry, NullLogger<RoutingManager>.Instance);
        return new RoutingTools(routingManager);
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

        #region Properties

        public int FindRouteCallCount { get; private set; }

        #endregion

        #region Public Methods

        public Task<ProjectRoute?> FindRouteAsync(ContextId contextId, CancellationToken cancellationToken = default)
        {
            FindRouteCallCount++;
            return _findRoute(contextId, cancellationToken);
        }

        #endregion
    }

    #endregion
}
