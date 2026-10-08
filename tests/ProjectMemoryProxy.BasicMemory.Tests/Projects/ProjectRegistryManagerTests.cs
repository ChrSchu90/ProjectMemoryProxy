namespace ProjectMemoryProxy.BasicMemory.Tests.Projects;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectMemoryProxy.BasicMemory.Projects;
using ProjectMemoryProxy.BasicMemory.Projects.ControlPlane;
using ProjectMemoryProxy.BasicMemory.Projects.Directory;
using ProjectMemoryProxy.BasicMemory.Projects.Lifecycle;
using ProjectMemoryProxy.Core.Routing;

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
    private static readonly string ProjectAPath = Path.Combine(Path.GetTempPath(), "project-memory-proxy-tests", "project-a");
    private static readonly string ProjectBPath = Path.Combine(Path.GetTempPath(), "project-memory-proxy-tests", "project-b");

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
        var manager = CreateManager(projectDirectory, projectRegistry);

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

        var manager = CreateManager(projectDirectory, projectRegistry);
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

        var manager = CreateManager(projectDirectory, projectRegistry);
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

        var manager = CreateManager(projectDirectory, projectRegistry);
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

        var manager = CreateManager(projectDirectory, projectRegistry);
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

        var manager = CreateManager(projectDirectory, projectRegistry);
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

        var manager = CreateManager(projectDirectory, projectRegistry);
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
        var manager = CreateManager(projectDirectory, projectRegistry);
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
        var manager = CreateManager(projectDirectory, projectRegistry);
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
        var manager = CreateManager(projectDirectory, projectRegistry);

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
        var manager = CreateManager(projectDirectory, projectRegistry);

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
        var manager = CreateManager(projectDirectory, projectRegistry);

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
        var manager = CreateManager(projectDirectory, projectRegistry);

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

        var manager = CreateManager(projectDirectory, projectRegistry);
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

        var manager = CreateManager(projectDirectory, projectRegistry);
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

        var manager = CreateManager(projectDirectory, projectRegistry);
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

        var manager = CreateManager(projectDirectory, projectRegistry);
        await Assert.ThrowsAsync<ArgumentException>(() => manager.BindContextAsync(GitContext, Guid.Empty));
        Assert.AreEqual(0, projectRegistry.TotalCallCount);
    }

    /// <summary>
    /// Verifies that an active project routing is deactivated without revalidating the Basic Memory project.
    /// </summary>
    [TestMethod]
    public async Task DeactivateProjectAsyncDeactivatesActiveRouting()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry { ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Active) };
        var manager = CreateManager(projectDirectory, projectRegistry);
        var result = await manager.DeactivateProjectAsync(ProjectAId);
        Assert.AreEqual(ProjectRoutingStatusChangeStatus.Updated, result.Status);

        Assert.IsNotNull(result.Routing);
        Assert.AreEqual(ProjectAId, result.Routing.MemoryProjectId);
        Assert.AreEqual("project-a", result.Routing.MemoryProjectName);
        Assert.AreEqual(Status.Inactive, result.Routing.Status);

        Assert.AreEqual(0, projectDirectory.ValidateCallCount);
        Assert.AreEqual(1, projectRegistry.TryUpdateProjectStatusCallCount);
        Assert.AreEqual(ProjectAId, projectRegistry.UpdatedProjectMemoryProjectId);
        Assert.AreEqual(Status.Active, projectRegistry.ExpectedProjectStatus);
        Assert.AreEqual(Status.Inactive, projectRegistry.NewProjectStatus);
    }

    /// <summary>
    /// Verifies that deactivating an already inactive project routing is idempotent and performs no persistence update.
    /// </summary>
    [TestMethod]
    public async Task DeactivateProjectAsyncReturnsAlreadyInactive()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var existing = new ProjectRouting(ProjectAId, "project-a", Status.Inactive);
        var projectRegistry = new StubProjectRegistry { ProjectById = existing };
        var manager = CreateManager(projectDirectory, projectRegistry);

        var result = await manager.DeactivateProjectAsync(ProjectAId);
        Assert.AreEqual(ProjectRoutingStatusChangeStatus.AlreadyInRequestedState, result.Status);
        Assert.AreEqual(existing, result.Routing);
        Assert.AreEqual(0, projectDirectory.ValidateCallCount);
        Assert.AreEqual(0, projectRegistry.TryUpdateProjectStatusCallCount);
    }

    /// <summary>
    /// Verifies that an inactive project routing is reactivated only after its exact Basic Memory identity is revalidated.
    /// </summary>
    [TestMethod]
    public async Task ReactivateProjectAsyncValidatesAndActivatesRouting()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry { ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Inactive) };
        var manager = CreateManager(projectDirectory, projectRegistry);

        var result = await manager.ReactivateProjectAsync(ProjectAId);
        Assert.AreEqual(ProjectRoutingStatusChangeStatus.Updated, result.Status);

        Assert.IsNotNull(result.Routing);
        Assert.AreEqual(Status.Active, result.Routing.Status);

        Assert.AreEqual(1, projectDirectory.ValidateCallCount);
        Assert.AreEqual(ProjectAId, projectDirectory.ValidatedMemoryProjectId);
        Assert.AreEqual("project-a", projectDirectory.ValidatedMemoryProjectName);

        Assert.AreEqual(1, projectRegistry.TryUpdateProjectStatusCallCount);
        Assert.AreEqual(Status.Inactive, projectRegistry.ExpectedProjectStatus);
        Assert.AreEqual(Status.Active, projectRegistry.NewProjectStatus);
    }

    /// <summary>
    /// Verifies that project reactivation fails closed when the registered Basic Memory project no longer exists.
    /// </summary>
    [TestMethod]
    public async Task ReactivateProjectAsyncRejectsMissingBasicMemoryProject()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.NotFound);
        var projectRegistry = new StubProjectRegistry
        {
            ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Inactive)
        };

        var manager = CreateManager(projectDirectory, projectRegistry);
        var result = await manager.ReactivateProjectAsync(ProjectAId);
        Assert.AreEqual(ProjectRoutingStatusChangeStatus.BasicMemoryProjectNotFound, result.Status);
        Assert.IsNull(result.Routing);
        Assert.AreEqual(1, projectDirectory.ValidateCallCount);
        Assert.AreEqual(0, projectRegistry.TryUpdateProjectStatusCallCount);
    }

    /// <summary>
    /// Verifies that project reactivation fails closed when the persisted Basic Memory project name no longer matches its identifier.
    /// </summary>
    [TestMethod]
    public async Task ReactivateProjectAsyncRejectsBasicMemoryNameMismatch()
    {
        await AssertProjectReactivationValidationFailureAsync(BasicMemoryProjectValidationStatus.NameMismatch, ProjectRoutingStatusChangeStatus.BasicMemoryProjectNameMismatch);
    }

    /// <summary>
    /// Verifies that project reactivation fails closed when the persisted Basic Memory project identifier no longer matches its name.
    /// </summary>
    [TestMethod]
    public async Task ReactivateProjectAsyncRejectsBasicMemoryIdMismatch()
    {
        await AssertProjectReactivationValidationFailureAsync(BasicMemoryProjectValidationStatus.IdMismatch, ProjectRoutingStatusChangeStatus.BasicMemoryProjectIdMismatch);
    }

    /// <summary>
    /// Verifies that project reactivation fails closed when the persisted Basic Memory identity resolves to conflicting projects.
    /// </summary>
    [TestMethod]
    public async Task ReactivateProjectAsyncRejectsBasicMemoryIdentityConflict()
    {
        await AssertProjectReactivationValidationFailureAsync(BasicMemoryProjectValidationStatus.IdentityConflict, ProjectRoutingStatusChangeStatus.BasicMemoryProjectIdentityConflict);
    }

    /// <summary>
    /// Verifies that a concurrent project deactivation is recovered by re-reading registry state and returning an idempotent success.
    /// </summary>
    [TestMethod]
    public async Task DeactivateProjectAsyncHandlesConcurrentExactUpdate()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry { ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Active) };
        projectRegistry.TryUpdateProjectStatusHandler = (memoryProjectId, expectedStatus, newStatus, cancellationToken) =>
        {
            projectRegistry.ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Inactive);
            return Task.FromResult(false);
        };

        var manager = CreateManager(projectDirectory, projectRegistry);
        var result = await manager.DeactivateProjectAsync(ProjectAId);
        Assert.AreEqual(ProjectRoutingStatusChangeStatus.AlreadyInRequestedState, result.Status);
        Assert.IsNotNull(result.Routing);
        Assert.AreEqual(Status.Inactive, result.Routing.Status);
        Assert.AreEqual(2, projectRegistry.FindByIdCallCount);
        Assert.AreEqual(1, projectRegistry.TryUpdateProjectStatusCallCount);
    }

    /// <summary>
    /// Verifies that an unresolved concurrent project status change fails closed when the persisted state remains unexpected.
    /// </summary>
    [TestMethod]
    public async Task DeactivateProjectAsyncFailsClosedForConcurrentConflict()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry
        {
            ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Active),
            TryUpdateProjectStatusHandler = (memoryProjectId, expectedStatus, newStatus, cancellationToken) => Task.FromResult(false)
        };

        var manager = CreateManager(projectDirectory, projectRegistry);
        var result = await manager.DeactivateProjectAsync(ProjectAId);
        Assert.AreEqual(ProjectRoutingStatusChangeStatus.RegistryWriteConflict, result.Status);
        Assert.IsNull(result.Routing);
        Assert.AreEqual(2, projectRegistry.FindByIdCallCount);
    }

    /// <summary>
    /// Verifies that an active context binding is deactivated without changing or inspecting its target project routing.
    /// </summary>
    [TestMethod]
    public async Task DeactivateContextAsyncDeactivatesActiveBinding()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry { Binding = new ContextBinding(GitContext, ProjectAId, Status.Active) };
        var manager = CreateManager(projectDirectory, projectRegistry);
        var result = await manager.DeactivateContextAsync(GitContext);
        Assert.AreEqual(ContextBindingStatusChangeStatus.Updated, result.Status);

        Assert.IsNotNull(result.Binding);
        Assert.AreEqual(Status.Inactive, result.Binding.Status);

        Assert.AreEqual(0, projectRegistry.FindByIdCallCount);
        Assert.AreEqual(1, projectRegistry.TryUpdateBindingStatusCallCount);
        Assert.AreEqual(GitContext, projectRegistry.UpdatedBindingContextId);
        Assert.AreEqual(Status.Active, projectRegistry.ExpectedBindingStatus);
        Assert.AreEqual(Status.Inactive, projectRegistry.NewBindingStatus);
    }

    /// <summary>
    /// Verifies that an inactive context binding is reactivated when its target project routing is active.
    /// </summary>
    [TestMethod]
    public async Task ReactivateContextAsyncActivatesBindingForActiveProject()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry
        {
            Binding = new ContextBinding(GitContext, ProjectAId, Status.Inactive),
            ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Active)
        };

        var manager = CreateManager(projectDirectory, projectRegistry);
        var result = await manager.ReactivateContextAsync(GitContext);
        Assert.AreEqual(ContextBindingStatusChangeStatus.Updated, result.Status);
        Assert.IsNotNull(result.Binding);
        Assert.AreEqual(Status.Active, result.Binding.Status);
        Assert.AreEqual(1, projectRegistry.FindByIdCallCount);
        Assert.AreEqual(1, projectRegistry.TryUpdateBindingStatusCallCount);
        Assert.AreEqual(Status.Inactive, projectRegistry.ExpectedBindingStatus);
        Assert.AreEqual(Status.Active, projectRegistry.NewBindingStatus);
    }

    /// <summary>
    /// Verifies that a context binding cannot be reactivated while its target project routing is inactive.
    /// </summary>
    [TestMethod]
    public async Task ReactivateContextAsyncRejectsInactiveProjectRouting()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry
        {
            Binding = new ContextBinding(GitContext, ProjectAId, Status.Inactive),
            ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Inactive)
        };

        var manager = CreateManager(projectDirectory, projectRegistry);
        var result = await manager.ReactivateContextAsync(GitContext);
        Assert.AreEqual(ContextBindingStatusChangeStatus.ProjectRoutingInactive, result.Status);
        Assert.IsNull(result.Binding);
        Assert.AreEqual(0, projectRegistry.TryUpdateBindingStatusCallCount);
    }

    /// <summary>
    /// Verifies that a context binding cannot be reactivated when its target project routing no longer exists.
    /// </summary>
    [TestMethod]
    public async Task ReactivateContextAsyncRejectsMissingProjectRouting()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry
        {
            Binding = new ContextBinding(GitContext, ProjectAId, Status.Inactive)
        };

        var manager = CreateManager(projectDirectory, projectRegistry);
        var result = await manager.ReactivateContextAsync(GitContext);
        Assert.AreEqual(ContextBindingStatusChangeStatus.ProjectRoutingNotFound, result.Status);
        Assert.IsNull(result.Binding);
        Assert.AreEqual(0, projectRegistry.TryUpdateBindingStatusCallCount);
    }

    /// <summary>
    /// Verifies that a concurrent context reactivation is recovered by re-reading registry state and returning an idempotent success.
    /// </summary>
    [TestMethod]
    public async Task ReactivateContextAsyncHandlesConcurrentExactUpdate()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry
        {
            Binding = new ContextBinding(GitContext, ProjectAId, Status.Inactive),
            ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Active)
        };

        projectRegistry.TryUpdateBindingStatusHandler = (contextId, expectedStatus, newStatus, cancellationToken) =>
        {
            projectRegistry.Binding = new ContextBinding(GitContext, ProjectAId, Status.Active);
            return Task.FromResult(false);
        };

        var manager = CreateManager(projectDirectory, projectRegistry);
        var result = await manager.ReactivateContextAsync(GitContext);
        Assert.AreEqual(ContextBindingStatusChangeStatus.AlreadyInRequestedState, result.Status);
        Assert.IsNotNull(result.Binding);
        Assert.AreEqual(Status.Active, result.Binding.Status);
        Assert.AreEqual(2, projectRegistry.FindBindingCallCount);
        Assert.AreEqual(1, projectRegistry.TryUpdateBindingStatusCallCount);
    }

    /// <summary>
    /// Verifies that an unresolved concurrent context status change fails closed when the persisted state remains unexpected.
    /// </summary>
    [TestMethod]
    public async Task ReactivateContextAsyncFailsClosedForConcurrentConflict()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry
        {
            Binding = new ContextBinding(GitContext, ProjectAId, Status.Inactive),
            ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Active),
            TryUpdateBindingStatusHandler = (contextId, expectedStatus, newStatus, cancellationToken) => Task.FromResult(false)
        };

        var manager = CreateManager(projectDirectory, projectRegistry);
        var result = await manager.ReactivateContextAsync(GitContext);
        Assert.AreEqual(ContextBindingStatusChangeStatus.RegistryWriteConflict, result.Status);
        Assert.IsNull(result.Binding);
        Assert.AreEqual(2, projectRegistry.FindBindingCallCount);
    }

    /// <summary>
    /// Verifies that explicit unbind removes the exact context binding without inspecting its status or target project routing.
    /// </summary>
    [TestMethod]
    public async Task UnbindContextAsyncRemovesBinding()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry { Binding = new ContextBinding(GitContext, ProjectAId, Status.Inactive) };
        var manager = CreateManager(projectDirectory, projectRegistry);

        var result = await manager.UnbindContextAsync(GitContext);
        Assert.AreEqual(ContextBindingRemovalStatus.Unbound, result.Status);
        Assert.AreEqual(1, projectRegistry.TryRemoveBindingCallCount);
        Assert.AreEqual(GitContext, projectRegistry.RemovedBindingContextId);
        Assert.AreEqual(0, projectRegistry.FindBindingCallCount);
        Assert.AreEqual(0, projectRegistry.FindByIdCallCount);
        Assert.AreEqual(0, projectDirectory.ValidateCallCount);
    }

    /// <summary>
    /// Verifies that unbinding an already unbound context is treated as an idempotent expected outcome after re-reading registry state.
    /// </summary>
    [TestMethod]
    public async Task UnbindContextAsyncReturnsAlreadyUnboundForMissingBinding()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry
        {
            TryRemoveBindingHandler = (contextId, cancellationToken) => Task.FromResult(false)
        };

        var manager = CreateManager(projectDirectory, projectRegistry);
        var result = await manager.UnbindContextAsync(GitContext);
        Assert.AreEqual(ContextBindingRemovalStatus.AlreadyUnbound, result.Status);
        Assert.AreEqual(1, projectRegistry.TryRemoveBindingCallCount);
        Assert.AreEqual(1, projectRegistry.FindBindingCallCount);
    }

    /// <summary>
    /// Verifies that a concurrent exact unbind is recovered as an idempotent result when the binding has disappeared on re-read.
    /// </summary>
    [TestMethod]
    public async Task UnbindContextAsyncHandlesConcurrentRemoval()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry { Binding = new ContextBinding(GitContext, ProjectAId, Status.Active) };
        projectRegistry.TryRemoveBindingHandler = (contextId, cancellationToken) =>
        {
            projectRegistry.Binding = null;
            return Task.FromResult(false);
        };

        var manager = CreateManager(projectDirectory, projectRegistry);
        var result = await manager.UnbindContextAsync(GitContext);
        Assert.AreEqual(ContextBindingRemovalStatus.AlreadyUnbound, result.Status);
        Assert.AreEqual(1, projectRegistry.TryRemoveBindingCallCount);
        Assert.AreEqual(1, projectRegistry.FindBindingCallCount);
    }

    /// <summary>
    /// Verifies that a failed unbind fails closed when an exact binding still exists after the persistence operation.
    /// </summary>
    [TestMethod]
    public async Task UnbindContextAsyncFailsClosedForUnresolvedWriteConflict()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectRegistry = new StubProjectRegistry
        {
            Binding = new ContextBinding(GitContext, ProjectAId, Status.Active),
            TryRemoveBindingHandler = (contextId, cancellationToken) => Task.FromResult(false)
        };

        var manager = CreateManager(projectDirectory, projectRegistry);
        var result = await manager.UnbindContextAsync(GitContext);
        Assert.AreEqual(ContextBindingRemovalStatus.RegistryWriteConflict, result.Status);
        Assert.AreEqual(1, projectRegistry.TryRemoveBindingCallCount);
        Assert.AreEqual(1, projectRegistry.FindBindingCallCount);
    }

    /// <summary>
    /// Verifies that creating a new Basic Memory project validates its returned identity and registers an active project routing.
    /// </summary>
    [TestMethod]
    public async Task CreateProjectAsyncCreatesAndRegistersProject()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectLifecycle = CreateProjectLifecycle(BasicMemoryProjectCreationStatus.Created, ProjectAPath);
        var projectRegistry = new StubProjectRegistry();
        var manager = CreateManager(projectDirectory, projectRegistry, projectLifecycle);
        var result = await manager.CreateProjectAsync("project-a", ProjectAPath);
        Assert.AreEqual(ProjectCreationStatus.Created, result.Status);

        Assert.IsNotNull(result.Routing);
        Assert.AreEqual(ProjectAId, result.Routing.MemoryProjectId);
        Assert.AreEqual("project-a", result.Routing.MemoryProjectName);
        Assert.AreEqual(Status.Active, result.Routing.Status);

        Assert.AreEqual(1, projectLifecycle.CreateCallCount);
        Assert.AreEqual("project-a", projectLifecycle.CreatedMemoryProjectName);
        Assert.AreEqual(ProjectAPath, projectLifecycle.CreatedMemoryProjectPath);

        Assert.AreEqual(1, projectDirectory.ValidateCallCount);
        Assert.AreEqual(ProjectAId, projectDirectory.ValidatedMemoryProjectId);
        Assert.AreEqual("project-a", projectDirectory.ValidatedMemoryProjectName);

        Assert.AreEqual(1, projectRegistry.CreateCallCount);
        Assert.AreEqual(ProjectAId, projectRegistry.CreatedMemoryProjectId);
        Assert.AreEqual("project-a", projectRegistry.CreatedMemoryProjectName);
        Assert.AreEqual(Status.Active, projectRegistry.CreatedStatus);
    }

    /// <summary>
    /// Verifies that an existing unregistered Basic Memory project at the requested path is adopted as recovery from a previous or concurrent create.
    /// </summary>
    [TestMethod]
    public async Task CreateProjectAsyncRecoversExistingProject()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectLifecycle = CreateProjectLifecycle(BasicMemoryProjectCreationStatus.AlreadyExists, ProjectAPath);

        var projectRegistry = new StubProjectRegistry();
        var manager = CreateManager(projectDirectory, projectRegistry, projectLifecycle);
        var result = await manager.CreateProjectAsync("project-a", ProjectAPath);
        Assert.AreEqual(ProjectCreationStatus.Recovered, result.Status);

        Assert.IsNotNull(result.Routing);
        Assert.AreEqual(ProjectAId, result.Routing.MemoryProjectId);
        Assert.AreEqual("project-a", result.Routing.MemoryProjectName);
        Assert.AreEqual(Status.Active, result.Routing.Status);

        Assert.AreEqual(1, projectLifecycle.CreateCallCount);
        Assert.AreEqual(1, projectDirectory.ValidateCallCount);
        Assert.AreEqual(1, projectRegistry.CreateCallCount);
    }

    /// <summary>
    /// Verifies that an exact existing registry routing makes project creation idempotent without invoking Basic Memory create or changing routing status.
    /// </summary>
    [TestMethod]
    public async Task CreateProjectAsyncReturnsAlreadyRegisteredWithoutCreatingProject()
    {
        var existingRouting = new ProjectRouting(ProjectAId, "project-a", Status.Inactive);
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectLifecycle = new StubBasicMemoryProjectLifecycle();
        var projectRegistry = new StubProjectRegistry
        {
            ProjectByName = existingRouting
        };

        var manager = CreateManager(projectDirectory, projectRegistry, projectLifecycle);
        var result = await manager.CreateProjectAsync("project-a", ProjectAPath);
        Assert.AreEqual(ProjectCreationStatus.AlreadyRegistered, result.Status);
        Assert.AreEqual(existingRouting, result.Routing);
        Assert.AreEqual(Status.Inactive, result.Routing!.Status);

        Assert.AreEqual(0, projectLifecycle.CreateCallCount);
        Assert.AreEqual(1, projectDirectory.ValidateCallCount);
        Assert.AreEqual(ProjectAId, projectDirectory.ValidatedMemoryProjectId);
        Assert.AreEqual("project-a", projectDirectory.ValidatedMemoryProjectName);
        Assert.AreEqual(0, projectRegistry.CreateCallCount);
    }

    /// <summary>
    /// Verifies that repeated creation preserves the same validated project UUID and name without using a different path to create another project.
    /// </summary>
    [TestMethod]
    public async Task CreateProjectAsyncKeepsExistingIdentityWhenRequestedPathDiffers()
    {
        var existingRouting = new ProjectRouting(ProjectAId, "project-a", Status.Active);
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectLifecycle = new StubBasicMemoryProjectLifecycle();
        var projectRegistry = new StubProjectRegistry { ProjectByName = existingRouting };
        var manager = CreateManager(projectDirectory, projectRegistry, projectLifecycle);

        var result = await manager.CreateProjectAsync("project-a", ProjectBPath);

        Assert.AreEqual(ProjectCreationStatus.AlreadyRegistered, result.Status);
        Assert.AreEqual(existingRouting, result.Routing);
        Assert.AreEqual(0, projectLifecycle.CreateCallCount);
        Assert.AreEqual(0, projectRegistry.CreateCallCount);
        Assert.AreEqual(ProjectAId, projectDirectory.ValidatedMemoryProjectId);
        Assert.AreEqual("project-a", projectDirectory.ValidatedMemoryProjectName);
    }

    /// <summary>
    /// Verifies that a registered project name is not silently adopted when Basic Memory now assigns it a different GUID.
    /// </summary>
    [TestMethod]
    public async Task CreateProjectAsyncRejectsChangedGuidForRegisteredName()
    {
        var existingRouting = new ProjectRouting(ProjectAId, "project-a", Status.Active);
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.IdMismatch);
        var projectLifecycle = new StubBasicMemoryProjectLifecycle();
        var projectRegistry = new StubProjectRegistry { ProjectByName = existingRouting };
        var manager = CreateManager(projectDirectory, projectRegistry, projectLifecycle);

        var result = await manager.CreateProjectAsync("project-a", ProjectAPath);

        Assert.AreEqual(ProjectCreationStatus.BasicMemoryProjectIdMismatch, result.Status);
        Assert.IsNull(result.Routing);
        Assert.AreEqual(0, projectLifecycle.CreateCallCount);
        Assert.AreEqual(0, projectRegistry.CreateCallCount);
    }

    /// <summary>
    /// Verifies that an existing unregistered Basic Memory project is not adopted when its filesystem path differs from the requested create path.
    /// </summary>
    [TestMethod]
    public async Task CreateProjectAsyncRejectsExistingProjectAtDifferentPath()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectLifecycle = CreateProjectLifecycle(BasicMemoryProjectCreationStatus.AlreadyExists, ProjectBPath);
        var projectRegistry = new StubProjectRegistry();
        var manager = CreateManager(projectDirectory, projectRegistry, projectLifecycle);
        var result = await manager.CreateProjectAsync("project-a", ProjectAPath);
        Assert.AreEqual(ProjectCreationStatus.BasicMemoryProjectPathMismatch, result.Status);
        Assert.IsNull(result.Routing);
        Assert.AreEqual(1, projectLifecycle.CreateCallCount);
        Assert.AreEqual(0, projectDirectory.ValidateCallCount);
        Assert.AreEqual(0, projectRegistry.CreateCallCount);
    }

    /// <summary>
    /// Verifies that project creation fails closed when Basic Memory returns a project name that differs from the exact requested name.
    /// </summary>
    [TestMethod]
    public async Task CreateProjectAsyncRejectsReturnedProjectNameMismatch()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectLifecycle = CreateProjectLifecycle(BasicMemoryProjectCreationStatus.Created, ProjectAPath, memoryProjectName: "PROJECT-A");
        var projectRegistry = new StubProjectRegistry();
        var manager = CreateManager(projectDirectory, projectRegistry, projectLifecycle);
        var result = await manager.CreateProjectAsync("project-a", ProjectAPath);
        Assert.AreEqual(ProjectCreationStatus.BasicMemoryProjectNameMismatch, result.Status);
        Assert.IsNull(result.Routing);
        Assert.AreEqual(0, projectDirectory.ValidateCallCount);
        Assert.AreEqual(0, projectRegistry.CreateCallCount);
    }

    /// <summary>
    /// Verifies that a created project fails closed when its post-create Basic Memory identity cannot be found.
    /// </summary>
    [TestMethod]
    public async Task CreateProjectAsyncRejectsMissingPostCreateProject()
    {
        await AssertCreateProjectValidationFailureAsync(BasicMemoryProjectValidationStatus.NotFound, ProjectCreationStatus.BasicMemoryProjectNotFound);
    }

    /// <summary>
    /// Verifies that a created project fails closed when post-create validation reports a name mismatch.
    /// </summary>
    [TestMethod]
    public async Task CreateProjectAsyncRejectsPostCreateNameMismatch()
    {
        await AssertCreateProjectValidationFailureAsync(BasicMemoryProjectValidationStatus.NameMismatch, ProjectCreationStatus.BasicMemoryProjectNameMismatch);
    }

    /// <summary>
    /// Verifies that a created project fails closed when post-create validation reports an identifier mismatch.
    /// </summary>
    [TestMethod]
    public async Task CreateProjectAsyncRejectsPostCreateIdMismatch()
    {
        await AssertCreateProjectValidationFailureAsync(BasicMemoryProjectValidationStatus.IdMismatch, ProjectCreationStatus.BasicMemoryProjectIdMismatch);
    }

    /// <summary>
    /// Verifies that a created project fails closed when post-create validation reports conflicting identities.
    /// </summary>
    [TestMethod]
    public async Task CreateProjectAsyncRejectsPostCreateIdentityConflict()
    {
        await AssertCreateProjectValidationFailureAsync(BasicMemoryProjectValidationStatus.IdentityConflict, ProjectCreationStatus.BasicMemoryProjectIdentityConflict);
    }

    /// <summary>
    /// Verifies that an unknown post-create validation outcome fails closed instead of registering the project.
    /// </summary>
    [TestMethod]
    public async Task CreateProjectAsyncFailsClosedForUnknownPostCreateValidationStatus()
    {
        await AssertCreateProjectValidationFailureAsync((BasicMemoryProjectValidationStatus)int.MaxValue, ProjectCreationStatus.BasicMemoryValidationFailed);
    }

    /// <summary>
    /// Verifies that a concurrent exact registry insert after Basic Memory project creation is recovered as an idempotent registered result.
    /// </summary>
    [TestMethod]
    public async Task CreateProjectAsyncHandlesConcurrentExactRegistryInsert()
    {
        var exactRouting = new ProjectRouting(ProjectAId, "project-a", Status.Active);
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectLifecycle = CreateProjectLifecycle(BasicMemoryProjectCreationStatus.Created, ProjectAPath);
        var projectRegistry = new StubProjectRegistry();
        projectRegistry.CreateHandler = (memoryProjectId, memoryProjectName, status, cancellationToken) =>
        {
            projectRegistry.ProjectById = exactRouting;
            projectRegistry.ProjectByName = exactRouting;
            throw new ProjectRegistryConflictException("A concurrent writer created the routing.");
        };

        var manager = CreateManager(projectDirectory, projectRegistry, projectLifecycle);
        var result = await manager.CreateProjectAsync("project-a", ProjectAPath);
        Assert.AreEqual(ProjectCreationStatus.AlreadyRegistered, result.Status);
        Assert.AreEqual(exactRouting, result.Routing);
        Assert.AreEqual(1, projectRegistry.CreateCallCount);
    }

    /// <summary>
    /// Verifies that an unresolved concurrent registry conflict after Basic Memory project creation fails closed.
    /// </summary>
    [TestMethod]
    public async Task CreateProjectAsyncFailsClosedForUnresolvedRegistryConflict()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectLifecycle = CreateProjectLifecycle(BasicMemoryProjectCreationStatus.Created, ProjectAPath);
        var projectRegistry = new StubProjectRegistry
        {
            CreateHandler = (memoryProjectId, memoryProjectName, status, cancellationToken) => throw new ProjectRegistryConflictException("A concurrent writer conflicted with project registration.")
        };

        var manager = CreateManager(projectDirectory, projectRegistry, projectLifecycle);
        var result = await manager.CreateProjectAsync("project-a", ProjectAPath);
        Assert.AreEqual(ProjectCreationStatus.RegistryWriteConflict, result.Status);
        Assert.IsNull(result.Routing);
    }

    /// <summary>
    /// Verifies that a project created by Basic Memory remains recoverable when the first ProjectMemoryProxy registry write fails unexpectedly.
    /// </summary>
    [TestMethod]
    public async Task CreateProjectAsyncRecoversAfterPreviousRegistryFailure()
    {
        var createAttempt = 0;

        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectLifecycle = new StubBasicMemoryProjectLifecycle
        {
            CreateHandler = (memoryProjectName, memoryProjectPath, cancellationToken) =>
            {
                createAttempt++;

                return Task.FromResult(
                    new BasicMemoryProjectCreationResult(
                        createAttempt == 1 ? BasicMemoryProjectCreationStatus.Created : BasicMemoryProjectCreationStatus.AlreadyExists,
                        new BasicMemoryProjectInfo(ProjectAId, "project-a"),
                        ProjectAPath));
            }
        };

        var projectRegistry = new StubProjectRegistry();
        projectRegistry.CreateHandler = (memoryProjectId, memoryProjectName, status, cancellationToken) =>
        {
            if (projectRegistry.CreateCallCount == 1)
                throw new InvalidOperationException("Simulated registry persistence failure.");

            return Task.FromResult(new ProjectRouting(memoryProjectId, memoryProjectName, status));
        };

        var manager = CreateManager(projectDirectory, projectRegistry, projectLifecycle);
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.CreateProjectAsync("project-a", ProjectAPath));

        var retryResult = await manager.CreateProjectAsync("project-a", ProjectAPath);
        Assert.AreEqual(ProjectCreationStatus.Recovered, retryResult.Status);
        Assert.IsNotNull(retryResult.Routing);
        Assert.AreEqual(ProjectAId, retryResult.Routing.MemoryProjectId);

        Assert.AreEqual(2, projectLifecycle.CreateCallCount);
        Assert.AreEqual(2, projectRegistry.CreateCallCount);
    }

    /// <summary>
    /// Verifies that project creation rejects a relative Basic Memory filesystem path before invoking external lifecycle operations.
    /// </summary>
    [TestMethod]
    public async Task CreateProjectAsyncRejectsRelativeProjectPath()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectLifecycle = new StubBasicMemoryProjectLifecycle();
        var projectRegistry = new StubProjectRegistry();

        var manager = CreateManager(projectDirectory, projectRegistry, projectLifecycle);
        await Assert.ThrowsAsync<ArgumentException>(() => manager.CreateProjectAsync("project-a", "relative/project-a"));
        Assert.AreEqual(0, projectLifecycle.CreateCallCount);
        Assert.AreEqual(0, projectDirectory.ValidateCallCount);
        Assert.AreEqual(0, projectRegistry.TotalCallCount);
    }

    /// <summary>
    /// Verifies that an active registered project can be deleted directly after exact Basic Memory validation and post-delete absence verification.
    /// </summary>
    [TestMethod]
    public async Task DeleteProjectAsyncDeletesActiveRouting()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);

        projectDirectory.ValidateHandler = (memoryProjectId, memoryProjectName, callCount, cancellationToken) =>
            Task.FromResult(CreateValidationResult(callCount == 1 ? BasicMemoryProjectValidationStatus.ExactMatch : BasicMemoryProjectValidationStatus.NotFound));

        var projectRegistry = new StubProjectRegistry
        {
            ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Active)
        };

        var projectLifecycle = new StubBasicMemoryProjectLifecycle();
        projectLifecycle.DeleteHandler = (memoryProjectName, deleteNotes, cancellationToken) =>
        {
            Assert.AreEqual(1, projectDirectory.ValidateCallCount);
            Assert.AreEqual(0, projectRegistry.TryRemoveProjectCallCount);
            return Task.CompletedTask;
        };

        projectRegistry.TryRemoveProjectHandler = (memoryProjectId, cancellationToken) =>
        {
            Assert.AreEqual(1, projectLifecycle.DeleteCallCount);
            Assert.AreEqual(2, projectDirectory.ValidateCallCount);
            return Task.FromResult(true);
        };

        var manager = CreateManager(projectDirectory, projectRegistry, projectLifecycle);
        var result = await manager.DeleteProjectAsync(ProjectAId, deleteNotes: false);
        Assert.AreEqual(ProjectDeletionStatus.Deleted, result.Status);
        Assert.AreEqual(2, projectDirectory.ValidateCallCount);
        Assert.AreEqual(1, projectLifecycle.DeleteCallCount);
        Assert.AreEqual("project-a", projectLifecycle.DeletedMemoryProjectName);
        Assert.IsFalse(projectLifecycle.DeletedMemoryProjectDeleteNotes);
        Assert.AreEqual(1, projectRegistry.TryRemoveProjectCallCount);
        Assert.AreEqual(ProjectAId, projectRegistry.RemovedProjectMemoryProjectId);
    }

    /// <summary>
    /// Verifies that an inactive registered project can be deleted directly without requiring reactivation.
    /// </summary>
    [TestMethod]
    public async Task DeleteProjectAsyncDeletesInactiveRouting()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        projectDirectory.ValidateHandler = (memoryProjectId, memoryProjectName, callCount, cancellationToken) =>
            Task.FromResult(CreateValidationResult(callCount == 1 ? BasicMemoryProjectValidationStatus.ExactMatch : BasicMemoryProjectValidationStatus.NotFound));

        var projectRegistry = new StubProjectRegistry
        {
            ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Inactive)
        };

        var projectLifecycle = new StubBasicMemoryProjectLifecycle
        {
            DeleteHandler = (memoryProjectName, deleteNotes, cancellationToken) => Task.CompletedTask
        };

        var manager = CreateManager(projectDirectory, projectRegistry, projectLifecycle);
        var result = await manager.DeleteProjectAsync(ProjectAId, deleteNotes: true);
        Assert.AreEqual(ProjectDeletionStatus.Deleted, result.Status);
        Assert.AreEqual(1, projectLifecycle.DeleteCallCount);
        Assert.IsTrue(projectLifecycle.DeletedMemoryProjectDeleteNotes);
        Assert.AreEqual(1, projectRegistry.TryRemoveProjectCallCount);
    }

    /// <summary>
    /// Verifies that a project-name mismatch observed after the remote delete fails closed without removing the local routing.
    /// </summary>
    [TestMethod]
    public async Task DeleteProjectAsyncFailsClosedForPostDeleteNameMismatch()
    {
        await AssertPostDeleteValidationFailureAsync(BasicMemoryProjectValidationStatus.NameMismatch, ProjectDeletionStatus.BasicMemoryProjectNameMismatch);
    }

    /// <summary>
    /// Verifies that a project-identifier mismatch observed after the remote delete fails closed without removing the local routing.
    /// </summary>
    [TestMethod]
    public async Task DeleteProjectAsyncFailsClosedForPostDeleteIdMismatch()
    {
        await AssertPostDeleteValidationFailureAsync(BasicMemoryProjectValidationStatus.IdMismatch, ProjectDeletionStatus.BasicMemoryProjectIdMismatch);
    }

    /// <summary>
    /// Verifies that conflicting project identities observed after the remote delete fail closed without removing the local routing.
    /// </summary>
    [TestMethod]
    public async Task DeleteProjectAsyncFailsClosedForPostDeleteIdentityConflict()
    {
        await AssertPostDeleteValidationFailureAsync(BasicMemoryProjectValidationStatus.IdentityConflict, ProjectDeletionStatus.BasicMemoryProjectIdentityConflict);
    }

    /// <summary>
    /// Verifies that an unsupported post-delete validation outcome fails closed without removing the local routing.
    /// </summary>
    [TestMethod]
    public async Task DeleteProjectAsyncFailsClosedForUnknownPostDeleteValidationStatus()
    {
        await AssertPostDeleteValidationFailureAsync((BasicMemoryProjectValidationStatus)int.MaxValue, ProjectDeletionStatus.BasicMemoryValidationFailed);
    }

    /// <summary>
    /// Verifies that a project whose remote delete succeeded can be recovered on retry when the first local routing removal failed.
    /// </summary>
    [TestMethod]
    public async Task DeleteProjectAsyncRecoversAfterPreviousRegistryFailure()
    {
        var projectDirectory =
            CreateProjectDirectory(
                BasicMemoryProjectValidationStatus.ExactMatch);

        projectDirectory.ValidateHandler = (
            _,
            _,
            callCount,
            _) =>
            Task.FromResult(
                CreateValidationResult(
                    callCount == 1
                        ? BasicMemoryProjectValidationStatus.ExactMatch
                        : BasicMemoryProjectValidationStatus.NotFound));

        var projectLifecycle = new StubBasicMemoryProjectLifecycle
        {
            DeleteHandler = (_, _, _) => Task.CompletedTask
        };

        var projectRegistry = new StubProjectRegistry
        {
            ProjectById =
                new ProjectRouting(
                    ProjectAId,
                    "project-a",
                    Status.Active)
        };

        projectRegistry.TryRemoveProjectHandler = (_, _) =>
        {
            if (projectRegistry.TryRemoveProjectCallCount == 1)
            {
                throw new InvalidOperationException(
                    "Simulated registry persistence failure.");
            }

            return Task.FromResult(true);
        };

        var manager = CreateManager(
            projectDirectory,
            projectRegistry,
            projectLifecycle);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.DeleteProjectAsync(
                ProjectAId,
                deleteNotes: false));

        var retryResult = await manager.DeleteProjectAsync(
            ProjectAId,
            deleteNotes: false);

        Assert.AreEqual(
            ProjectDeletionStatus.Recovered,
            retryResult.Status);

        Assert.AreEqual(
            1,
            projectLifecycle.DeleteCallCount,
            "The already deleted Basic Memory project must not be deleted again.");

        Assert.AreEqual(3, projectDirectory.ValidateCallCount);
        Assert.AreEqual(2, projectRegistry.TryRemoveProjectCallCount);
    }

    /// <summary>
    /// Verifies that cancellation during the remote project delete is propagated without post-delete validation or local routing removal.
    /// </summary>
    [TestMethod]
    public async Task DeleteProjectAsyncPropagatesRemoteCancellation()
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);
        var projectLifecycle = new StubBasicMemoryProjectLifecycle
        {
            DeleteHandler = (_, _, _) => throw new OperationCanceledException()
        };

        var projectRegistry = new StubProjectRegistry
        {
            ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Active)
        };

        var manager = CreateManager(projectDirectory, projectRegistry, projectLifecycle);
        await Assert.ThrowsAsync<OperationCanceledException>(() => manager.DeleteProjectAsync(ProjectAId, deleteNotes: false));
        Assert.AreEqual(1, projectDirectory.ValidateCallCount);
        Assert.AreEqual(1, projectLifecycle.DeleteCallCount);
        Assert.AreEqual(0, projectRegistry.TryRemoveProjectCallCount);
    }

    #endregion

    #region Private Methods

    private static StubBasicMemoryProjectDirectory CreateProjectDirectory(BasicMemoryProjectValidationStatus status)
    {
        return new StubBasicMemoryProjectDirectory(new BasicMemoryProjectValidationResult(status, null, null));
    }

    private static BasicMemoryProjectValidationResult CreateValidationResult(BasicMemoryProjectValidationStatus status)
    {
        return new BasicMemoryProjectValidationResult(status, null, null);
    }

    private static async Task AssertBasicMemoryValidationFailureAsync(BasicMemoryProjectValidationStatus validationStatus, ProjectRegistrationStatus expectedRegistrationStatus)
    {
        var projectDirectory = CreateProjectDirectory(validationStatus);
        var projectRegistry = new StubProjectRegistry();
        var manager = CreateManager(projectDirectory, projectRegistry);
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

    private static async Task AssertProjectReactivationValidationFailureAsync(BasicMemoryProjectValidationStatus validationStatus, ProjectRoutingStatusChangeStatus expectedStatus)
    {
        var projectDirectory = CreateProjectDirectory(validationStatus);
        var projectRegistry = new StubProjectRegistry { ProjectById = new ProjectRouting(ProjectAId, "project-a", Status.Inactive) };

        var manager = CreateManager(projectDirectory, projectRegistry);
        var result = await manager.ReactivateProjectAsync(ProjectAId);
        Assert.AreEqual(expectedStatus, result.Status);
        Assert.IsNull(result.Routing);
        Assert.AreEqual(1, projectDirectory.ValidateCallCount);
        Assert.AreEqual(0, projectRegistry.TryUpdateProjectStatusCallCount);
    }

    private static ProjectRegistryManager CreateManager(StubBasicMemoryProjectDirectory projectDirectory, StubProjectRegistry projectRegistry, StubBasicMemoryProjectLifecycle? projectLifecycle = null)
    {
        return new ProjectRegistryManager(projectDirectory, projectLifecycle ?? new StubBasicMemoryProjectLifecycle(), projectRegistry);
    }

    private static StubBasicMemoryProjectLifecycle CreateProjectLifecycle(BasicMemoryProjectCreationStatus status, string projectPath, Guid? memoryProjectId = null, string memoryProjectName = "project-a")
    {
        return new StubBasicMemoryProjectLifecycle
        {
            CreateHandler = (requestedProjectName, requestedProjectPath, cancellationToken) =>
                Task.FromResult(new BasicMemoryProjectCreationResult(status, new BasicMemoryProjectInfo(memoryProjectId ?? ProjectAId, memoryProjectName), projectPath))
        };
    }

    private static async Task AssertCreateProjectValidationFailureAsync(BasicMemoryProjectValidationStatus validationStatus, ProjectCreationStatus expectedStatus)
    {
        var projectDirectory = CreateProjectDirectory(validationStatus);
        var projectLifecycle = CreateProjectLifecycle(BasicMemoryProjectCreationStatus.Created, ProjectAPath);
        var projectRegistry = new StubProjectRegistry();
        var manager = CreateManager(projectDirectory, projectRegistry, projectLifecycle);

        var result = await manager.CreateProjectAsync("project-a", ProjectAPath);
        Assert.AreEqual(expectedStatus, result.Status);
        Assert.IsNull(result.Routing);

        Assert.AreEqual(1, projectLifecycle.CreateCallCount);
        Assert.AreEqual(1, projectDirectory.ValidateCallCount);
        Assert.AreEqual(0, projectRegistry.CreateCallCount);
    }

    private static async Task AssertPostDeleteValidationFailureAsync(BasicMemoryProjectValidationStatus postDeleteValidationStatus, ProjectDeletionStatus expectedStatus)
    {
        var projectDirectory = CreateProjectDirectory(BasicMemoryProjectValidationStatus.ExactMatch);

        projectDirectory.ValidateHandler = (
                _,
                _,
                callCount,
                _) =>
            Task.FromResult(
                CreateValidationResult(
                    callCount == 1
                        ? BasicMemoryProjectValidationStatus.ExactMatch
                        : postDeleteValidationStatus));

        var projectLifecycle = new StubBasicMemoryProjectLifecycle
        {
            DeleteHandler = (_, _, _) => Task.CompletedTask
        };

        var projectRegistry = new StubProjectRegistry
        {
            ProjectById =
                                          new ProjectRouting(
                                              ProjectAId,
                                              "project-a",
                                              Status.Active)
        };

        var manager = CreateManager(
            projectDirectory,
            projectRegistry,
            projectLifecycle);

        var result = await manager.DeleteProjectAsync(
                         ProjectAId,
                         deleteNotes: false);

        Assert.AreEqual(expectedStatus, result.Status);
        Assert.AreEqual(2, projectDirectory.ValidateCallCount);
        Assert.AreEqual(1, projectLifecycle.DeleteCallCount);
        Assert.AreEqual(0, projectRegistry.TryRemoveProjectCallCount);
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

        public Func<Guid, string, int, CancellationToken, Task<BasicMemoryProjectValidationResult>>? ValidateHandler { get; set; }

        public int ValidateCallCount { get; private set; }

        public Guid? ValidatedMemoryProjectId { get; private set; }

        public string? ValidatedMemoryProjectName { get; private set; }

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
            ValidatedMemoryProjectId = memoryProjectId;
            ValidatedMemoryProjectName = memoryProjectName;

            return ValidateHandler != null ?
                       ValidateHandler(memoryProjectId, memoryProjectName, ValidateCallCount, cancellationToken)
                       : Task.FromResult(_validationResult);
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

        public Func<ContextId, CancellationToken, Task<bool>>? TryRemoveBindingHandler { get; set; }

        public int FindBindingCallCount { get; private set; }

        public int CreateBindingCallCount { get; private set; }

        public int TryRemoveBindingCallCount { get; private set; }

        public ContextId? CreatedContextId { get; private set; }

        public Guid? CreatedBindingMemoryProjectId { get; private set; }

        public Status? CreatedBindingStatus { get; private set; }

        public int FindByIdCallCount { get; private set; }

        public int FindByNameCallCount { get; private set; }

        public int CreateCallCount { get; private set; }

        public int TotalCallCount =>
            FindByIdCallCount +
            FindByNameCallCount +
            CreateCallCount +
            FindBindingCallCount +
            CreateBindingCallCount +
            TryRemoveProjectCallCount +
            TryRemoveBindingCallCount +
            TryUpdateProjectStatusCallCount +
            TryUpdateBindingStatusCallCount;

        public Guid? CreatedMemoryProjectId { get; private set; }

        public string? CreatedMemoryProjectName { get; private set; }

        public Status? CreatedStatus { get; private set; }

        public Func<Guid, Status, Status, CancellationToken, Task<bool>>? TryUpdateProjectStatusHandler { get; set; }

        public Func<ContextId, Status, Status, CancellationToken, Task<bool>>? TryUpdateBindingStatusHandler { get; set; }

        public int TryUpdateProjectStatusCallCount { get; private set; }

        public int TryUpdateBindingStatusCallCount { get; private set; }

        public Guid? UpdatedProjectMemoryProjectId { get; private set; }

        public Status? ExpectedProjectStatus { get; private set; }

        public Status? NewProjectStatus { get; private set; }

        public ContextId? UpdatedBindingContextId { get; private set; }

        public ContextId? RemovedBindingContextId { get; private set; }

        public Status? ExpectedBindingStatus { get; private set; }

        public Status? NewBindingStatus { get; private set; }

        public Func<Guid, CancellationToken, Task<bool>>? TryRemoveProjectHandler { get; set; }

        public int TryRemoveProjectCallCount { get; private set; }

        public Guid? RemovedProjectMemoryProjectId { get; private set; }

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

        public Task<bool> TryUpdateProjectStatusAsync(Guid memoryProjectId, Status expectedStatus, Status newStatus, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryUpdateProjectStatusCallCount++;
            UpdatedProjectMemoryProjectId = memoryProjectId;
            ExpectedProjectStatus = expectedStatus;
            NewProjectStatus = newStatus;

            return TryUpdateProjectStatusHandler != null ?
                       TryUpdateProjectStatusHandler(memoryProjectId, expectedStatus, newStatus, cancellationToken)
                       : Task.FromResult(true);
        }

        public Task<bool> TryUpdateBindingStatusAsync(ContextId contextId, Status expectedStatus, Status newStatus, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryUpdateBindingStatusCallCount++;
            UpdatedBindingContextId = contextId;
            ExpectedBindingStatus = expectedStatus;
            NewBindingStatus = newStatus;

            return TryUpdateBindingStatusHandler != null ?
                       TryUpdateBindingStatusHandler(contextId, expectedStatus, newStatus, cancellationToken) :
                       Task.FromResult(true);
        }

        public Task<bool> TryRemoveBindingAsync(ContextId contextId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryRemoveBindingCallCount++;
            RemovedBindingContextId = contextId;

            return TryRemoveBindingHandler != null ?
                       TryRemoveBindingHandler(contextId, cancellationToken) :
                       Task.FromResult(true);
        }

        public async Task<bool> TryRemoveProjectAsync(Guid memoryProjectId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TryRemoveProjectCallCount++;
            RemovedProjectMemoryProjectId = memoryProjectId;

            var removed = TryRemoveProjectHandler == null ||
                          await TryRemoveProjectHandler(memoryProjectId, cancellationToken).ConfigureAwait(false);
            if (removed)
            {
                if (ProjectById?.MemoryProjectId == memoryProjectId)
                    ProjectById = null;

                if (ProjectByName?.MemoryProjectId == memoryProjectId)
                    ProjectByName = null;
            }

            return removed;
        }

        public Task<IReadOnlyList<ProjectRouting>> ListProjectsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ProjectRouting>>(Array.Empty<ProjectRouting>());
        }

        public Task<IReadOnlyList<ContextBinding>> ListBindingsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ContextBinding>>(Array.Empty<ContextBinding>());
        }

        #endregion
    }

    private sealed class StubBasicMemoryProjectLifecycle : IBasicMemoryProjectLifecycle
    {
        #region Properties

        public Func<string, string, CancellationToken, Task<BasicMemoryProjectCreationResult>>? CreateHandler { get; set; }

        public Func<string, bool, CancellationToken, Task>? DeleteHandler { get; set; }

        public int CreateCallCount { get; private set; }

        public int DeleteCallCount { get; private set; }

        public string? CreatedMemoryProjectName { get; private set; }

        public string? CreatedMemoryProjectPath { get; private set; }

        public string? DeletedMemoryProjectName { get; private set; }

        public bool? DeletedMemoryProjectDeleteNotes { get; private set; }

        #endregion

        #region Public Methods

        public Task<BasicMemoryProjectCreationResult> CreateAsync(string memoryProjectName, string memoryProjectPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CreateCallCount++;
            CreatedMemoryProjectName = memoryProjectName;
            CreatedMemoryProjectPath = memoryProjectPath;

            if (CreateHandler == null)
                throw new NotSupportedException("The test did not configure a Basic Memory project-create operation.");

            return CreateHandler(memoryProjectName, memoryProjectPath, cancellationToken);
        }

        public Task DeleteAsync(string memoryProjectName, bool deleteNotes, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DeleteCallCount++;
            DeletedMemoryProjectName = memoryProjectName;
            DeletedMemoryProjectDeleteNotes = deleteNotes;

            if (DeleteHandler == null)
                throw new NotSupportedException("The test did not configure a Basic Memory project-delete operation.");

            return DeleteHandler(memoryProjectName, deleteNotes, cancellationToken);
        }

        #endregion
    }

    #endregion
}
