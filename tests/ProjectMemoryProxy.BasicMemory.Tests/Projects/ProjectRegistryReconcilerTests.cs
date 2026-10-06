namespace ProjectMemoryProxy.BasicMemory.Tests.Projects;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectMemoryProxy.BasicMemory.Projects;
using ProjectMemoryProxy.BasicMemory.Projects.Directory;
using ProjectMemoryProxy.BasicMemory.Projects.Lifecycle;
using ProjectMemoryProxy.Core.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Tests for <see cref="ProjectRegistryReconciler"/>
/// </summary>
[TestClass]
public sealed class ProjectRegistryReconcilerTests
{
    #region Static Fields

    private static readonly Guid ProjectAId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid ProjectBId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    #endregion

    #region Private Fields

    #endregion

    #region Tests Constructors

    #endregion

    #region Test Preparation

    #endregion

    #region Tests

    /// <summary>
    /// Verifies that a Basic Memory project missing from the local registry is registered as active from the current project snapshot.
    /// </summary>
    [TestMethod]
    public async Task ReconcileAsyncRegistersMissingProjectAsActive()
    {
        var projectDirectory = new StubBasicMemoryProjectDirectory(new BasicMemoryProjectInfo(ProjectAId, "project-a"));
        var projectRegistry = new StubProjectRegistry();
        var reconciler = CreateReconciler(projectDirectory, projectRegistry);

        await reconciler.ReconcileAsync();
        var routing = projectRegistry.GetProject(ProjectAId);
        Assert.IsNotNull(routing);
        Assert.AreEqual("project-a", routing.MemoryProjectName);
        Assert.AreEqual(Status.Active, routing.Status);
        Assert.AreEqual(1, projectRegistry.CreateCallCount);
        Assert.AreEqual(1, projectDirectory.ListCallCount);
        Assert.AreEqual(0, projectDirectory.ValidateCallCount);
    }

    /// <summary>
    /// Verifies that an active local routing is deactivated when its Basic Memory project is absent from the current snapshot.
    /// </summary>
    [TestMethod]
    public async Task ReconcileAsyncDeactivatesActiveProjectMissingFromBasicMemory()
    {
        var projectDirectory = new StubBasicMemoryProjectDirectory();
        var projectRegistry = new StubProjectRegistry(new ProjectRouting(ProjectAId, "project-a", Status.Active));
        var reconciler = CreateReconciler(projectDirectory, projectRegistry);

        await reconciler.ReconcileAsync();
        var routing = projectRegistry.GetProject(ProjectAId);
        Assert.IsNotNull(routing);
        Assert.AreEqual(Status.Inactive, routing.Status);
        Assert.AreEqual(1, projectRegistry.TryUpdateProjectStatusCallCount);
        Assert.AreEqual(0, projectRegistry.CreateCallCount);
    }

    /// <summary>
    /// Verifies that an already inactive local routing remains unchanged when its Basic Memory project is absent.
    /// </summary>
    [TestMethod]
    public async Task ReconcileAsyncLeavesAlreadyInactiveMissingProjectUnchanged()
    {
        var projectDirectory = new StubBasicMemoryProjectDirectory();
        var projectRegistry = new StubProjectRegistry(new ProjectRouting(ProjectAId, "project-a", Status.Inactive));
        var reconciler = CreateReconciler(projectDirectory, projectRegistry);

        await reconciler.ReconcileAsync();
        var routing = projectRegistry.GetProject(ProjectAId);
        Assert.IsNotNull(routing);
        Assert.AreEqual(Status.Inactive, routing.Status);
        Assert.AreEqual(0, projectRegistry.TryUpdateProjectStatusCallCount);
        Assert.AreEqual(0, projectRegistry.CreateCallCount);
    }

    /// <summary>
    /// Verifies that reconciliation never automatically reactivates an inactive routing when the matching Basic Memory project exists again.
    /// </summary>
    [TestMethod]
    public async Task ReconcileAsyncDoesNotReactivateInactiveProject()
    {
        var projectDirectory = new StubBasicMemoryProjectDirectory(new BasicMemoryProjectInfo(ProjectAId, "project-a"));
        var projectRegistry = new StubProjectRegistry(new ProjectRouting(ProjectAId, "project-a", Status.Inactive));
        var reconciler = CreateReconciler(projectDirectory, projectRegistry);

        await reconciler.ReconcileAsync();
        var routing = projectRegistry.GetProject(ProjectAId);
        Assert.IsNotNull(routing);
        Assert.AreEqual(Status.Inactive, routing.Status);
        Assert.AreEqual(0, projectRegistry.TryUpdateProjectStatusCallCount);
        Assert.AreEqual(0, projectRegistry.CreateCallCount);
    }

    /// <summary>
    /// Verifies that reconciliation performs no registry mutation when the local inventory already matches the Basic Memory project snapshot.
    /// </summary>
    [TestMethod]
    public async Task ReconcileAsyncDoesNothingWhenInventoriesMatch()
    {
        var projectDirectory = new StubBasicMemoryProjectDirectory(new BasicMemoryProjectInfo(ProjectAId, "project-a"), new BasicMemoryProjectInfo(ProjectBId, "project-b"));
        var projectRegistry = new StubProjectRegistry(new ProjectRouting(ProjectAId, "project-a", Status.Active), new ProjectRouting(ProjectBId, "project-b", Status.Inactive));
        var reconciler = CreateReconciler(projectDirectory, projectRegistry);

        await reconciler.ReconcileAsync();
        Assert.AreEqual(0, projectRegistry.CreateCallCount);
        Assert.AreEqual(0, projectRegistry.TryUpdateProjectStatusCallCount);
        Assert.AreEqual(1, projectRegistry.ListProjectsCallCount);
        Assert.AreEqual(1, projectDirectory.ListCallCount);
        Assert.AreEqual(0, projectDirectory.ValidateCallCount);
    }

    /// <summary>
    /// Verifies that a project identifier associated with different names fails closed before any reconciliation mutation occurs.
    /// </summary>
    [TestMethod]
    public async Task ReconcileAsyncFailsClosedForProjectNameMismatchBeforeMutation()
    {
        var projectDirectory = new StubBasicMemoryProjectDirectory(new BasicMemoryProjectInfo(ProjectAId, "project-a"), new BasicMemoryProjectInfo(ProjectBId, "project-b"));
        var projectRegistry = new StubProjectRegistry(new ProjectRouting(ProjectAId, "different-project", Status.Active), new ProjectRouting(Guid.Parse("99999999-8888-7777-6666-555555555555"), "stale-project", Status.Active));
        var reconciler = CreateReconciler(projectDirectory, projectRegistry);

        await Assert.ThrowsAsync<InvalidOperationException>(() => reconciler.ReconcileAsync());
        Assert.AreEqual(0, projectRegistry.CreateCallCount);
        Assert.AreEqual(0, projectRegistry.TryUpdateProjectStatusCallCount);
        Assert.AreEqual(Status.Active, projectRegistry.GetProject(ProjectAId)!.Status);
    }

    /// <summary>
    /// Verifies that a project name associated with different identifiers fails closed before any reconciliation mutation occurs.
    /// </summary>
    [TestMethod]
    public async Task ReconcileAsyncFailsClosedForProjectIdMismatchBeforeMutation()
    {
        var projectDirectory = new StubBasicMemoryProjectDirectory(new BasicMemoryProjectInfo(ProjectAId, "project-a"), new BasicMemoryProjectInfo(ProjectBId, "project-b"));
        var projectRegistry = new StubProjectRegistry(new ProjectRouting(ProjectBId, "project-a", Status.Active), new ProjectRouting(Guid.Parse("99999999-8888-7777-6666-555555555555"), "stale-project", Status.Active));
        var reconciler = CreateReconciler(projectDirectory, projectRegistry);

        await Assert.ThrowsAsync<InvalidOperationException>(() => reconciler.ReconcileAsync());
        Assert.AreEqual(0, projectRegistry.CreateCallCount);
        Assert.AreEqual(0, projectRegistry.TryUpdateProjectStatusCallCount);
        Assert.AreEqual(Status.Active, projectRegistry.GetProject(ProjectBId)!.Status);
    }

    /// <summary>
    /// Verifies that reconciliation reads the Basic Memory project inventory exactly once and does not revalidate individual projects.
    /// </summary>
    [TestMethod]
    public async Task ReconcileAsyncUsesSingleBasicMemoryProjectSnapshot()
    {
        var projectDirectory = new StubBasicMemoryProjectDirectory(new BasicMemoryProjectInfo(ProjectAId, "project-a"), new BasicMemoryProjectInfo(ProjectBId, "project-b"));
        var projectRegistry = new StubProjectRegistry();
        var reconciler = CreateReconciler(projectDirectory, projectRegistry);

        await reconciler.ReconcileAsync();
        Assert.AreEqual(1, projectDirectory.ListCallCount);
        Assert.AreEqual(0, projectDirectory.ValidateCallCount);
        Assert.AreEqual(2, projectRegistry.CreateCallCount);
    }

    /// <summary>
    /// Verifies that reconciliation propagates cancellation before reading or mutating project state.
    /// </summary>
    [TestMethod]
    public async Task ReconcileAsyncPropagatesCancellationBeforeWork()
    {
        var projectDirectory = new StubBasicMemoryProjectDirectory(new BasicMemoryProjectInfo(ProjectAId, "project-a"));
        var projectRegistry = new StubProjectRegistry();
        var reconciler = CreateReconciler(projectDirectory, projectRegistry);

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => reconciler.ReconcileAsync(cancellationTokenSource.Token));
        Assert.AreEqual(0, projectDirectory.ListCallCount);
        Assert.AreEqual(0, projectRegistry.ListProjectsCallCount);
        Assert.AreEqual(0, projectRegistry.CreateCallCount);
        Assert.AreEqual(0, projectRegistry.TryUpdateProjectStatusCallCount);
    }

    /// <summary>
    /// Verifies that reconciliation accepts a concurrent exact registration that wins the persistence race.
    /// </summary>
    [TestMethod]
    public async Task ReconcileAsyncAcceptsConcurrentExactRegistration()
    {
        var projectDirectory = new StubBasicMemoryProjectDirectory(new BasicMemoryProjectInfo(ProjectAId, "project-a"));
        var projectRegistry = new StubProjectRegistry();

        projectRegistry.CreateHandler = (memoryProjectId, memoryProjectName, status, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            projectRegistry.SetProject(new ProjectRouting(memoryProjectId, memoryProjectName, status));
            throw new ProjectRegistryConflictException("Concurrent registration won the persistence race.");
        };

        var reconciler = CreateReconciler(projectDirectory, projectRegistry);
        await reconciler.ReconcileAsync();

        var routing = projectRegistry.GetProject(ProjectAId);
        Assert.IsNotNull(routing);
        Assert.AreEqual("project-a", routing.MemoryProjectName);
        Assert.AreEqual(Status.Active, routing.Status);
        Assert.AreEqual(1, projectRegistry.CreateCallCount);
    }

    /// <summary>
    /// Verifies that reconciliation accepts a concurrent transition to inactive after its initial project snapshot.
    /// </summary>
    [TestMethod]
    public async Task ReconcileAsyncAcceptsConcurrentDeactivation()
    {
        var projectDirectory = new StubBasicMemoryProjectDirectory();
        var projectRegistry = new StubProjectRegistry(new ProjectRouting(ProjectAId, "project-a", Status.Active));
        projectRegistry.TryUpdateProjectStatusHandler = (memoryProjectId, expectedStatus, newStatus, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.AreEqual(ProjectAId, memoryProjectId);
                Assert.AreEqual(Status.Active, expectedStatus);
                Assert.AreEqual(Status.Inactive, newStatus);
                projectRegistry.SetProjectStatus(memoryProjectId, Status.Inactive);
                return Task.FromResult(false);
            };

        var reconciler = CreateReconciler(projectDirectory, projectRegistry);
        await reconciler.ReconcileAsync();

        var routing = projectRegistry.GetProject(ProjectAId);
        Assert.IsNotNull(routing);
        Assert.AreEqual(Status.Inactive, routing.Status);
        Assert.AreEqual(1, projectRegistry.TryUpdateProjectStatusCallCount);
    }

    /// <summary>
    /// Verifies that reconciliation accepts concurrent removal of a stale routing while deactivation loses the race.
    /// </summary>
    [TestMethod]
    public async Task ReconcileAsyncAcceptsConcurrentRemovalDuringDeactivation()
    {
        var projectDirectory = new StubBasicMemoryProjectDirectory();
        var projectRegistry = new StubProjectRegistry(new ProjectRouting(ProjectAId, "project-a", Status.Active));
        projectRegistry.TryUpdateProjectStatusHandler = (memoryProjectId, _, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                projectRegistry.RemoveProject(memoryProjectId);
                return Task.FromResult(false);
            };

        var reconciler = CreateReconciler(projectDirectory, projectRegistry);
        await reconciler.ReconcileAsync();
        Assert.IsNull(projectRegistry.GetProject(ProjectAId));
        Assert.AreEqual(1, projectRegistry.TryUpdateProjectStatusCallCount);
    }

    /// <summary>
    /// Verifies that reconciliation fails closed when a concurrent registration conflict leaves no classifiable persisted routing.
    /// </summary>
    [TestMethod]
    public async Task ReconcileAsyncFailsClosedForUnclassifiedRegistrationConflict()
    {
        var projectDirectory = new StubBasicMemoryProjectDirectory(new BasicMemoryProjectInfo(ProjectAId, "project-a"));
        var projectRegistry = new StubProjectRegistry
        {
            CreateHandler = (_, _, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new ProjectRegistryConflictException("Concurrent registration could not be classified.");
            }
        };

        var reconciler = CreateReconciler(projectDirectory, projectRegistry);
        await Assert.ThrowsAsync<InvalidOperationException>(() => reconciler.ReconcileAsync());

        Assert.IsNull(projectRegistry.GetProject(ProjectAId));
        Assert.AreEqual(1, projectRegistry.CreateCallCount);
    }

    /// <summary>
    /// Verifies that reconciliation fails closed when a concurrent deactivation miss leaves the routing active.
    /// </summary>
    [TestMethod]
    public async Task ReconcileAsyncFailsClosedForUnclassifiedDeactivationConflict()
    {
        var projectDirectory = new StubBasicMemoryProjectDirectory();
        var projectRegistry = new StubProjectRegistry(new ProjectRouting(ProjectAId, "project-a", Status.Active))
        {
            TryUpdateProjectStatusHandler = (_, _, _, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult(false);
                }
        };

        var reconciler = CreateReconciler(projectDirectory, projectRegistry);
        await Assert.ThrowsAsync<InvalidOperationException>(() => reconciler.ReconcileAsync());

        var routing = projectRegistry.GetProject(ProjectAId);
        Assert.IsNotNull(routing);
        Assert.AreEqual(Status.Active, routing.Status);
        Assert.AreEqual(1, projectRegistry.TryUpdateProjectStatusCallCount);
    }

    #endregion

    #region Private Methods

    private static ProjectRegistryReconciler CreateReconciler(StubBasicMemoryProjectDirectory projectDirectory, StubProjectRegistry projectRegistry)
    {
        var manager = new ProjectRegistryManager(projectDirectory, new StubBasicMemoryProjectLifecycle(), projectRegistry);
        return new ProjectRegistryReconciler(projectDirectory, projectRegistry, manager);
    }

    #endregion

    #region Test Classes

    private sealed class StubBasicMemoryProjectDirectory : IBasicMemoryProjectDirectory
    {
        #region Private Fields

        private readonly IReadOnlyList<BasicMemoryProjectInfo> _projects;

        #endregion

        #region Constructors

        public StubBasicMemoryProjectDirectory(params BasicMemoryProjectInfo[] projects)
        {
            _projects = projects;
        }

        #endregion

        #region Properties

        public int ListCallCount { get; private set; }

        public int ValidateCallCount { get; private set; }

        #endregion

        #region Public Methods

        public Task<IReadOnlyList<BasicMemoryProjectInfo>> ListAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ListCallCount++;
            return Task.FromResult(_projects);
        }

        public Task<BasicMemoryProjectValidationResult> ValidateAsync(Guid memoryProjectId, string memoryProjectName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ValidateCallCount++;
            throw new AssertFailedException("The reconciler must use its single Basic Memory project snapshot instead of per-project validation.");
        }

        #endregion
    }

    private sealed class StubProjectRegistry : IProjectRegistry
    {
        #region Private Fields

        private readonly Dictionary<Guid, ProjectRouting> _projectsById;

        #endregion

        #region Constructors

        public StubProjectRegistry(params ProjectRouting[] projects)
        {
            _projectsById = projects.ToDictionary(project => project.MemoryProjectId);
        }

        #endregion

        #region Properties

        public int CreateCallCount { get; private set; }

        public int ListProjectsCallCount { get; private set; }

        public int TryUpdateProjectStatusCallCount { get; private set; }

        public Func<Guid, string, Status, CancellationToken, Task<ProjectRouting>>? CreateHandler { get; set; }

        public Func<Guid, Status, Status, CancellationToken, Task<bool>>? TryUpdateProjectStatusHandler { get; set; }

        #endregion

        #region Public Methods

        public Task<IReadOnlyList<ProjectRouting>> ListProjectsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ListProjectsCallCount++;
            return Task.FromResult<IReadOnlyList<ProjectRouting>>(_projectsById.Values.ToArray());
        }

        public Task<ProjectRoute?> FindRouteAsync(ContextId contextId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProjectRouting?> FindByMemoryProjectIdAsync(Guid memoryProjectId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _projectsById.TryGetValue(memoryProjectId, out var project);
            return Task.FromResult(project);
        }

        public Task<ProjectRouting?> FindByMemoryProjectNameAsync(string memoryProjectName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var project = _projectsById.Values.SingleOrDefault(candidate => string.Equals(candidate.MemoryProjectName, memoryProjectName, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(project);
        }

        public Task<ProjectRouting> CreateAsync(Guid memoryProjectId, string memoryProjectName, Status status, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CreateCallCount++;
            if (CreateHandler != null)
                return CreateHandler(memoryProjectId, memoryProjectName, status, cancellationToken);

            var project = new ProjectRouting(memoryProjectId, memoryProjectName, status);
            _projectsById.Add(memoryProjectId, project);
            return Task.FromResult(project);
        }

        public Task<bool> TryRemoveProjectAsync(Guid memoryProjectId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ContextBinding?> FindBindingAsync(ContextId contextId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ContextBinding> CreateBindingAsync(ContextId contextId, Guid memoryProjectId, Status status, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TryUpdateProjectStatusAsync(Guid memoryProjectId, Status expectedStatus, Status newStatus, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TryUpdateProjectStatusCallCount++;
            if (TryUpdateProjectStatusHandler != null)
            {
                return TryUpdateProjectStatusHandler(memoryProjectId, expectedStatus, newStatus, cancellationToken);
            }

            if (!_projectsById.TryGetValue(memoryProjectId, out var project) || project.Status != expectedStatus)
            {
                return Task.FromResult(false);
            }

            _projectsById[memoryProjectId] = project with { Status = newStatus };
            return Task.FromResult(true);
        }

        public Task<bool> TryUpdateBindingStatusAsync(ContextId contextId, Status expectedStatus, Status newStatus, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TryRemoveBindingAsync(ContextId contextId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ProjectRouting? GetProject(Guid memoryProjectId)
        {
            _projectsById.TryGetValue(memoryProjectId, out var project);
            return project;
        }

        public void SetProject(ProjectRouting project)
        {
            _projectsById[project.MemoryProjectId] = project;
        }

        public void SetProjectStatus(Guid memoryProjectId, Status status)
        {
            if (!_projectsById.TryGetValue(memoryProjectId, out var project))
                throw new InvalidOperationException($"Project '{memoryProjectId:D}' is not registered in the test registry.");

            _projectsById[memoryProjectId] = project with { Status = status };
        }

        public void RemoveProject(Guid memoryProjectId)
        {
            _projectsById.Remove(memoryProjectId);
        }

        #endregion
    }

    private sealed class StubBasicMemoryProjectLifecycle : IBasicMemoryProjectLifecycle
    {
        #region Public Methods

        public Task<BasicMemoryProjectCreationResult> CreateAsync(string memoryProjectName, string memoryProjectPath, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(string memoryProjectName, bool deleteNotes, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        #endregion
    }

    #endregion
}
