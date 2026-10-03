namespace ProjectMemoryProxy.BasicMemory.Tests;

using System;
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
using ModelContextProtocol.Server;
using ProjectMemoryProxy.Core.Configuration;

/// <summary>
/// Tests for <see cref="BasicMemoryMirroredToolCatalog"/>
/// </summary>
[TestClass]
public sealed class BasicMemoryMirroredToolCatalogTests
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
    /// Verifies that only generically routable and rewriteable upstream tools are included in the mirrored tool snapshot.
    /// </summary>
    [TestMethod]
    public async Task InitializeAsyncIncludesOnlyMirroredTools()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var fixture = await CreateFixtureAsync(cancellationTokenSource.Token);
        using var upstreamCatalog = new BasicMemoryToolCatalog(fixture.Client, NullLogger<BasicMemoryToolCatalog>.Instance);
        using var mirroredCatalog = new BasicMemoryMirroredToolCatalog(upstreamCatalog, NullLogger<BasicMemoryMirroredToolCatalog>.Instance);

        await mirroredCatalog.InitializeAsync(cancellationTokenSource.Token);
        Assert.IsTrue(mirroredCatalog.IsInitialized);
        Assert.AreEqual(1, mirroredCatalog.Tools.Count);

        var tool = mirroredCatalog.Tools.Single();
        Assert.AreEqual("read_note", tool.Name);

        var properties = tool.PublicInputSchema.GetProperty("properties");
        Assert.IsTrue(properties.TryGetProperty("identifier", out _));
        Assert.IsTrue(properties.TryGetProperty("context_id", out _));
        Assert.IsFalse(properties.TryGetProperty("project_id", out _));
    }

    /// <summary>
    /// Verifies that repeated initialization preserves the same process-lifetime mirrored tool snapshot.
    /// </summary>
    [TestMethod]
    public async Task InitializeAsyncBuildsSnapshotOnlyOnce()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var fixture = await CreateFixtureAsync(cancellationTokenSource.Token);
        using var upstreamCatalog = new BasicMemoryToolCatalog(fixture.Client, NullLogger<BasicMemoryToolCatalog>.Instance);
        using var mirroredCatalog = new BasicMemoryMirroredToolCatalog(upstreamCatalog, NullLogger<BasicMemoryMirroredToolCatalog>.Instance);

        await mirroredCatalog.InitializeAsync(cancellationTokenSource.Token);
        var firstSnapshot = mirroredCatalog.Tools;
        await mirroredCatalog.InitializeAsync(cancellationTokenSource.Token);
        Assert.AreSame(firstSnapshot, mirroredCatalog.Tools);
    }

    /// <summary>
    /// Verifies that mirrored tool lookup uses the exact canonical upstream MCP tool name.
    /// </summary>
    [TestMethod]
    public async Task TryGetToolUsesExactToolName()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var fixture = await CreateFixtureAsync(cancellationTokenSource.Token);
        using var upstreamCatalog = new BasicMemoryToolCatalog(fixture.Client, NullLogger<BasicMemoryToolCatalog>.Instance);
        using var mirroredCatalog = new BasicMemoryMirroredToolCatalog(upstreamCatalog, NullLogger<BasicMemoryMirroredToolCatalog>.Instance);

        await mirroredCatalog.InitializeAsync(cancellationTokenSource.Token);
        Assert.IsTrue(mirroredCatalog.TryGetTool("read_note", out var tool));
        Assert.IsNotNull(tool);
        Assert.IsFalse(mirroredCatalog.TryGetTool("READ_NOTE", out _));
    }

    #endregion

    #region Private Methods

    private static async Task<TestFixture> CreateFixtureAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
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
            return new TestFixture(server, client);
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

    [McpServerToolType]
    private sealed class TestBasicMemoryTools
    {
        /// <summary>
        /// Provides a representative automatically routed Basic Memory tool.
        /// </summary>
        [McpServerTool(Name = "read_note", ReadOnly = true)]
        public static string ReadNote(string identifier, string? project_id = null)
        {
            return identifier;
        }

        /// <summary>
        /// Provides a representative routingless tool that must not be generically mirrored.
        /// </summary>
        [McpServerTool(Name = "health", ReadOnly = true)]
        public static string Health()
        {
            return "ok";
        }

        /// <summary>
        /// Provides a compatibility tool that requires an explicit proxy adapter.
        /// </summary>
        [McpServerTool(Name = "search", ReadOnly = true)]
        public static string Search(string query, string? project_id = null)
        {
            return query;
        }
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        public TestFixture(WebApplication server, BasicMemoryClient client)
        {
            Server = server;
            Client = client;
        }

        public WebApplication Server { get; }

        public BasicMemoryClient Client { get; }

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await Server.DisposeAsync();
        }
    }

    #endregion
}
