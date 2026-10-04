namespace ProjectMemoryProxy.BasicMemory.Tests.Projects;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectMemoryProxy.BasicMemory.Projects;
using ProjectMemoryProxy.Core.Routing;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Tests for <see cref="ProjectRegistryManager"/>
/// </summary>
[TestClass]
public sealed class ProjectRegistryManagerTests
{
    #region Static Fields

    private static readonly Guid ProjectAId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid ProjectBId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly ContextId GitContext = ParseContextId("git:github.com/ChrSchu90/ProjectMemoryProxy");
    private static readonly ContextId ChatGptContext = ParseContextId("chatgpt-project:chatty-mcp-and-aiharborvm");

    #endregion

    #region Private Fields

    #endregion

    #region Tests Constructors

    #endregion

    #region Test Preparation

    #endregion

    #region Tests


    /// <summary>
    /// Verifies that an exact Basic Memory identity is registered as an active project routing when no registry entry exists.
    /// </summary>
    [TestMethod]
    public async Task RegisterExistingProjectAsyncCreatesActiveRouting()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry();
        var manager = new ProjectRegistryManager(projectDirectory, projectRegistry);

        var result = await manager.RegisterExistingProjectAsync(ProjectAId, "project-a");
        Assert.AreEqual(ProjectRegistrationStatus.Registered, result.Status);

        Assert.IsNotNull(result.Routing);
        Assert.AreEqual(ProjectAId, result.Routing.MemoryProjectId);
        Assert.AreEqual("project-a", result.Routing.MemoryProjectName);
        Assert.AreEqual(Status.Active, result.Routing.Status);

        Assert.AreEqual(1, projectDirectory.ValidateCallCount);
        Assert.AreEqual(1, projectRegistry.FindByIdCallCount);
        Assert.AreEqual(1, projectRegistry.FindByNameCallCount);
        Assert.AreEqual(1, projectRegistry.CreateCallCount);

        Assert.AreEqual(ProjectAId, projectRegistry.CreatedMemoryProjectId);
        Assert.AreEqual("project-a", projectRegistry.CreatedMemoryProjectName);
        Assert.AreEqual(Status.Active, projectRegistry.CreatedStatus);
    }

    /// <summary>
    /// Verifies that an exact existing project routing is treated as idempotently registered without changing its status.
    /// </summary>
    [TestMethod]
    public async Task RegisterExistingProjectAsyncReturnsAlreadyRegisteredForExactRouting()
    {
        var existingRouting = new ProjectRouting(ProjectAId, "project-a", Status.Inactive);
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry
        {
            ProjectById = existingRouting,
            ProjectByName = existingRouting
        };

        var manager = new ProjectRegistryManager(projectDirectory, projectRegistry);
        var result = await manager.RegisterExistingProjectAsync(ProjectAId, "project-a");
        Assert.AreEqual(ProjectRegistrationStatus.AlreadyRegistered, result.Status);
        Assert.AreEqual(existingRouting, result.Routing);
        Assert.AreEqual(Status.Inactive, result.Routing!.Status);
        Assert.AreEqual(0, projectRegistry.CreateCallCount);
    }

    /// <summary>
    /// Verifies that a missing Basic Memory project fails closed before any registry operation is attempted.
    /// </summary>
    [TestMethod]
    public async Task RegisterExistingProjectAsyncRejectsMissingBasicMemoryProject()
    {
        await AssertBasicMemoryValidationFailureAsync(BasicMemoryProjectValidationStatus.NotFound, ProjectRegistrationStatus.BasicMemoryProjectNotFound);
    }

    /// <summary>
    /// Verifies that a Basic Memory project-name mismatch fails closed before any registry operation is attempted.
    /// </summary>
    [TestMethod]
    public async Task RegisterExistingProjectAsyncRejectsBasicMemoryProjectNameMismatch()
    {
        await AssertBasicMemoryValidationFailureAsync(BasicMemoryProjectValidationStatus.NameMismatch, ProjectRegistrationStatus.BasicMemoryProjectNameMismatch);
    }

    /// <summary>
    /// Verifies that a Basic Memory project-identifier mismatch fails closed before any registry operation is attempted.
    /// </summary>
    [TestMethod]
    public async Task RegisterExistingProjectAsyncRejectsBasicMemoryProjectIdMismatch()
    {
        await AssertBasicMemoryValidationFailureAsync(BasicMemoryProjectValidationStatus.IdMismatch, ProjectRegistrationStatus.BasicMemoryProjectIdMismatch);
    }

    /// <summary>
    /// Verifies that conflicting Basic Memory project identities fail closed before any registry operation is attempted.
    /// </summary>
    [TestMethod]
    public async Task RegisterExistingProjectAsyncRejectsBasicMemoryProjectIdentityConflict()
    {
        await AssertBasicMemoryValidationFailureAsync(BasicMemoryProjectValidationStatus.IdentityConflict, ProjectRegistrationStatus.BasicMemoryProjectIdentityConflict);
    }

    /// <summary>
    /// Verifies that an unknown future Basic Memory validation outcome fails closed instead of being treated as registrable.
    /// </summary>
    [TestMethod]
    public async Task RegisterExistingProjectAsyncFailsClosedForUnknownValidationStatus()
    {
        await AssertBasicMemoryValidationFailureAsync((BasicMemoryProjectValidationStatus)int.MaxValue, ProjectRegistrationStatus.BasicMemoryValidationFailed);
    }

    /// <summary>
    /// Verifies that an existing registry entry with the requested identifier but a different name is reported as a name conflict.
    /// </summary>
    [TestMethod]
    public async Task RegisterExistingProjectAsyncReportsRegistryNameMismatch()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry
        {
            ProjectById = new ProjectRouting(ProjectAId, "different-project", Status.Active)
        };

        var manager = new ProjectRegistryManager(projectDirectory, projectRegistry);
        var result = await manager.RegisterExistingProjectAsync(ProjectAId, "project-a");
        Assert.AreEqual(ProjectRegistrationStatus.RegistryProjectNameMismatch, result.Status);
        Assert.IsNull(result.Routing);
        Assert.AreEqual(0, projectRegistry.CreateCallCount);
    }

    /// <summary>
    /// Verifies that an existing registry entry with the requested name but a different identifier is reported as an identifier conflict.
    /// </summary>
    [TestMethod]
    public async Task RegisterExistingProjectAsyncReportsRegistryIdMismatch()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry
        {
            ProjectByName = new ProjectRouting(ProjectBId, "project-a", Status.Active)
        };

        var manager = new ProjectRegistryManager(projectDirectory, projectRegistry);
        var result = await manager.RegisterExistingProjectAsync(ProjectAId, "project-a");
        Assert.AreEqual(ProjectRegistrationStatus.RegistryProjectIdMismatch, result.Status);
        Assert.IsNull(result.Routing);
        Assert.AreEqual(0, projectRegistry.CreateCallCount);
    }

    /// <summary>
    /// Verifies that identifier and name lookups resolving to different registry entries are reported as an identity conflict.
    /// </summary>
    [TestMethod]
    public async Task RegisterExistingProjectAsyncReportsRegistryIdentityConflict()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry
        {
            ProjectById = new ProjectRouting(ProjectAId, "different-project", Status.Active),
            ProjectByName = new ProjectRouting(ProjectBId, "project-a", Status.Active)
        };

        var manager = new ProjectRegistryManager(projectDirectory, projectRegistry);
        var result = await manager.RegisterExistingProjectAsync(ProjectAId, "project-a");
        Assert.AreEqual(ProjectRegistrationStatus.RegistryProjectIdentityConflict, result.Status);
        Assert.IsNull(result.Routing);
        Assert.AreEqual(0, projectRegistry.CreateCallCount);
    }

    /// <summary>
    /// Verifies that a concurrent exact insert is recovered by re-reading registry state and returning an idempotent success.
    /// </summary>
    [TestMethod]
    public async Task RegisterExistingProjectAsyncHandlesConcurrentExactInsert()
    {
        var exactRouting = new ProjectRouting(ProjectAId, "project-a", Status.Active);
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry();
        projectRegistry.CreateHandler = (memoryProjectId, memoryProjectName, status, cancellationToken) =>
        {
            projectRegistry.ProjectById = exactRouting;
            projectRegistry.ProjectByName = exactRouting;
            throw new ProjectRegistryConflictException("A concurrent writer created the routing.");
        };

        var manager = new ProjectRegistryManager(projectDirectory, projectRegistry);
        var result = await manager.RegisterExistingProjectAsync(ProjectAId, "project-a");
        Assert.AreEqual(ProjectRegistrationStatus.AlreadyRegistered, result.Status);
        Assert.AreEqual(exactRouting, result.Routing);
        Assert.AreEqual(1, projectRegistry.CreateCallCount);

        // One lookup pair before CreateAsync and another after the database uniqueness conflict.
        Assert.AreEqual(2, projectRegistry.FindByIdCallCount);
        Assert.AreEqual(2, projectRegistry.FindByNameCallCount);
    }

    /// <summary>
    /// Verifies that an unresolved concurrent registry write conflict fails closed when re-reading reveals no classifiable state.
    /// </summary>
    [TestMethod]
    public async Task RegisterExistingProjectAsyncFailsClosedForUnresolvedWriteConflict()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry
        {
            CreateHandler = (memoryProjectId, memoryProjectName, status, cancellationToken) =>
                throw new ProjectRegistryConflictException("A concurrent registry write failed.")
        };

        var manager = new ProjectRegistryManager(projectDirectory, projectRegistry);
        var result = await manager.RegisterExistingProjectAsync(ProjectAId, "project-a");
        Assert.AreEqual(ProjectRegistrationStatus.RegistryWriteConflict, result.Status);
        Assert.IsNull(result.Routing);
        Assert.AreEqual(1, projectRegistry.CreateCallCount);
        Assert.AreEqual(2, projectRegistry.FindByIdCallCount);
        Assert.AreEqual(2, projectRegistry.FindByNameCallCount);
    }

    /// <summary>
    /// Verifies that registration rejects an empty Basic Memory project identifier before performing external validation.
    /// </summary>
    [TestMethod]
    public async Task RegisterExistingProjectAsyncRejectsEmptyProjectId()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry();
        var manager = new ProjectRegistryManager(projectDirectory, projectRegistry);
        await Assert.ThrowsAsync<ArgumentException>(() => manager.RegisterExistingProjectAsync(Guid.Empty, "project-a"));
        Assert.AreEqual(0, projectDirectory.ValidateCallCount);
        Assert.AreEqual(0, projectRegistry.TotalCallCount);
    }

    /// <summary>
    /// Verifies that registration rejects an empty Basic Memory project name before performing external validation.
    /// </summary>
    [TestMethod]
    public async Task RegisterExistingProjectAsyncRejectsEmptyProjectName()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry();
        var manager = new ProjectRegistryManager(projectDirectory, projectRegistry);
        await Assert.ThrowsAsync<ArgumentException>(() => manager.RegisterExistingProjectAsync(ProjectAId, " "));
        Assert.AreEqual(0, projectDirectory.ValidateCallCount);
        Assert.AreEqual(0, projectRegistry.TotalCallCount);
    }

    /// <summary>
    /// Verifies that an unbound technical context is bound actively to an existing active project routing.
    /// </summary>
    [TestMethod]
    public async Task BindContextAsyncCreatesActiveBinding()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry { ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Active) };
        var manager = new ProjectRegistryManager(projectDirectory, projectRegistry);

        var result = await manager.BindContextAsync(GitContext, ProjectAId);
        Assert.AreEqual(ContextBindingRegistrationStatus.Bound, result.Status);

        Assert.IsNotNull(result.Binding);
        Assert.AreEqual(GitContext, result.Binding.ContextId);
        Assert.AreEqual(ProjectAId, result.Binding.MemoryProjectId);
        Assert.AreEqual(Status.Active, result.Binding.Status);

        Assert.AreEqual(1, projectRegistry.FindByIdCallCount);
        Assert.AreEqual(1, projectRegistry.FindBindingCallCount);
        Assert.AreEqual(1, projectRegistry.CreateBindingCallCount);

        Assert.AreEqual(GitContext, projectRegistry.CreatedContextId);
        Assert.AreEqual(ProjectAId, projectRegistry.CreatedBindingMemoryProjectId);
        Assert.AreEqual(Status.Active, projectRegistry.CreatedBindingStatus);
    }

    /// <summary>
    /// Verifies that an exact existing context binding is treated as idempotently bound without changing its status.
    /// </summary>
    [TestMethod]
    public async Task BindContextAsyncReturnsAlreadyBoundForExactBinding()
    {
        var existingBinding = new ContextBinding(GitContext, ProjectAId, Status.Inactive);
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry { ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Active), Binding = existingBinding };
        var manager = new ProjectRegistryManager(projectDirectory, projectRegistry);

        var result = await manager.BindContextAsync(GitContext, ProjectAId);
        Assert.AreEqual(ContextBindingRegistrationStatus.AlreadyBound, result.Status);
        Assert.AreEqual(existingBinding, result.Binding);
        Assert.AreEqual(Status.Inactive, result.Binding!.Status);
        Assert.AreEqual(0, projectRegistry.CreateBindingCallCount);
    }

    /// <summary>
    /// Verifies that a context cannot be bound when the requested Basic Memory project has no registered project routing.
    /// </summary>
    [TestMethod]
    public async Task BindContextAsyncRejectsMissingProjectRouting()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry();
        var manager = new ProjectRegistryManager(projectDirectory, projectRegistry);

        var result = await manager.BindContextAsync(GitContext, ProjectAId);
        Assert.AreEqual(ContextBindingRegistrationStatus.ProjectRoutingNotFound, result.Status);
        Assert.IsNull(result.Binding);
        Assert.AreEqual(1, projectRegistry.FindByIdCallCount);
        Assert.AreEqual(0, projectRegistry.FindBindingCallCount);
        Assert.AreEqual(0, projectRegistry.CreateBindingCallCount);
    }

    /// <summary>
    /// Verifies that a context cannot be bound to an inactive project routing.
    /// </summary>
    [TestMethod]
    public async Task BindContextAsyncRejectsInactiveProjectRouting()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry { ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Inactive) };
        var manager = new ProjectRegistryManager(projectDirectory, projectRegistry);

        var result = await manager.BindContextAsync(GitContext, ProjectAId);
        Assert.AreEqual(ContextBindingRegistrationStatus.ProjectRoutingInactive, result.Status);
        Assert.IsNull(result.Binding);
        Assert.AreEqual(0, projectRegistry.FindBindingCallCount);
        Assert.AreEqual(0, projectRegistry.CreateBindingCallCount);
    }

    /// <summary>
    /// Verifies that an existing context binding to another project is reported as a binding conflict.
    /// </summary>
    [TestMethod]
    public async Task BindContextAsyncReportsDifferentProjectConflict()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry
        {
            ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Active),
            Binding = new ContextBinding(GitContext, ProjectBId, Status.Active)
        };

        var manager = new ProjectRegistryManager(projectDirectory, projectRegistry);
        var result = await manager.BindContextAsync(GitContext, ProjectAId);
        Assert.AreEqual(ContextBindingRegistrationStatus.ContextAlreadyBoundToDifferentProject, result.Status);
        Assert.IsNull(result.Binding);
        Assert.AreEqual(0, projectRegistry.CreateBindingCallCount);
    }

    /// <summary>
    /// Verifies that a concurrent exact binding insert is recovered by re-reading registry state and returning an idempotent success.
    /// </summary>
    [TestMethod]
    public async Task BindContextAsyncHandlesConcurrentExactInsert()
    {
        var concurrentBinding = new ContextBinding(GitContext, ProjectAId, Status.Active);
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry { ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Active) };
        projectRegistry.CreateBindingHandler = (contextId, memoryProjectId, status, cancellationToken) =>
        {
            projectRegistry.Binding = concurrentBinding;
            throw new ProjectRegistryConflictException("A concurrent writer created the binding.");
        };

        var manager = new ProjectRegistryManager(projectDirectory, projectRegistry);
        var result = await manager.BindContextAsync(GitContext, ProjectAId);
        Assert.AreEqual(ContextBindingRegistrationStatus.AlreadyBound, result.Status);
        Assert.AreEqual(concurrentBinding, result.Binding);
        Assert.AreEqual(1, projectRegistry.CreateBindingCallCount);

        // One lookup before CreateBindingAsync and one after the database uniqueness conflict.
        Assert.AreEqual(2, projectRegistry.FindBindingCallCount);
    }

    /// <summary>
    /// Verifies that an unresolved concurrent binding write conflict fails closed when re-reading reveals no classifiable state.
    /// </summary>
    [TestMethod]
    public async Task BindContextAsyncFailsClosedForUnresolvedWriteConflict()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry
        {
            ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Active),
            CreateBindingHandler = (contextId, memoryProjectId, status, cancellationToken) =>
                throw new ProjectRegistryConflictException("A concurrent binding write failed.")
        };

        var manager = new ProjectRegistryManager(projectDirectory, projectRegistry);
        var result = await manager.BindContextAsync(GitContext, ProjectAId);
        Assert.AreEqual(ContextBindingRegistrationStatus.RegistryWriteConflict, result.Status);

        Assert.IsNull(result.Binding);
        Assert.AreEqual(1, projectRegistry.CreateBindingCallCount);
        Assert.AreEqual(2, projectRegistry.FindBindingCallCount);
    }

    /// <summary>
    /// Verifies that binding rejects an empty Basic Memory project identifier before accessing registry state.
    /// </summary>
    [TestMethod]
    public async Task BindContextAsyncRejectsEmptyProjectId()
    {
        var projectDirectory = CreateProjectDirectory(
            BasicMemoryProjectValidationStatus.ExactMatch);

        var projectRegistry = new StubProjectRegistry();

        var manager = new ProjectRegistryManager(projectDirectory, projectRegistry);
        await Assert.ThrowsAsync<ArgumentException>(() => manager.BindContextAsync(GitContext, Guid.Empty));
        Assert.AreEqual(0, projectRegistry.TotalCallCount);
    }

    #endregion

    #region Private Methods

    private static StubBasicMemoryProjectDirectory CreateProjectDirectory(BasicMemoryProjectValidationStatus status)
    {
        return new StubBasicMemoryProjectDirectory(new BasicMemoryProjectValidationResult(status, null, null));
    }

    private static async Task AssertBasicMemoryValidationFailureAsync(BasicMemoryProjectValidationStatus validationStatus, ProjectRegistrationStatus expectedRegistrationStatus)
    {
        var projectDirectory = CreateProjectDirectory(validationStatus);
        var projectRegistry = new StubProjectRegistry();
        var manager = new ProjectRegistryManager(projectDirectory, projectRegistry);
        var result = await manager.RegisterExistingProjectAsync(ProjectAId, "project-a");
        Assert.AreEqual(expectedRegistrationStatus, result.Status);
        Assert.IsNull(result.Routing);
        Assert.AreEqual(1, projectDirectory.ValidateCallCount);
        Assert.AreEqual(0, projectRegistry.TotalCallCount);
    }

    private static ContextId ParseContextId(string value)
    {
        if (!ContextId.TryParse(value, out var contextId))
            throw new InvalidOperationException($"The test context identifier '{value}' is invalid.");

        return contextId;
    }

    #endregion

    #region Test Classes

    private sealed class StubBasicMemoryProjectDirectory : IBasicMemoryProjectDirectory
    {
        #region Private Fields

        private readonly BasicMemoryProjectValidationResult _validationResult;

        #endregion

        #region Constructors

        public StubBasicMemoryProjectDirectory(BasicMemoryProjectValidationResult validationResult)
        {
            _validationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
        }

        #endregion

        #region Properties

        public int ValidateCallCount { get; private set; }

        #endregion

        #region Public Methods

        public Task<IReadOnlyList<BasicMemoryProjectInfo>> ListAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<BasicMemoryProjectValidationResult> ValidateAsync(Guid memoryProjectId, string memoryProjectName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateCallCount++;
            return Task.FromResult(_validationResult);
        }

        #endregion
    }

    private sealed class StubProjectRegistry : IProjectRegistry
    {
        #region Properties

        public ProjectRouting? ProjectById { get; set; }

        public ProjectRouting? ProjectByName { get; set; }

        public Func<Guid, string, Status, CancellationToken, Task<ProjectRouting>>? CreateHandler { get; set; }

        public ContextBinding? Binding { get; set; }

        public Func<ContextId, Guid, Status, CancellationToken, Task<ContextBinding>>? CreateBindingHandler { get; set; }

        public int FindBindingCallCount { get; private set; }

        public int CreateBindingCallCount { get; private set; }

        public ContextId? CreatedContextId { get; private set; }

        public Guid? CreatedBindingMemoryProjectId { get; private set; }

        public Status? CreatedBindingStatus { get; private set; }

        public int FindByIdCallCount { get; private set; }

        public int FindByNameCallCount { get; private set; }

        public int CreateCallCount { get; private set; }

        public int TotalCallCount => FindByIdCallCount + FindByNameCallCount + CreateCallCount + FindBindingCallCount + CreateBindingCallCount;

        public Guid? CreatedMemoryProjectId { get; private set; }

        public string? CreatedMemoryProjectName { get; private set; }

        public Status? CreatedStatus { get; private set; }

        #endregion

        #region Public Methods

        public Task<ProjectRoute?> FindRouteAsync(ContextId contextId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProjectRouting?> FindByMemoryProjectIdAsync(Guid memoryProjectId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FindByIdCallCount++;
            return Task.FromResult(ProjectById);
        }

        public Task<ProjectRouting?> FindByMemoryProjectNameAsync(string memoryProjectName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FindByNameCallCount++;
            return Task.FromResult(ProjectByName);
        }

        public Task<ProjectRouting> CreateAsync(Guid memoryProjectId, string memoryProjectName, Status status, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreateCallCount++;
            CreatedMemoryProjectId = memoryProjectId;
            CreatedMemoryProjectName = memoryProjectName;
            CreatedStatus = status;

            return CreateHandler != null ?
                       CreateHandler(memoryProjectId, memoryProjectName, status, cancellationToken) :
                       Task.FromResult(new ProjectRouting(memoryProjectId, memoryProjectName, status));
        }

        public Task<ContextBinding?> FindBindingAsync(ContextId contextId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FindBindingCallCount++;
            return Task.FromResult(Binding);
        }

        public Task<ContextBinding> CreateBindingAsync(ContextId contextId, Guid memoryProjectId, Status status, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CreateBindingCallCount++;
            CreatedContextId = contextId;
            CreatedBindingMemoryProjectId = memoryProjectId;
            CreatedBindingStatus = status;
            return CreateBindingHandler != null ?
                       CreateBindingHandler(contextId, memoryProjectId, status, cancellationToken) :
                       Task.FromResult(new ContextBinding(contextId, memoryProjectId, status));
        }

        #endregion
    }

    #endregion
}
