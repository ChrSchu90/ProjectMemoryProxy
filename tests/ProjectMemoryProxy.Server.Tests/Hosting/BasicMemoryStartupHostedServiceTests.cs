namespace ProjectMemoryProxy.Server.Tests.Hosting;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
using ProjectMemoryProxy.BasicMemory;
using ProjectMemoryProxy.Core.Configuration;
using ProjectMemoryProxy.Core.Routing;
using ProjectMemoryProxy.Server.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Tests for <see cref="BasicMemoryStartupHostedService"/>
/// </summary>
[TestClass]
public sealed class BasicMemoryStartupHostedServiceTests
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
    /// Verifies that startup registers safely mirrored Basic Memory tools and reconciles new and missing project routings before completing.
    /// </summary>
    [TestMethod]
    public async Task StartAsyncRegistersToolsAndReconcilesProjectInventory()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cancellationToken = cancellationTokenSource.Token;
        await using var upstreamServer = await StartBasicMemoryServerAsync([CreateProject(ProjectAId, "project-a")], cancellationToken);
        var projectRegistry = new StubProjectRegistry(new ProjectRouting(ProjectBId, "project-b", Status.Active));
        await using var serviceProvider = CreateServiceProvider(GetMcpEndpoint(upstreamServer), projectRegistry);
        var hostedService = new BasicMemoryStartupHostedService(serviceProvider, NullLogger<BasicMemoryStartupHostedService>.Instance);
        await hostedService.StartAsync(cancellationToken);

        var projectA = projectRegistry.GetProject(ProjectAId);
        Assert.IsNotNull(projectA);
        Assert.AreEqual("project-a", projectA.MemoryProjectName);
        Assert.AreEqual(Status.Active, projectA.Status);

        var projectB = projectRegistry.GetProject(ProjectBId);
        Assert.IsNotNull(projectB);
        Assert.AreEqual(Status.Inactive, projectB.Status);
        Assert.AreEqual(1, projectRegistry.CreateCallCount);
        Assert.AreEqual(1, projectRegistry.TryUpdateProjectStatusCallCount);

        var serverOptions = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var toolCollection = serverOptions.ToolCollection ??
                             throw new AssertFailedException("The MCP tool collection was not initialized.");

        Assert.IsTrue(toolCollection.TryGetPrimitive("read_note", out _));
        Assert.IsFalse(toolCollection.TryGetPrimitive("list_memory_projects", out _));
    }

    /// <summary>
    /// Verifies that a Basic Memory UUID/name identity conflict aborts startup without mutating persisted project-routing state.
    /// </summary>
    [TestMethod]
    public async Task StartAsyncFailsClosedForProjectIdentityConflict()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cancellationToken = cancellationTokenSource.Token;
        await using var upstreamServer = await StartBasicMemoryServerAsync([CreateProject(ProjectAId, "project-a")], cancellationToken);
        var projectRegistry = new StubProjectRegistry(new ProjectRouting(ProjectAId, "different-project", Status.Active));
        await using var serviceProvider = CreateServiceProvider(GetMcpEndpoint(upstreamServer), projectRegistry);

        var hostedService = new BasicMemoryStartupHostedService(serviceProvider, NullLogger<BasicMemoryStartupHostedService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => hostedService.StartAsync(cancellationToken));

        var routing = projectRegistry.GetProject(ProjectAId);
        Assert.IsNotNull(routing);
        Assert.AreEqual("different-project", routing.MemoryProjectName);
        Assert.AreEqual(Status.Active, routing.Status);
        Assert.AreEqual(0, projectRegistry.CreateCallCount);
        Assert.AreEqual(0, projectRegistry.TryUpdateProjectStatusCallCount);
    }

    #endregion

    #region Private Methods

    private static TestProject CreateProject(Guid projectId, string projectName)
    {
        return new TestProject(projectName, projectId.ToString("D"), "local");
    }

    private static async Task<WebApplication> StartBasicMemoryServerAsync(IReadOnlyList<TestProject> projects, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddSingleton(new TestProjectListState(projects));
        builder.Services
            .AddMcpServer()
            .WithHttpTransport(options => { options.SessionMode = HttpServerSessionMode.Stateless; })
            .WithTools<TestBasicMemoryTools>();

        var app = builder.Build();
        app.MapMcp("/mcp");
        await app.StartAsync(cancellationToken);
        return app;
    }

    private static ServiceProvider CreateServiceProvider(Uri basicMemoryEndpoint, IProjectRegistry projectRegistry)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Options.Create(new ProjectMemoryProxyOptions
        {
            BasicMemoryEndpoint = basicMemoryEndpoint,
            BasicMemoryConnectionTimeout = TimeSpan.FromSeconds(5)
        }));

        services.AddSingleton(projectRegistry);
        services.AddSingleton<RoutingManager>();
        services.AddBasicMemory();

        // The hosted-service test needs MCP server options/tool registration, but it does not host the ProjectMemoryProxy HTTP transport itself.
        services.AddMcpServer();

        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
    }

    private static Uri GetMcpEndpoint(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ??
                        throw new InvalidOperationException("The test MCP server did not publish any addresses.");

        return new Uri($"{addresses.Single().TrimEnd('/')}/mcp");
    }

    #endregion

    #region Test Classes

    private sealed class TestProjectListState
    {
        public TestProjectListState(
            IReadOnlyList<TestProject> projects)
        {
            Projects = projects ?? throw new ArgumentNullException(nameof(projects));
        }

        public IReadOnlyList<TestProject> Projects { get; }
    }

    [McpServerToolType]
    private sealed class TestBasicMemoryTools
    {
        private readonly TestProjectListState _state;

        public TestBasicMemoryTools(TestProjectListState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Provides a representative project-routed Basic Memory operation for mirrored-tool startup registration.
        /// </summary>
        [McpServerTool(Name = "read_note", ReadOnly = true)]
        public string ReadNote(string identifier, string? project_id = null)
        {
            return $"{identifier}|{project_id}";
        }

        /// <summary>
        /// Provides the structured Basic Memory project-list response used by startup reconciliation.
        /// </summary>
        [McpServerTool(Name = "list_memory_projects", ReadOnly = true, UseStructuredContent = true)]
        public ProjectListEnvelope ListMemoryProjects(string output_format = "text")
        {
            if (!string.Equals(output_format, "json", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Project reconciliation must request the JSON Basic Memory project-list contract.");
            }

            return new ProjectListEnvelope(new ProjectListPayload(_state.Projects));
        }
    }

    private sealed record ProjectListEnvelope(
        [property: JsonPropertyName("result")] ProjectListPayload Result);

    private sealed record ProjectListPayload(
        [property: JsonPropertyName("projects")] IReadOnlyList<TestProject> Projects);

    private sealed record TestProject(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("external_id")] string ExternalId,
        [property: JsonPropertyName("source")] string Source);

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

        public int TryUpdateProjectStatusCallCount { get; private set; }

        #endregion

        #region Public Methods

        public Task<IReadOnlyList<ProjectRouting>> ListProjectsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ProjectRouting>>(_projectsById.Values.ToArray());
        }

        public Task<IReadOnlyList<ContextBinding>> ListBindingsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ContextBinding>>(Array.Empty<ContextBinding>());
        }

        public Task<ProjectRoute?> FindRouteAsync(ContextId contextId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<ProjectRoute?>(null);
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

        #endregion
    }

    #endregion
}
