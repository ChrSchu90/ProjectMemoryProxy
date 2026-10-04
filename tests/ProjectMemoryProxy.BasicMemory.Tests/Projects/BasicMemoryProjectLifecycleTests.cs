namespace ProjectMemoryProxy.BasicMemory.Tests.Projects;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
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
using ProjectMemoryProxy.BasicMemory.Client;
using ProjectMemoryProxy.BasicMemory.Discovery;
using ProjectMemoryProxy.BasicMemory.Projects.Lifecycle;
using ProjectMemoryProxy.Core.Configuration;

/// <summary>
/// Tests for <see cref="BasicMemoryProjectLifecycle"/>.
/// </summary>
[TestClass]
public sealed class BasicMemoryProjectLifecycleTests
{
    #region Static Fields

    private static readonly Guid ProjectAId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    #endregion

    #region Private Fields

    #endregion

    #region Tests Constructors

    #endregion

    #region Test Preparation

    #endregion

    #region Tests

    /// <summary>
    /// Verifies that a newly created Basic Memory project is returned with its exact identity and path using controlled lifecycle arguments.
    /// </summary>
    [TestMethod]
    public async Task CreateAsyncReturnsCreatedProject()
    {
        var projectPath = GetProjectPath("project-a");
        await using var fixture = await CreateFixtureAsync(new TestProjectCreatePayload("project-a", ProjectAId.ToString("D"), projectPath, true, false));

        var result = await fixture.Lifecycle.CreateAsync("project-a", projectPath);
        Assert.AreEqual(BasicMemoryProjectCreationStatus.Created, result.Status);
        Assert.AreEqual(ProjectAId, result.Project.MemoryProjectId);
        Assert.AreEqual("project-a", result.Project.MemoryProjectName);
        Assert.AreEqual(projectPath, result.ProjectPath);

        Assert.AreEqual(1, fixture.State.CallCount);
        Assert.AreEqual("project-a", fixture.State.ProjectName);
        Assert.AreEqual(projectPath, fixture.State.ProjectPath);
        Assert.IsFalse(fixture.State.SetDefault);
        Assert.AreEqual("json", fixture.State.OutputFormat);
    }

    /// <summary>
    /// Verifies that an existing Basic Memory project is returned as an idempotent create outcome with its current identity and path.
    /// </summary>
    [TestMethod]
    public async Task CreateAsyncReturnsExistingProject()
    {
        var projectPath = GetProjectPath("project-a");
        await using var fixture = await CreateFixtureAsync(new TestProjectCreatePayload("project-a", ProjectAId.ToString("D"), projectPath, false, true));

        var result = await fixture.Lifecycle.CreateAsync("project-a", projectPath);
        Assert.AreEqual(BasicMemoryProjectCreationStatus.AlreadyExists, result.Status);
        Assert.AreEqual(ProjectAId, result.Project.MemoryProjectId);
        Assert.AreEqual("project-a", result.Project.MemoryProjectName);
        Assert.AreEqual(projectPath, result.ProjectPath);
    }

    /// <summary>
    /// Verifies that project creation fails closed when Basic Memory returns an invalid external project identifier.
    /// </summary>
    [TestMethod]
    public async Task CreateAsyncRejectsInvalidExternalId()
    {
        var projectPath = GetProjectPath("project-a");
        await using var fixture = await CreateFixtureAsync(new TestProjectCreatePayload("project-a", "not-a-guid", projectPath, true, false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Lifecycle.CreateAsync("project-a", projectPath));
    }

    /// <summary>
    /// Verifies that project creation fails closed when Basic Memory reports contradictory created and already-exists flags.
    /// </summary>
    [TestMethod]
    public async Task CreateAsyncRejectsInconsistentCreationFlags()
    {
        var projectPath = GetProjectPath("project-a");
        await using var fixture = await CreateFixtureAsync(new TestProjectCreatePayload("project-a", ProjectAId.ToString("D"), projectPath, true, true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Lifecycle.CreateAsync("project-a", projectPath));
    }

    /// <summary>
    /// Verifies that a structured Basic Memory lifecycle error fails closed instead of being interpreted as a created project.
    /// </summary>
    [TestMethod]
    public async Task CreateAsyncRejectsStructuredErrorResponse()
    {
        var projectPath = GetProjectPath("project-a");
        await using var fixture = await CreateFixtureAsync(new TestProjectCreatePayload("project-a", ProjectAId.ToString("D"), projectPath, false, false, "PROJECT_CONSTRAINED"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Lifecycle.CreateAsync("project-a", projectPath));
    }

    /// <summary>
    /// Verifies that project creation fails closed when Basic Memory does not expose the required create lifecycle tool.
    /// </summary>
    [TestMethod]
    public async Task CreateAsyncRejectsMissingCreateTool()
    {
        await using var fixture = await CreateFixtureWithoutCreateToolAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Lifecycle.CreateAsync("project-a", GetProjectPath("project-a")));
    }
    
    /// <summary>
    /// Verifies that project deletion forwards the exact project name and delete-notes choice to Basic Memory.
    /// </summary>
    [TestMethod]
    public async Task DeleteAsyncForwardsProjectNameAndDeleteNotes()
    {
        await using var fixture = await CreateFixtureAsync(new TestProjectCreatePayload("unused", ProjectAId.ToString("D"), GetProjectPath("unused"), true, false));
        await fixture.Lifecycle.DeleteAsync("project-a", deleteNotes: true);
        Assert.AreEqual(1, fixture.State.DeleteCallCount);
        Assert.AreEqual("project-a", fixture.State.DeletedProjectName);
        Assert.IsTrue(fixture.State.DeleteNotes);
    }

    /// <summary>
    /// Verifies that project deletion rejects a missing project name before invoking the Basic Memory lifecycle tool.
    /// </summary>
    [TestMethod]
    public async Task DeleteAsyncRejectsEmptyProjectName()
    {
        await using var fixture = await CreateFixtureAsync(new TestProjectCreatePayload("unused", ProjectAId.ToString("D"), GetProjectPath("unused"), true, false));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Lifecycle.DeleteAsync(" ", deleteNotes: false));
        Assert.AreEqual(0, fixture.State.DeleteCallCount);
    }

    #endregion

    #region Private Methods

    private static string GetProjectPath(string projectName)
    {
        return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "project-memory-proxy-tests", projectName);
    }

    private static async Task<TestFixture> CreateFixtureAsync(TestProjectCreatePayload response, CancellationToken cancellationToken = default)
    {
        var state = new TestProjectCreateState(response);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));

        builder.Services.AddSingleton(state);
        builder.Services
            .AddMcpServer()
            .WithHttpTransport(options => { options.SessionMode = HttpServerSessionMode.Stateless; })
            .WithTools<TestBasicMemoryTools>();

        var server = builder.Build();
        server.MapMcp("/mcp");
        await server.StartAsync(cancellationToken);

        return await CreateFixtureAsync(server, state, cancellationToken);
    }

    private static async Task<TestFixture> CreateFixtureWithoutCreateToolAsync(CancellationToken cancellationToken = default)
    {
        var state = new TestProjectCreateState(new TestProjectCreatePayload("unused", ProjectAId.ToString("D"), GetProjectPath("unused"), true, false));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));

        builder.Services.AddSingleton(state);
        builder.Services
            .AddMcpServer()
            .WithHttpTransport(options => { options.SessionMode = HttpServerSessionMode.Stateless; })
            .WithTools<TestMissingCreateTool>();

        var server = builder.Build();
        server.MapMcp("/mcp");
        await server.StartAsync(cancellationToken);

        return await CreateFixtureAsync(server, state, cancellationToken);
    }

    private static async Task<TestFixture> CreateFixtureAsync(WebApplication server, TestProjectCreateState state, CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = GetMcpEndpoint(server);
            var options = Options.Create(new ProjectMemoryProxyOptions
            {
                BasicMemoryEndpoint = endpoint,
                BasicMemoryConnectionTimeout = TimeSpan.FromSeconds(5)
            });

            var client = new BasicMemoryClient(options, NullLoggerFactory.Instance, NullLogger<BasicMemoryClient>.Instance);

            try
            {
                var toolCatalog = new BasicMemoryToolCatalog(client, NullLogger<BasicMemoryToolCatalog>.Instance);
                var lifecycle = new BasicMemoryProjectLifecycle(toolCatalog);
                return new TestFixture(server, client, toolCatalog, lifecycle, state);
            }
            catch
            {
                await client.DisposeAsync();
                throw;
            }
        }
        catch
        {
            await server.DisposeAsync();
            throw;
        }
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

    private sealed class TestFixture : IAsyncDisposable
    {
        #region Constructors

        public TestFixture(WebApplication server, BasicMemoryClient client, BasicMemoryToolCatalog toolCatalog, BasicMemoryProjectLifecycle lifecycle, TestProjectCreateState state)
        {
            Server = server;
            Client = client;
            ToolCatalog = toolCatalog;
            Lifecycle = lifecycle;
            State = state;
        }

        #endregion

        #region Properties

        public WebApplication Server { get; }

        public BasicMemoryClient Client { get; }

        public BasicMemoryToolCatalog ToolCatalog { get; }

        public BasicMemoryProjectLifecycle Lifecycle { get; }

        public TestProjectCreateState State { get; }

        #endregion

        #region Public Methods

        public async ValueTask DisposeAsync()
        {
            ToolCatalog.Dispose();
            await Client.DisposeAsync();
            await Server.DisposeAsync();
        }

        #endregion
    }

    private sealed class TestProjectCreateState
    {
        #region Constructors

        public TestProjectCreateState(TestProjectCreatePayload response)
        {
            Response = response ?? throw new ArgumentNullException(nameof(response));
        }

        #endregion

        #region Properties

        public TestProjectCreatePayload Response { get; }

        public int CallCount { get; private set; }

        public string? ProjectName { get; private set; }

        public string? ProjectPath { get; private set; }

        public bool SetDefault { get; private set; }

        public string? OutputFormat { get; private set; }

        public int DeleteCallCount { get; private set; }

        public string? DeletedProjectName { get; private set; }

        public bool? DeleteNotes { get; private set; }

        #endregion

        #region Public Methods

        public void Record(string projectName, string projectPath, bool setDefault, string outputFormat)
        {
            CallCount++;
            ProjectName = projectName;
            ProjectPath = projectPath;
            SetDefault = setDefault;
            OutputFormat = outputFormat;
        }

        public void RecordDelete(string projectName, bool deleteNotes)
        {
            DeleteCallCount++;
            DeletedProjectName = projectName;
            DeleteNotes = deleteNotes;
        }

        #endregion
    }

    [McpServerToolType]
    private sealed class TestBasicMemoryTools
    {
        #region Private Fields

        private readonly TestProjectCreateState _state;

        #endregion

        #region Constructors

        public TestBasicMemoryTools(TestProjectCreateState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Provides the structured Basic Memory project-create response used by lifecycle tests.
        /// </summary>
        [McpServerTool(Name = "create_memory_project", ReadOnly = false, UseStructuredContent = true)]
        public ProjectCreateEnvelope CreateMemoryProject(string project_name, string project_path, bool set_default = false, string output_format = "text")
        {
            _state.Record(project_name, project_path, set_default, output_format);
            return new ProjectCreateEnvelope(_state.Response);
        }

        /// <summary>
        /// Provides the Basic Memory project-delete response used by lifecycle tests.
        /// </summary>
        [McpServerTool(Name = "delete_project", ReadOnly = false)]
        public string DeleteProject(string project_name, bool delete_notes = false)
        {
            _state.RecordDelete(project_name, delete_notes);
            return "Project deleted.";
        }

        #endregion
    }

    [McpServerToolType]
    private sealed class TestMissingCreateTool
    {
        /// <summary>
        /// Provides an unrelated tool so the catalog initializes without the create lifecycle operation.
        /// </summary>
        [McpServerTool(Name = "unrelated_tool", ReadOnly = true)]
        public static string UnrelatedTool()
        {
            return "unused";
        }
    }

    private sealed record ProjectCreateEnvelope(
        [property: JsonPropertyName("result")] TestProjectCreatePayload Result);

    private sealed record TestProjectCreatePayload(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("external_id")] string ExternalId,
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("created")] bool Created,
        [property: JsonPropertyName("already_exists")] bool AlreadyExists,
        [property: JsonPropertyName("error")] string? Error = null);

    #endregion
}
