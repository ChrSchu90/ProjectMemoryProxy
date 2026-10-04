namespace ProjectMemoryProxy.BasicMemory.Tests.MCP;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ProjectMemoryProxy.BasicMemory.Client;
using ProjectMemoryProxy.BasicMemory.Discovery;
using ProjectMemoryProxy.BasicMemory.MCP;
using ProjectMemoryProxy.BasicMemory.Mirroring;
using ProjectMemoryProxy.Core.Configuration;
using ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Tests for <see cref="BasicMemoryMirroredMcpServerTool"/>
/// </summary>
[TestClass]
public sealed class BasicMemoryMirroredMcpServerToolTests
{
    #region Static Fields

    private static readonly Guid MemoryProjectId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private const string MemoryProjectName = "project-memory-proxy";
    private const string ContextIdValue = "git:github.com/ChrSchu90/ProjectMemoryProxy";

    #endregion

    #region Private Fields

    #endregion

    #region Tests Constructors

    #endregion

    #region Test Preparation

    #endregion

    #region Tests

    /// <summary>
    /// Verifies that a mirrored MCP tool exposes the rewritten public schema and invokes Basic Memory with the server-resolved project identifier.
    /// </summary>
    [TestMethod]
    public async Task MirroredToolRoutesEndToEnd()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cancellationToken = cancellationTokenSource.Token;
        await using var upstreamServer = await StartBasicMemoryServerAsync(cancellationToken);
        await using var basicMemoryClient = CreateBasicMemoryClient(GetMcpEndpoint(upstreamServer));
        using var upstreamCatalog = new BasicMemoryToolCatalog(basicMemoryClient, NullLogger<BasicMemoryToolCatalog>.Instance);
        using var mirroredCatalog = new BasicMemoryMirroredToolCatalog(upstreamCatalog, NullLogger<BasicMemoryMirroredToolCatalog>.Instance);

        await mirroredCatalog.InitializeAsync(cancellationToken);
        Assert.IsTrue(mirroredCatalog.TryGetTool("read_note", out var mirroredTool));
        Assert.IsNotNull(mirroredTool);

        var routingManager = new RoutingManager(new StubProjectRegistry(new ProjectRoute(MemoryProjectId, MemoryProjectName, Status.Active, Status.Active)), NullLogger<RoutingManager>.Instance);
        var argumentBuilder = new BasicMemoryInvocationArgumentBuilder(routingManager);
        var serverTool = new BasicMemoryMirroredMcpServerTool(mirroredTool, argumentBuilder);
        await using var proxyServer = await StartProxyServerAsync(serverTool, cancellationToken);
        await using var proxyClient = await CreateMcpClientAsync(GetMcpEndpoint(proxyServer), cancellationToken);

        var tools = await proxyClient.ListToolsAsync(cancellationToken: cancellationToken);
        Assert.HasCount(1, tools);

        var publicTool = tools.Single();
        Assert.AreEqual("read_note", publicTool.Name);

        AssertPublicSchema(publicTool.ProtocolTool.InputSchema);
        var result = await publicTool.CallAsync(new Dictionary<string, object?>
        {
            ["identifier"] = "notes/example",
            ["context_id"] = ContextIdValue
        }, cancellationToken: cancellationToken);

        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        Assert.AreEqual($"notes/example|{MemoryProjectId:D}", text);
    }

    #endregion

    #region Private Methods

    private static async Task<WebApplication> StartBasicMemoryServerAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services
            .AddMcpServer()
            .WithHttpTransport(options => { options.SessionMode = HttpServerSessionMode.Stateless; })
            .WithTools<TestBasicMemoryTools>();

        var app = builder.Build();
        app.MapMcp("/mcp");
        await app.StartAsync(cancellationToken);
        return app;
    }

    private static async Task<WebApplication> StartProxyServerAsync(McpServerTool serverTool, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services
            .AddMcpServer()
            .WithHttpTransport(options => { options.SessionMode = HttpServerSessionMode.Stateless; })
            .WithTools(new[] { serverTool });

        var app = builder.Build();
        app.MapMcp("/mcp");
        await app.StartAsync(cancellationToken);
        return app;
    }

    private static BasicMemoryClient CreateBasicMemoryClient(Uri endpoint)
    {
        var options = Options.Create(new ProjectMemoryProxyOptions
        {
            BasicMemoryEndpoint = endpoint,
            BasicMemoryConnectionTimeout = TimeSpan.FromSeconds(5)
        });

        return new BasicMemoryClient(options, NullLoggerFactory.Instance, NullLogger<BasicMemoryClient>.Instance);
    }

    private static async Task<McpClient> CreateMcpClientAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp,
            ConnectionTimeout = TimeSpan.FromSeconds(5),
            EnableStandaloneGetStream = false
        });

        return await McpClient.CreateAsync(transport, loggerFactory: NullLoggerFactory.Instance, cancellationToken: cancellationToken);
    }

    private static Uri GetMcpEndpoint(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ??
                        throw new InvalidOperationException("The test MCP server did not publish any addresses.");

        return new Uri($"{addresses.Single().TrimEnd('/')}/mcp");
    }

    private static void AssertPublicSchema(System.Text.Json.JsonElement schema)
    {
        var properties = schema.GetProperty("properties");
        Assert.IsTrue(properties.TryGetProperty("identifier", out _));
        Assert.IsTrue(properties.TryGetProperty("context_id", out _));
        Assert.IsFalse(properties.TryGetProperty("project", out _));
        Assert.IsFalse(properties.TryGetProperty("project_id", out _));
        Assert.IsFalse(schema.GetProperty("additionalProperties").GetBoolean());

        var required = schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray();
        CollectionAssert.Contains(required, "identifier");
        CollectionAssert.Contains(required, "context_id");
    }

    #endregion

    #region Test Classes

    [McpServerToolType]
    private sealed class TestBasicMemoryTools
    {
        /// <summary>
        /// Provides a representative project-routed Basic Memory operation.
        /// </summary>
        [McpServerTool(Name = "read_note", ReadOnly = true)]
        public static string ReadNote(string identifier, string? project_id = null)
        {
            return $"{identifier}|{project_id}";
        }
    }

    private sealed class StubProjectRegistry : IProjectRegistry
    {
        #region Private Fields

        private readonly ProjectRoute? _route;

        #endregion

        #region Constructors

        public StubProjectRegistry(ProjectRoute? route)
        {
            _route = route;
        }

        #endregion

        #region Public Methods

        public Task<ProjectRoute?> FindRouteAsync(ContextId contextId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_route);
        }

        #endregion
    }

    #endregion
}
