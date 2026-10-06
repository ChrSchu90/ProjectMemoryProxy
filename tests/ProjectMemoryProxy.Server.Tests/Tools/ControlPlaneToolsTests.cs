namespace ProjectMemoryProxy.Server.Tests.Tools;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Server;
using ProjectMemoryProxy.BasicMemory.Projects;
using ProjectMemoryProxy.BasicMemory.Projects.Directory;
using ProjectMemoryProxy.BasicMemory.Projects.Lifecycle;
using ProjectMemoryProxy.Core.Routing;
using ProjectMemoryProxy.Server.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Tests for <see cref="ControlPlaneTools"/>
/// </summary>
[TestClass]
public sealed class ControlPlaneToolsTests
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
    /// Verifies that the control-plane MCP surface exposes the intended active tools while status-transition tools remain prepared but hidden.
    /// </summary>
    [TestMethod]
    public void McpToolAttributesExposeOnlyEnabledControlPlaneOperations()
    {
        var exposedToolNames = typeof(ControlPlaneTools)
                               .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                               .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
                               .Where(attribute => attribute != null)
                               .Select(attribute => attribute!.Name)
                               .OrderBy(name => name, StringComparer.Ordinal)
                               .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "bind_context",
                "create_project",
                "delete_project",
                "list_context_bindings",
                "list_projects",
                "unbind_context"
            },
            exposedToolNames);

        Assert.IsNull(typeof(ControlPlaneTools).GetMethod(nameof(ControlPlaneTools.DeactivateProjectAsync))!.GetCustomAttribute<McpServerToolAttribute>());
        Assert.IsNull(typeof(ControlPlaneTools).GetMethod(nameof(ControlPlaneTools.ReactivateProjectAsync))!.GetCustomAttribute<McpServerToolAttribute>());
        Assert.IsNull(typeof(ControlPlaneTools).GetMethod(nameof(ControlPlaneTools.DeactivateContextAsync))!.GetCustomAttribute<McpServerToolAttribute>());
        Assert.IsNull(typeof(ControlPlaneTools).GetMethod(nameof(ControlPlaneTools.ReactivateContextAsync))!.GetCustomAttribute<McpServerToolAttribute>());
    }

    /// <summary>
    /// Verifies that the MCP control plane supports project creation, discovery, binding lifecycle, hidden status transitions, unbinding, and deletion through the existing manager rules.
    /// </summary>
    [TestMethod]
    public async Task ControlPlaneWorkflowUsesExistingLifecycleAndRegistryRules()
    {
        var state = new BasicMemoryState();
        var registry = new InMemoryProjectRegistry();
        var manager = new ProjectRegistryManager(new StubProjectDirectory(state), new StubProjectLifecycle(state), registry);
        var tools = new ControlPlaneTools(manager, registry);
        var projectPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ProjectMemoryProxy.Tests", Guid.NewGuid().ToString("N")));

        var createResult = await tools.CreateProjectAsync("project-a", projectPath, CancellationToken.None);
        Assert.AreEqual("created", createResult.Status);
        Assert.IsNotNull(createResult.Project);
        var projectId = createResult.Project.ProjectId;
        Assert.AreEqual("project-a", createResult.Project.ProjectName);
        Assert.AreEqual("active", createResult.Project.Status);

        var projects = await tools.ListProjectsAsync(CancellationToken.None);
        Assert.AreEqual(1, projects.Projects.Count);
        Assert.AreEqual(projectId, projects.Projects[0].ProjectId);

        var bindResult = await tools.BindContextAsync(TestContextId, projectId, CancellationToken.None);
        Assert.AreEqual("bound", bindResult.Status);
        Assert.IsNotNull(bindResult.Binding);
        Assert.AreEqual(TestContextId, bindResult.Binding.ContextId);
        Assert.AreEqual(projectId, bindResult.Binding.ProjectId);
        Assert.AreEqual("active", bindResult.Binding.Status);

        var bindings = await tools.ListContextBindingsAsync(CancellationToken.None);
        Assert.AreEqual(1, bindings.Bindings.Count);
        Assert.AreEqual(TestContextId, bindings.Bindings[0].ContextId);

        var deactivateProjectResult = await tools.DeactivateProjectAsync(projectId, CancellationToken.None);
        Assert.AreEqual("updated", deactivateProjectResult.Status);
        Assert.AreEqual("inactive", deactivateProjectResult.Project!.Status);

        var reactivateProjectResult = await tools.ReactivateProjectAsync(projectId, CancellationToken.None);
        Assert.AreEqual("updated", reactivateProjectResult.Status);
        Assert.AreEqual("active", reactivateProjectResult.Project!.Status);

        var deactivateContextResult = await tools.DeactivateContextAsync(TestContextId, CancellationToken.None);
        Assert.AreEqual("updated", deactivateContextResult.Status);
        Assert.AreEqual("inactive", deactivateContextResult.Binding!.Status);

        var reactivateContextResult = await tools.ReactivateContextAsync(TestContextId, CancellationToken.None);
        Assert.AreEqual("updated", reactivateContextResult.Status);
        Assert.AreEqual("active", reactivateContextResult.Binding!.Status);

        var unbindResult = await tools.UnbindContextAsync(TestContextId, CancellationToken.None);
        Assert.AreEqual("unbound", unbindResult.Status);
        Assert.AreEqual(0, (await tools.ListContextBindingsAsync(CancellationToken.None)).Bindings.Count);

        var deleteResult = await tools.DeleteProjectAsync(projectId, false, CancellationToken.None);
        Assert.AreEqual("deleted", deleteResult.Status);
        Assert.AreEqual(0, (await tools.ListProjectsAsync(CancellationToken.None)).Projects.Count);
    }

    /// <summary>
    /// Verifies that malformed context identifiers are rejected without mutating the registry.
    /// </summary>
    [TestMethod]
    public async Task BindContextAsyncRejectsInvalidContext()
    {
        var state = new BasicMemoryState();
        var registry = new InMemoryProjectRegistry();
        var manager = new ProjectRegistryManager(new StubProjectDirectory(state), new StubProjectLifecycle(state), registry);
        var tools = new ControlPlaneTools(manager, registry);

        var result = await tools.BindContextAsync("invalid-context", Guid.NewGuid(), CancellationToken.None);
        Assert.AreEqual("invalid_context", result.Status);
        Assert.IsNull(result.Binding);
        Assert.AreEqual(0, registry.Bindings.Count);
    }
    
    #endregion

    #region Private Methods

    #endregion

    #region Test Classes

    private sealed class BasicMemoryState
    {
        public Dictionary<string, (BasicMemoryProjectInfo Project, string Path)> Projects { get; } = new(StringComparer.Ordinal);
    }

    private sealed class StubProjectDirectory : IBasicMemoryProjectDirectory
    {
        private readonly BasicMemoryState _state;

        public StubProjectDirectory(BasicMemoryState state)
        {
            _state = state;
        }

        public Task<IReadOnlyList<BasicMemoryProjectInfo>> ListAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<BasicMemoryProjectInfo>>(_state.Projects.Values.Select(item => item.Project).ToArray());
        }

        public Task<BasicMemoryProjectValidationResult> ValidateAsync(Guid memoryProjectId, string memoryProjectName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var byName = _state.Projects.TryGetValue(memoryProjectName, out var namedProject) ? namedProject.Project : null;
            var byId = _state.Projects.Values.Select(item => item.Project).SingleOrDefault(project => project.MemoryProjectId == memoryProjectId);

            var status = byId == null && byName == null ? BasicMemoryProjectValidationStatus.NotFound :
                byId != null && byName != null && byId == byName ? BasicMemoryProjectValidationStatus.ExactMatch :
                byId != null && byName == null ? BasicMemoryProjectValidationStatus.NameMismatch :
                byId == null ? BasicMemoryProjectValidationStatus.IdMismatch :
                BasicMemoryProjectValidationStatus.IdentityConflict;

            return Task.FromResult(new BasicMemoryProjectValidationResult(status, byId, byName));
        }
    }

    private sealed class StubProjectLifecycle : IBasicMemoryProjectLifecycle
    {
        private readonly BasicMemoryState _state;

        public StubProjectLifecycle(BasicMemoryState state)
        {
            _state = state;
        }

        public Task<BasicMemoryProjectCreationResult> CreateAsync(string memoryProjectName, string memoryProjectPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_state.Projects.TryGetValue(memoryProjectName, out var existing))
                return Task.FromResult(new BasicMemoryProjectCreationResult(BasicMemoryProjectCreationStatus.AlreadyExists, existing.Project, existing.Path));

            var project = new BasicMemoryProjectInfo(Guid.NewGuid(), memoryProjectName);
            _state.Projects.Add(memoryProjectName, (project, memoryProjectPath));
            return Task.FromResult(new BasicMemoryProjectCreationResult(BasicMemoryProjectCreationStatus.Created, project, memoryProjectPath));
        }

        public Task DeleteAsync(string memoryProjectName, bool deleteNotes, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _state.Projects.Remove(memoryProjectName);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryProjectRegistry : IProjectRegistry
    {
        private readonly Dictionary<Guid, ProjectRouting> _projects = new();
        private readonly Dictionary<ContextId, ContextBinding> _bindings = new();

        public IReadOnlyDictionary<ContextId, ContextBinding> Bindings => _bindings;

        public Task<IReadOnlyList<ProjectRouting>> ListProjectsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ProjectRouting>>(_projects.Values.OrderBy(project => project.MemoryProjectName, StringComparer.Ordinal).ToArray());
        }

        public Task<IReadOnlyList<ContextBinding>> ListBindingsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ContextBinding>>(_bindings.Values.OrderBy(binding => binding.ContextId.ToString(), StringComparer.Ordinal).ToArray());
        }

        public Task<ProjectRoute?> FindRouteAsync(ContextId contextId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_bindings.TryGetValue(contextId, out var binding) || !_projects.TryGetValue(binding.MemoryProjectId, out var routing))
                return Task.FromResult<ProjectRoute?>(null);

            return Task.FromResult<ProjectRoute?>(new ProjectRoute(routing.MemoryProjectId, routing.MemoryProjectName, routing.Status, binding.Status));
        }

        public Task<ProjectRouting?> FindByMemoryProjectIdAsync(Guid memoryProjectId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_projects.GetValueOrDefault(memoryProjectId));
        }

        public Task<ProjectRouting?> FindByMemoryProjectNameAsync(string memoryProjectName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_projects.Values.SingleOrDefault(project => string.Equals(project.MemoryProjectName, memoryProjectName, StringComparison.Ordinal)));
        }

        public Task<ProjectRouting> CreateAsync(Guid memoryProjectId, string memoryProjectName, Status status, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_projects.ContainsKey(memoryProjectId) || _projects.Values.Any(project => string.Equals(project.MemoryProjectName, memoryProjectName, StringComparison.Ordinal)))
                throw new ProjectRegistryConflictException("Duplicate project routing.");

            var routing = new ProjectRouting(memoryProjectId, memoryProjectName, status);
            _projects.Add(memoryProjectId, routing);
            return Task.FromResult(routing);
        }

        public Task<bool> TryRemoveProjectAsync(Guid memoryProjectId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_projects.Remove(memoryProjectId))
                return Task.FromResult(false);

            foreach (var contextId in _bindings.Where(item => item.Value.MemoryProjectId == memoryProjectId).Select(item => item.Key).ToArray())
            {
                _bindings.Remove(contextId);
            }

            return Task.FromResult(true);
        }

        public Task<ContextBinding?> FindBindingAsync(ContextId contextId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_bindings.GetValueOrDefault(contextId));
        }

        public Task<ContextBinding> CreateBindingAsync(ContextId contextId, Guid memoryProjectId, Status status, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_projects.ContainsKey(memoryProjectId))
                throw new InvalidOperationException("Missing project routing.");
            if (_bindings.ContainsKey(contextId))
                throw new ProjectRegistryConflictException("Duplicate context binding.");

            var binding = new ContextBinding(contextId, memoryProjectId, status);
            _bindings.Add(contextId, binding);
            return Task.FromResult(binding);
        }

        public Task<bool> TryUpdateProjectStatusAsync(Guid memoryProjectId, Status expectedStatus, Status newStatus, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_projects.TryGetValue(memoryProjectId, out var routing) || routing.Status != expectedStatus)
                return Task.FromResult(false);

            _projects[memoryProjectId] = routing with { Status = newStatus };
            return Task.FromResult(true);
        }

        public Task<bool> TryUpdateBindingStatusAsync(ContextId contextId, Status expectedStatus, Status newStatus, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_bindings.TryGetValue(contextId, out var binding) || binding.Status != expectedStatus)
                return Task.FromResult(false);

            _bindings[contextId] = binding with { Status = newStatus };
            return Task.FromResult(true);
        }

        public Task<bool> TryRemoveBindingAsync(ContextId contextId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_bindings.Remove(contextId));
        }
    }

    #endregion
}
