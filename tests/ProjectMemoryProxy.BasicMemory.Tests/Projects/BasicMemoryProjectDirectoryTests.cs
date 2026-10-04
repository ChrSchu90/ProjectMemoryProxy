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
using ProjectMemoryProxy.BasicMemory.Projects.Directory;
using ProjectMemoryProxy.Core.Configuration;

/// <summary>
/// Tests for <see cref="BasicMemoryProjectDirectory"/>.
/// </summary>
[TestClass]
public sealed class BasicMemoryProjectDirectoryTests
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
    /// Verifies that local Basic Memory projects are returned using their exact external UUID and project name.
    /// </summary>
    [TestMethod]
    public async Task ListAsyncReturnsLocalProjectIdentities()
    {
        await using var fixture = await CreateFixtureAsync(
        [
            CreateProject(ProjectAId, "project-a"),
            CreateProject(ProjectBId, "project-b")
        ]);

        var projects = await fixture.Directory.ListAsync();
        Assert.HasCount(2, projects);
        Assert.AreEqual(ProjectAId, projects[0].MemoryProjectId);
        Assert.AreEqual("project-a", projects[0].MemoryProjectName);
        Assert.AreEqual(ProjectBId, projects[1].MemoryProjectId);
        Assert.AreEqual("project-b", projects[1].MemoryProjectName);
    }

    /// <summary>
    /// Verifies that project validation distinguishes exact matches, missing projects, mismatched names, mismatched identifiers, and conflicting identities.
    /// </summary>
    [TestMethod]
    public async Task ValidateAsyncDistinguishesIdentityOutcomes()
    {
        await using var fixture = await CreateFixtureAsync(
        [
            CreateProject(ProjectAId, "project-a"),
            CreateProject(ProjectBId, "project-b")
        ]);

        var exactMatch = await fixture.Directory.ValidateAsync(ProjectAId, "project-a");
        Assert.AreEqual(BasicMemoryProjectValidationStatus.ExactMatch, exactMatch.Status);
        Assert.AreEqual(ProjectAId, exactMatch.ProjectById?.MemoryProjectId);
        Assert.AreEqual(ProjectAId, exactMatch.ProjectByName?.MemoryProjectId);

        var notFound = await fixture.Directory.ValidateAsync(Guid.Parse("99999999-8888-7777-6666-555555555555"), "missing-project");
        Assert.AreEqual(BasicMemoryProjectValidationStatus.NotFound, notFound.Status);
        Assert.IsNull(notFound.ProjectById);
        Assert.IsNull(notFound.ProjectByName);

        var nameMismatch = await fixture.Directory.ValidateAsync(ProjectAId, "missing-project");
        Assert.AreEqual(BasicMemoryProjectValidationStatus.NameMismatch, nameMismatch.Status);
        Assert.AreEqual(ProjectAId, nameMismatch.ProjectById?.MemoryProjectId);
        Assert.IsNull(nameMismatch.ProjectByName);

        var idMismatch = await fixture.Directory.ValidateAsync(Guid.Parse("99999999-8888-7777-6666-555555555555"), "project-a");
        Assert.AreEqual(BasicMemoryProjectValidationStatus.IdMismatch, idMismatch.Status);
        Assert.IsNull(idMismatch.ProjectById);
        Assert.AreEqual(ProjectAId, idMismatch.ProjectByName?.MemoryProjectId);

        var identityConflict = await fixture.Directory.ValidateAsync(ProjectAId, "project-b");
        Assert.AreEqual(BasicMemoryProjectValidationStatus.IdentityConflict, identityConflict.Status);
        Assert.AreEqual(ProjectAId, identityConflict.ProjectById?.MemoryProjectId);
        Assert.AreEqual(ProjectBId, identityConflict.ProjectByName?.MemoryProjectId);
    }

    /// <summary>
    /// Verifies that project discovery fails closed when Basic Memory reports a project from a non-local source.
    /// </summary>
    [TestMethod]
    public async Task ListAsyncRejectsNonLocalProjects()
    {
        await using var fixture = await CreateFixtureAsync(
        [
            CreateProject(ProjectAId, "project-a", "cloud")
        ]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Directory.ListAsync());
    }

    /// <summary>
    /// Verifies that project discovery fails closed when Basic Memory returns an invalid external project identifier.
    /// </summary>
    [TestMethod]
    public async Task ListAsyncRejectsInvalidExternalIds()
    {
        await using var fixture = await CreateFixtureAsync(
        [
            new TestProject(
                "project-a",
                "not-a-guid",
                "local")
        ]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Directory.ListAsync());
    }

    /// <summary>
    /// Verifies that project discovery fails closed when Basic Memory returns the same external UUID for multiple projects.
    /// </summary>
    [TestMethod]
    public async Task ListAsyncRejectsDuplicateProjectIds()
    {
        await using var fixture = await CreateFixtureAsync(
        [
            CreateProject(ProjectAId, "project-a"),
            CreateProject(ProjectAId, "project-b")
        ]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Directory.ListAsync());
    }

    /// <summary>
    /// Verifies that project discovery fails closed when Basic Memory returns duplicate project names regardless of casing.
    /// </summary>
    [TestMethod]
    public async Task ListAsyncRejectsDuplicateProjectNames()
    {
        await using var fixture = await CreateFixtureAsync(
        [
            CreateProject(ProjectAId, "project-a"),
            CreateProject(ProjectBId, "PROJECT-A")
        ]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Directory.ListAsync());
    }

    /// <summary>
    /// Verifies that project validation rejects an empty expected Basic Memory project identifier before querying the project list.
    /// </summary>
    [TestMethod]
    public async Task ValidateAsyncRejectsEmptyProjectId()
    {
        await using var fixture = await CreateFixtureAsync([]);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Directory.ValidateAsync(Guid.Empty, "project-a"));
    }

    /// <summary>
    /// Verifies that project validation rejects an empty expected Basic Memory project name before querying the project list.
    /// </summary>
    [TestMethod]
    public async Task ValidateAsyncRejectsEmptyProjectName()
    {
        await using var fixture = await CreateFixtureAsync([]);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Directory.ValidateAsync(ProjectAId, " "));
    }

    #endregion

    #region Private Methods

    private static TestProject CreateProject(Guid projectId, string projectName, string source = "local")
    {
        return new TestProject(projectName, projectId.ToString("D"), source);
    }

    private static async Task<TestFixture> CreateFixtureAsync(IReadOnlyList<TestProject> projects, CancellationToken cancellationToken = default)
    {
        var state = new TestProjectListState(projects);

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
                var directory = new BasicMemoryProjectDirectory(toolCatalog);
                return new TestFixture(server, client, toolCatalog, directory);
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

        public TestFixture(WebApplication server, BasicMemoryClient client, BasicMemoryToolCatalog toolCatalog, BasicMemoryProjectDirectory directory)
        {
            Server = server;
            Client = client;
            ToolCatalog = toolCatalog;
            Directory = directory;
        }

        #endregion

        #region Properties

        public WebApplication Server { get; }

        public BasicMemoryClient Client { get; }

        public BasicMemoryToolCatalog ToolCatalog { get; }

        public BasicMemoryProjectDirectory Directory { get; }

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

    private sealed class TestProjectListState
    {
        #region Constructors

        public TestProjectListState(IReadOnlyList<TestProject> projects)
        {
            Projects = projects ?? throw new ArgumentNullException(nameof(projects));
        }

        #endregion

        #region Properties

        public IReadOnlyList<TestProject> Projects { get; }

        #endregion
    }

    [McpServerToolType]
    private sealed class TestBasicMemoryTools
    {
        #region Private Fields

        private readonly TestProjectListState _state;

        #endregion

        #region Constructors

        public TestBasicMemoryTools(TestProjectListState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Provides the structured Basic Memory project-list response used by project directory tests.
        /// </summary>
        [McpServerTool(Name = "list_memory_projects", ReadOnly = true, UseStructuredContent = true)]
        public ProjectListEnvelope ListMemoryProjects(string output_format = "text")
        {
            if (!string.Equals(output_format, "json", StringComparison.Ordinal))
                throw new InvalidOperationException("The project directory must request the JSON Basic Memory project-list contract.");

            return new ProjectListEnvelope(new ProjectListPayload(_state.Projects));
        }

        #endregion
    }

    private sealed record ProjectListEnvelope(
        [property: JsonPropertyName("result")] ProjectListPayload Result);

    private sealed record ProjectListPayload(
        [property: JsonPropertyName("projects")] IReadOnlyList<TestProject> Projects);

    private sealed record TestProject(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("external_id")] string ExternalId,
        [property: JsonPropertyName("source")] string Source);

    #endregion
}
