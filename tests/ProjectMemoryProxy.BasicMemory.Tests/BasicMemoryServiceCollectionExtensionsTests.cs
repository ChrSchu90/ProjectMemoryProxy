namespace ProjectMemoryProxy.BasicMemory.Tests;

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
using ProjectMemoryProxy.BasicMemory.Mirroring;
using ProjectMemoryProxy.BasicMemory.Projects;
using ProjectMemoryProxy.BasicMemory.Projects.Directory;
using ProjectMemoryProxy.BasicMemory.Projects.Lifecycle;
using ProjectMemoryProxy.Core.Configuration;
using ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Tests for <see cref="BasicMemoryServiceCollectionExtensions"/>
/// </summary>
[TestClass]
public sealed class BasicMemoryServiceCollectionExtensionsTests
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
    /// Verifies that the Basic Memory client can be constructed from the dependency injection container.
    /// </summary>
    [TestMethod]
    public async Task RegisterServices()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(Options.Create(new ProjectMemoryProxyOptions
        {
            BasicMemoryEndpoint = new Uri("http://127.0.0.1:5101/mcp"),
            BasicMemoryConnectionTimeout = TimeSpan.FromSeconds(30)
        }));

        services.AddSingleton<IProjectRegistry>(new StubProjectRegistry(null));
        
        services.AddBasicMemory();
        await using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Assert.IsNotNull(serviceProvider.GetRequiredService<IBasicMemoryClient>());
        Assert.IsNotNull(serviceProvider.GetRequiredService<BasicMemoryToolCatalog>());
        Assert.IsNotNull(serviceProvider.GetRequiredService<BasicMemoryMirroredToolCatalog>());
        Assert.IsNotNull(serviceProvider.GetRequiredService<IBasicMemoryProjectDirectory>());
        Assert.IsNotNull(serviceProvider.GetRequiredService<IBasicMemoryProjectLifecycle>());
        Assert.IsNotNull(serviceProvider.GetRequiredService<ProjectRegistryManager>());
    }

    /// <summary>
    /// Verifies that startup registration adds safely mirrored Basic Memory tools while preserving existing MCP tools.
    /// </summary>
    [TestMethod]
    public async Task RegisterBasicMemoryMirroredToolsAsyncAddsMirroredTools()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cancellationToken = cancellationTokenSource.Token;
        await using var upstreamServer = await StartBasicMemoryServerAsync(cancellationToken);

        var builder = CreateProxyBuilder(GetMcpEndpoint(upstreamServer));
        builder.Services
            .AddMcpServer()
            .WithHttpTransport(options => { options.SessionMode = HttpServerSessionMode.Stateless; })
            .WithTools<NativeTools>();

        await using var proxyServer = builder.Build();
        await proxyServer.Services.RegisterBasicMemoryMirroredToolsAsync(cancellationToken);

        proxyServer.MapMcp("/mcp");
        await proxyServer.StartAsync(cancellationToken);

        await using var proxyClient = await CreateMcpClientAsync(GetMcpEndpoint(proxyServer), cancellationToken);
        var tools = await proxyClient.ListToolsAsync(cancellationToken: cancellationToken);
        Assert.IsTrue(tools.Any(tool => tool.Name == "native_tool"));
        Assert.IsTrue(tools.Any(tool => tool.Name == "read_note"));

        var readNote = tools.Single(tool => tool.Name == "read_note");
        var result = await readNote.CallAsync(new Dictionary<string, object?>
        {
            ["identifier"] = "notes/example",
            ["context_id"] = ContextIdValue
        }, cancellationToken: cancellationToken);

        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        Assert.AreEqual($"notes/example|{MemoryProjectId:D}", text);
    }

    /// <summary>
    /// Verifies that startup registration fails when a mirrored Basic Memory tool conflicts with an existing proxy-owned MCP tool.
    /// </summary>
    [TestMethod]
    public async Task RegisterBasicMemoryMirroredToolsAsyncRejectsToolNameCollision()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cancellationToken = cancellationTokenSource.Token;

        await using var upstreamServer = await StartBasicMemoryServerAsync(cancellationToken);
        var builder = CreateProxyBuilder(GetMcpEndpoint(upstreamServer));
        builder.Services
            .AddMcpServer()
            .WithHttpTransport(options => { options.SessionMode = HttpServerSessionMode.Stateless; })
            .WithTools<ConflictingTools>();

        await using var app = builder.Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() => app.Services.RegisterBasicMemoryMirroredToolsAsync(cancellationToken));
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

    private static WebApplicationBuilder CreateProxyBuilder(Uri basicMemoryEndpoint)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddLogging();
        builder.Services.AddSingleton(Options.Create(new ProjectMemoryProxyOptions
        {
            BasicMemoryEndpoint = basicMemoryEndpoint,
            BasicMemoryConnectionTimeout = TimeSpan.FromSeconds(5)
        }));

        builder.Services.AddSingleton<IProjectRegistry>(new StubProjectRegistry(new ProjectRoute(MemoryProjectId, MemoryProjectName, Status.Active, Status.Active)));
        builder.Services.AddSingleton<RoutingManager>();
        builder.Services.AddBasicMemory();

        return builder;
    }

    private static Uri GetMcpEndpoint(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ??
                        throw new InvalidOperationException("The test MCP server did not publish any addresses.");

        return new Uri($"{addresses.Single().TrimEnd('/')}/mcp");
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

    [McpServerToolType]
    private sealed class NativeTools
    {
        /// <summary>
        /// Provides a representative proxy-owned MCP tool.
        /// </summary>
        [McpServerTool(Name = "native_tool", ReadOnly = true)]
        public static string NativeTool()
        {
            return "native";
        }
    }

    [McpServerToolType]
    private sealed class ConflictingTools
    {
        /// <summary>
        /// Provides a proxy-owned tool whose name intentionally conflicts with the mirrored upstream tool.
        /// </summary>
        [McpServerTool(Name = "read_note", ReadOnly = true)]
        public static string ReadNote()
        {
            return "proxy";
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

        public Task<ProjectRouting?> FindByMemoryProjectIdAsync(Guid memoryProjectId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProjectRouting?> FindByMemoryProjectNameAsync(string memoryProjectName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProjectRouting> CreateAsync(Guid memoryProjectId, string memoryProjectName, Status status, CancellationToken cancellationToken = default)
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
            throw new NotSupportedException();
        }

        public Task<bool> TryUpdateBindingStatusAsync(ContextId contextId, Status expectedStatus, Status newStatus, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TryRemoveBindingAsync(ContextId contextId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TryRemoveProjectAsync(Guid memoryProjectId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        #endregion
    }

    #endregion
}
