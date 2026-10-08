namespace ProjectMemoryProxy.Server.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ProjectMemoryProxy.BasicMemory;
using ProjectMemoryProxy.BasicMemory.Projects.Directory;
using ProjectMemoryProxy.BasicMemory.Projects.Lifecycle;
using ProjectMemoryProxy.Core.Configuration;
using ProjectMemoryProxy.Persistence;
using ProjectMemoryProxy.Server.Hosting;
using ProjectMemoryProxy.Server.Tools;

/// <summary>
/// Live end-to-end contract tests for the complete ProjectMemoryProxy MCP server surface.
/// </summary>
[TestClass]
public sealed class ProjectMemoryProxyContractTests
{
    #region Static Fields

    private static readonly string[] EnabledProxyOwnedTools =
    [
        "bind_context",
        "create_project",
        "delete_project",
        "list_context_bindings",
        "list_projects",
        "resolve_context",
        "unbind_context"
    ];

    private static readonly string[] HiddenStatusTransitionTools =
    [
        "deactivate_context",
        "deactivate_project",
        "reactivate_context",
        "reactivate_project"
    ];

    #endregion

    #region Private Fields

    #endregion

    #region Tests Constructors

    #endregion

    #region Test Preparation

    #endregion

    #region Tests

    /// <summary>
    /// Verifies that a configured live Basic Memory endpoint supports the complete proxy create, bind, route, unbind, rebind, and delete lifecycle through MCP.
    /// </summary>
    [TestMethod]
    public async Task LiveBasicMemoryEndpointSupportsProxyControlPlaneLifecycle()
    {
        var basicMemoryEndpoint = GetTestBasicMemoryEndpointOrSkipTest();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var cancellationToken = cancellationTokenSource.Token;
        var testId = Guid.NewGuid().ToString("N");
        var projectName = $"pmp-contract-{testId}";
        var contextId = $"git:contract.invalid/{projectName}";

        // The official Basic Memory Docker image uses BASIC_MEMORY_PROJECT_ROOT and resolves the real project path remotely.
        // This caller-local absolute path only satisfies the current ProjectMemoryProxy create_project contract; the test never accesses the remote filesystem.
        var requestedProjectPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ProjectMemoryProxy.ContractTests", projectName));
        var dataDirectory = Path.Combine(Path.GetTempPath(), "ProjectMemoryProxy.ContractTests", $"proxy-{testId}");

        Directory.CreateDirectory(dataDirectory);

        try
        {
            await using var proxyServer = await StartProxyServerAsync(basicMemoryEndpoint, dataDirectory, cancellationToken);

            try
            {
                await using var proxyClient = await CreateMcpClientAsync(GetMcpEndpoint(proxyServer), cancellationToken);
                var tools = await GetToolsByNameAsync(proxyClient, cancellationToken);

                AssertProxyToolSurface(tools);

                var createResult = GetStructuredContent(await tools["create_project"].CallAsync(new Dictionary<string, object?>
                {
                    ["project_name"] = projectName,
                    ["project_path"] = requestedProjectPath
                }, cancellationToken: cancellationToken), "create_project");

                Assert.AreEqual("created", createResult.GetProperty("status").GetString());
                var createdProject = createResult.GetProperty("project");
                Assert.AreEqual(projectName, createdProject.GetProperty("project_name").GetString());
                Assert.AreEqual("active", createdProject.GetProperty("status").GetString());
                Assert.IsTrue(Guid.TryParse(createdProject.GetProperty("project_id").GetString(), out var projectId));
                Assert.AreNotEqual(Guid.Empty, projectId);

                var projectsAfterCreate = GetStructuredContent(
                    await tools["list_projects"].CallAsync(new Dictionary<string, object?>(), cancellationToken: cancellationToken),
                    "list_projects");

                Assert.IsTrue(projectsAfterCreate.GetProperty("projects").EnumerateArray().Any(project =>
                    project.GetProperty("project_id").GetString() == projectId.ToString("D") &&
                    project.GetProperty("project_name").GetString() == projectName &&
                    project.GetProperty("status").GetString() == "active"));

                var bindResult = GetStructuredContent(await tools["bind_context"].CallAsync(new Dictionary<string, object?>
                {
                    ["context_id"] = contextId,
                    ["project_id"] = projectId.ToString("D")
                }, cancellationToken: cancellationToken), "bind_context");

                Assert.AreEqual("bound", bindResult.GetProperty("status").GetString());

                var bindingsAfterBind = GetStructuredContent(
                    await tools["list_context_bindings"].CallAsync(new Dictionary<string, object?>(), cancellationToken: cancellationToken),
                    "list_context_bindings");

                Assert.IsTrue(bindingsAfterBind.GetProperty("bindings").EnumerateArray().Any(binding =>
                    binding.GetProperty("context_id").GetString() == contextId &&
                    binding.GetProperty("project_id").GetString() == projectId.ToString("D") &&
                    binding.GetProperty("status").GetString() == "active"));

                var resolveResult = GetStructuredContent(await tools["resolve_context"].CallAsync(new Dictionary<string, object?>
                {
                    ["context_id"] = contextId
                }, cancellationToken: cancellationToken), "resolve_context");

                Assert.AreEqual("resolved", resolveResult.GetProperty("status").GetString());
                Assert.AreEqual(projectId.ToString("D"), resolveResult.GetProperty("project_id").GetString());

                var unbindResult = GetStructuredContent(await tools["unbind_context"].CallAsync(new Dictionary<string, object?>
                {
                    ["context_id"] = contextId
                }, cancellationToken: cancellationToken), "unbind_context");

                Assert.AreEqual("unbound", unbindResult.GetProperty("status").GetString());
                Assert.AreEqual(JsonValueKind.Null, unbindResult.GetProperty("binding").ValueKind);

                var resolveAfterUnbind = GetStructuredContent(await tools["resolve_context"].CallAsync(new Dictionary<string, object?>
                {
                    ["context_id"] = contextId
                }, cancellationToken: cancellationToken), "resolve_context");

                Assert.AreEqual("not_bound", resolveAfterUnbind.GetProperty("status").GetString());
                Assert.AreEqual(JsonValueKind.Null, resolveAfterUnbind.GetProperty("project_id").ValueKind);

                var rebindResult = GetStructuredContent(await tools["bind_context"].CallAsync(new Dictionary<string, object?>
                {
                    ["context_id"] = contextId,
                    ["project_id"] = projectId.ToString("D")
                }, cancellationToken: cancellationToken), "bind_context");

                Assert.AreEqual("bound", rebindResult.GetProperty("status").GetString());

                var noteContent = $"ProjectMemoryProxy live contract {testId}";
                var writeResult = await tools["write_note"].CallAsync(new Dictionary<string, object?>
                {
                    ["title"] = "Contract Note",
                    ["content"] = noteContent,
                    ["directory"] = "contract-tests",
                    ["context_id"] = contextId
                }, cancellationToken: cancellationToken);

                AssertToolSucceeded(writeResult, "write_note");

                var readResult = await tools["read_note"].CallAsync(new Dictionary<string, object?>
                {
                    ["identifier"] = "Contract Note",
                    ["context_id"] = contextId
                }, cancellationToken: cancellationToken);

                AssertToolSucceeded(readResult, "read_note");
                var readText = string.Join(Environment.NewLine, readResult.Content.OfType<TextContentBlock>().Select(block => block.Text));
                Assert.Contains(noteContent, readText);

                var deleteResult = GetStructuredContent(await tools["delete_project"].CallAsync(new Dictionary<string, object?>
                {
                    ["project_id"] = projectId.ToString("D"),
                    ["delete_notes"] = true
                }, cancellationToken: cancellationToken), "delete_project");

                Assert.AreEqual("deleted", deleteResult.GetProperty("status").GetString());

                var projectsAfterDelete = GetStructuredContent(
                    await tools["list_projects"].CallAsync(new Dictionary<string, object?>(), cancellationToken: cancellationToken),
                    "list_projects");

                Assert.IsFalse(projectsAfterDelete.GetProperty("projects").EnumerateArray().Any(project =>
                    project.GetProperty("project_id").GetString() == projectId.ToString("D")));

                var bindingsAfterDelete = GetStructuredContent(
                    await tools["list_context_bindings"].CallAsync(new Dictionary<string, object?>(), cancellationToken: cancellationToken),
                    "list_context_bindings");

                Assert.IsFalse(bindingsAfterDelete.GetProperty("bindings").EnumerateArray().Any(binding =>
                    binding.GetProperty("context_id").GetString() == contextId));

                var resolveAfterDelete = GetStructuredContent(await tools["resolve_context"].CallAsync(new Dictionary<string, object?>
                {
                    ["context_id"] = contextId
                }, cancellationToken: cancellationToken), "resolve_context");

                Assert.AreEqual("not_bound", resolveAfterDelete.GetProperty("status").GetString());
                Assert.AreEqual(JsonValueKind.Null, resolveAfterDelete.GetProperty("project_id").ValueKind);
            }
            finally
            {
                using var cleanupCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await DeleteUpstreamProjectIfPresentAsync(proxyServer.Services, projectName, cleanupCancellationTokenSource.Token);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(dataDirectory))
                Directory.Delete(dataDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Verifies that two active contexts with the same note title resolve to separate Basic Memory project identities and never read each other's content.
    /// </summary>
    [TestMethod]
    public async Task LiveBasicMemoryEndpointIsolatesNotesAcrossProjects()
    {
        var basicMemoryEndpoint = GetTestBasicMemoryEndpointOrSkipTest();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var cancellationToken = timeout.Token;
        var suffix = Guid.NewGuid().ToString("N");
        var projectA = $"pmp-isolation-a-{suffix}";
        var projectB = $"pmp-isolation-b-{suffix}";
        var contextA = $"git:contract.invalid/{projectA}";
        var contextB = $"git:contract.invalid/{projectB}";
        var markerA = $"content-from-project-a-{suffix}";
        var markerB = $"content-from-project-b-{suffix}";
        var dataDirectory = Path.Combine(Path.GetTempPath(), "ProjectMemoryProxy.ContractTests", $"isolation-{suffix}");
        Directory.CreateDirectory(dataDirectory);

        try
        {
            await using var proxyServer = await StartProxyServerAsync(basicMemoryEndpoint, dataDirectory, cancellationToken);
            try
            {
                await using var client = await CreateMcpClientAsync(GetMcpEndpoint(proxyServer), cancellationToken);
                var tools = await GetToolsByNameAsync(client, cancellationToken);

                async Task<string> CreateAndBindAsync(string projectName, string contextId)
                {
                    var path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ProjectMemoryProxy.ContractTests", projectName));
                    var created = GetStructuredContent(await tools["create_project"].CallAsync(new Dictionary<string, object?>
                    {
                        ["project_name"] = projectName,
                        ["project_path"] = path
                    }, cancellationToken: cancellationToken), "create_project");

                    Assert.AreEqual("created", created.GetProperty("status").GetString());
                    var projectId = created.GetProperty("project").GetProperty("project_id").GetString()!;
                    Assert.IsTrue(Guid.TryParse(projectId, out var parsedId));
                    Assert.AreNotEqual(Guid.Empty, parsedId);

                    var bound = GetStructuredContent(await tools["bind_context"].CallAsync(new Dictionary<string, object?>
                    {
                        ["context_id"] = contextId,
                        ["project_id"] = projectId
                    }, cancellationToken: cancellationToken), "bind_context");

                    Assert.AreEqual("bound", bound.GetProperty("status").GetString());
                    return projectId;
                }

                var idA = await CreateAndBindAsync(projectA, contextA);
                var idB = await CreateAndBindAsync(projectB, contextB);
                Assert.AreNotEqual(idA, idB);

                async Task WriteAsync(string contextId, string content)
                {
                    AssertToolSucceeded(await tools["write_note"].CallAsync(new Dictionary<string, object?>
                    {
                        ["title"] = "Same Title",
                        ["directory"] = "isolation-tests",
                        ["content"] = content,
                        ["context_id"] = contextId
                    }, cancellationToken: cancellationToken), "write_note");
                }

                await WriteAsync(contextA, markerA);
                await WriteAsync(contextB, markerB);

                async Task<string> ReadAsync(string contextId)
                {
                    var result = await tools["read_note"].CallAsync(new Dictionary<string, object?>
                    {
                        ["identifier"] = "Same Title",
                        ["context_id"] = contextId
                    }, cancellationToken: cancellationToken);
                    AssertToolSucceeded(result, "read_note");
                    return string.Join(Environment.NewLine, result.Content.OfType<TextContentBlock>().Select(block => block.Text));
                }

                var textA = await ReadAsync(contextA);
                var textB = await ReadAsync(contextB);
                Assert.Contains(markerA, textA);
                Assert.IsFalse(textA.Contains(markerB, StringComparison.Ordinal));
                Assert.Contains(markerB, textB);
                Assert.IsFalse(textB.Contains(markerA, StringComparison.Ordinal));
            }
            finally
            {
                using var cleanupTimeoutA = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await DeleteUpstreamProjectIfPresentAsync(proxyServer.Services, projectA, cleanupTimeoutA.Token);
                using var cleanupTimeoutB = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await DeleteUpstreamProjectIfPresentAsync(proxyServer.Services, projectB, cleanupTimeoutB.Token);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(dataDirectory))
                Directory.Delete(dataDirectory, recursive: true);
        }
    }

    #endregion

    #region Private Methods

    private static Uri GetTestBasicMemoryEndpointOrSkipTest()
    {
        const string EndpointEnvironmentVariable = "PROJECTMEMORYPROXY_TEST_BASIC_MEMORY_ENDPOINT";
        var endpointValue = Environment.GetEnvironmentVariable(EndpointEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(endpointValue))
        {
            Assert.Inconclusive("Endpoint not specified.");
            return null!;
        }

        if (!string.Equals(endpointValue, endpointValue.Trim(), StringComparison.Ordinal) ||
            !Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            Assert.Fail($"{EndpointEnvironmentVariable} must be an absolute HTTP or HTTPS URI without surrounding whitespace.");
            return null!;
        }

        return endpoint;
    }

    private static async Task<WebApplication> StartProxyServerAsync(Uri basicMemoryEndpoint, string dataDirectory, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddLogging();
        builder.Services.AddSingleton(Options.Create(new ProjectMemoryProxyOptions
        {
            DataDirectory = dataDirectory,
            BasicMemoryEndpoint = basicMemoryEndpoint,
            BasicMemoryConnectionTimeout = TimeSpan.FromSeconds(10)
        }));

        builder.Services.AddPersistence();
        builder.Services.AddBasicMemory();
        builder.Services.AddHostedService<BasicMemoryStartupHostedService>();

        var mcpServerBuilder = builder.Services
            .AddMcpServer()
            .WithHttpTransport(options => { options.SessionMode = HttpServerSessionMode.Stateless; });

        mcpServerBuilder.WithTools<RoutingTools>();
        mcpServerBuilder.WithTools<ControlPlaneTools>();

        var app = builder.Build();

        try
        {
            await app.Services.MigrateDatabaseAsync(cancellationToken);
            app.MapMcp("/mcp");
            await app.StartAsync(cancellationToken);
            return app;
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    private static async Task<McpClient> CreateMcpClientAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp,
            ConnectionTimeout = TimeSpan.FromSeconds(10),
            EnableStandaloneGetStream = false
        });

        return await McpClient.CreateAsync(transport, loggerFactory: NullLoggerFactory.Instance, cancellationToken: cancellationToken);
    }

    private static async Task<IReadOnlyDictionary<string, McpClientTool>> GetToolsByNameAsync(McpClient proxyClient, CancellationToken cancellationToken)
    {
        var tools = await proxyClient.ListToolsAsync(cancellationToken: cancellationToken);
        return tools.ToDictionary(tool => tool.Name, StringComparer.Ordinal);
    }

    private static Uri GetMcpEndpoint(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ??
                        throw new InvalidOperationException("The test MCP server did not publish any addresses.");

        return new Uri($"{addresses.Single().TrimEnd('/')}/mcp");
    }

    private static void AssertProxyToolSurface(IReadOnlyDictionary<string, McpClientTool> tools)
    {
        Assert.HasCount(22, tools, "The complete proxy should expose 15 Basic Memory-derived tools plus seven proxy-owned tools.");

        foreach (var toolName in EnabledProxyOwnedTools)
        {
            Assert.IsTrue(tools.ContainsKey(toolName), $"Expected proxy-owned tool '{toolName}' was not exposed.");
        }

        foreach (var toolName in HiddenStatusTransitionTools)
        {
            Assert.IsFalse(tools.ContainsKey(toolName), $"Prepared status-transition tool '{toolName}' must remain hidden.");
        }

        Assert.IsTrue(tools.ContainsKey("write_note"), "The routed Basic Memory write tool must be exposed.");
        Assert.IsTrue(tools.ContainsKey("read_note"), "The routed Basic Memory read tool must be exposed.");
    }

    private static JsonElement GetStructuredContent(CallToolResult result, string toolName)
    {
        AssertToolSucceeded(result, toolName);

        if (result.StructuredContent is not { } structuredContent)
        {
            Assert.Fail($"Tool '{toolName}' returned no structured content.");
            return default;
        }

        return structuredContent;
    }

    private static void AssertToolSucceeded(CallToolResult result, string toolName)
    {
        if (result.IsError is not true)
            return;

        var errorText = string.Join(Environment.NewLine, result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        Assert.Fail(string.IsNullOrWhiteSpace(errorText) ? $"Tool '{toolName}' returned an MCP tool error." : $"Tool '{toolName}' returned an MCP tool error: {errorText}");
    }

    private static async Task DeleteUpstreamProjectIfPresentAsync(IServiceProvider serviceProvider, string projectName, CancellationToken cancellationToken)
    {
        try
        {
            var projectDirectory = serviceProvider.GetRequiredService<IBasicMemoryProjectDirectory>();
            var projects = await projectDirectory.ListAsync(cancellationToken);
            if (!projects.Any(project => string.Equals(project.MemoryProjectName, projectName, StringComparison.Ordinal)))
                return;

            var projectLifecycle = serviceProvider.GetRequiredService<IBasicMemoryProjectLifecycle>();
            await projectLifecycle.DeleteAsync(projectName, deleteNotes: true, cancellationToken);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Cleanup for Basic Memory project '{projectName}' failed: {exception}");
        }
    }

    #endregion
}
