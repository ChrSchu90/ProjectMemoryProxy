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
using ModelContextProtocol.Server;
using ProjectMemoryProxy.Core.Configuration;

/// <summary>
/// Tests for <see cref="BasicMemoryToolCatalog"/>
/// </summary>
[TestClass]
public sealed class BasicMemoryToolCatalogTests
{
    #region Static Fields

    #endregion

    #region Private Fields

    #endregion

    #region Tests Constructors

    #endregion

    #region Test Preparation

    #endregion

    #region Tests

    /// <summary>
    /// Verifies that duplicate upstream tool names prevent catalog initialization without publishing a partial snapshot.
    /// </summary>
    [TestMethod]
    public async Task InitializeAsyncRejectsDuplicateToolNames()
    {
        await using var fixture = await CreateTestToolAsync();
        var client = new StubBasicMemoryClient([fixture.Tool, fixture.Tool]);
        using var catalog = new BasicMemoryToolCatalog(client, NullLogger<BasicMemoryToolCatalog>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.InitializeAsync());
        Assert.IsFalse(catalog.IsInitialized);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = catalog.Tools);
    }

    /// <summary>
    /// Verifies that successful initialization discovers upstream tools only once for the process lifetime.
    /// </summary>
    [TestMethod]
    public async Task InitializeAsyncDiscoversToolsOnlyOnce()
    {
        await using var fixture = await CreateTestToolAsync();
        var client = new StubBasicMemoryClient([fixture.Tool]);
        using var catalog = new BasicMemoryToolCatalog(client, NullLogger<BasicMemoryToolCatalog>.Instance);

        await catalog.InitializeAsync();
        await catalog.InitializeAsync();

        Assert.IsTrue(catalog.IsInitialized);
        Assert.AreEqual(1, client.ListToolsCallCount);
        Assert.AreEqual(1, catalog.Tools.Count);
    }

    /// <summary>
    /// Verifies that tool lookup uses the exact canonical upstream MCP tool name.
    /// </summary>
    [TestMethod]
    public async Task TryGetToolUsesExactToolName()
    {
        await using var fixture = await CreateTestToolAsync();
        var client = new StubBasicMemoryClient([fixture.Tool]);
        using var catalog = new BasicMemoryToolCatalog(client, NullLogger<BasicMemoryToolCatalog>.Instance);

        await catalog.InitializeAsync();
        Assert.IsTrue(catalog.TryGetTool(fixture.Tool.ProtocolTool.Name, out var result));
        Assert.AreSame(fixture.Tool, result);
        Assert.IsFalse(catalog.TryGetTool(fixture.Tool.ProtocolTool.Name.ToUpperInvariant(), out _));
    }

    /// <summary>
    /// Verifies that cancellation during discovery is propagated without initializing the tool catalog.
    /// </summary>
    [TestMethod]
    public async Task InitializeAsyncPropagatesCancellation()
    {
        var client = new StubBasicMemoryClient(cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<McpClientTool>>([]);
        });

        using var catalog = new BasicMemoryToolCatalog(client, NullLogger<BasicMemoryToolCatalog>.Instance);
        using var cancellationTokenSource = new CancellationTokenSource();

        await cancellationTokenSource.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => catalog.InitializeAsync(cancellationTokenSource.Token));
        Assert.IsFalse(catalog.IsInitialized);
    }

    #endregion

    #region Private Methods

    private static async Task<TestToolFixture> CreateTestToolAsync(CancellationToken cancellationToken = default)
    {
        var server = await CreateTestServerAsync(cancellationToken);

        try
        {
            var endpoint = GetMcpEndpoint(server);
            var options = Options.Create(new ProjectMemoryProxyOptions { BasicMemoryEndpoint = endpoint, BasicMemoryConnectionTimeout = TimeSpan.FromSeconds(5) });
            var client = new BasicMemoryClient(options, NullLoggerFactory.Instance, NullLogger<BasicMemoryClient>.Instance);

            try
            {
                var tools = await client.ListToolsAsync(cancellationToken);
                var tool = tools.Single(tool => tool.ProtocolTool.Name == "search_notes");
                return new TestToolFixture(server, client, tool);
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

    private static async Task<WebApplication> CreateTestServerAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));

        builder.Services
            .AddMcpServer()
            .WithHttpTransport(
                options =>
                {
                    options.SessionMode = HttpServerSessionMode.Stateless;
                })
            .WithTools<TestBasicMemoryTools>();

        var app = builder.Build();

        app.MapMcp("/mcp");
        await app.StartAsync(cancellationToken);
        return app;
    }

    private static Uri GetMcpEndpoint(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ?? 
                        throw new InvalidOperationException("The test MCP server did not publish any addresses.");

        var address = addresses.Single();
        return new Uri($"{address.TrimEnd('/')}/mcp");
    }

    #endregion

    #region Test Classes

    private sealed class TestToolFixture : IAsyncDisposable
    {
        #region Constructors

        public TestToolFixture(WebApplication server, BasicMemoryClient client, McpClientTool tool)
        {
            Server = server;
            Client = client;
            Tool = tool;
        }

        #endregion

        #region Properties

        public WebApplication Server { get; }

        public BasicMemoryClient Client { get; }

        public McpClientTool Tool { get; }

        #endregion

        #region Public Methods

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await Server.DisposeAsync();
        }

        #endregion
    }

    [McpServerToolType]
    private sealed class TestBasicMemoryTools
    {
        /// <summary>
        /// Provides a representative Basic Memory search tool for tool catalog tests.
        /// </summary>
        [McpServerTool(Name = "search_notes", ReadOnly = true)]
        [System.ComponentModel.Description("Searches notes in Basic Memory.")]
        public static string SearchNotes(
            [System.ComponentModel.Description("The search query.")] string query)
        {
            return query;
        }
    }

    private sealed class StubBasicMemoryClient : IBasicMemoryClient
    {
        #region Private Fields

        private readonly Func<CancellationToken, Task<IReadOnlyList<McpClientTool>>> _listTools;

        #endregion

        #region Constructors

        public StubBasicMemoryClient(IReadOnlyList<McpClientTool> tools)
            : this(_ => Task.FromResult(tools))
        {
        }

        public StubBasicMemoryClient(
            Func<CancellationToken, Task<IReadOnlyList<McpClientTool>>> listTools)
        {
            _listTools = listTools ?? throw new ArgumentNullException(nameof(listTools));
        }

        #endregion

        #region Properties

        public int ListToolsCallCount { get; private set; }

        #endregion

        #region Public Methods

        public Task<IReadOnlyList<McpClientTool>> ListToolsAsync(CancellationToken cancellationToken = default)
        {
            ListToolsCallCount++;
            return _listTools(cancellationToken);
        }

        #endregion
    }

    #endregion
}
