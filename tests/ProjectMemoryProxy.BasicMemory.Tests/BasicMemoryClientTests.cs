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
/// Tests for <see cref="BasicMemoryClient"/>.
/// </summary>
[TestClass]
public sealed class BasicMemoryClientTests
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
    /// Verifies that the client connects to a Streamable HTTP MCP upstream and discovers its exposed tools.
    /// </summary>
    [TestMethod]
    public async Task ListToolsAsyncDiscoversUpstreamTools()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var server = await CreateTestServerAsync(cancellationTokenSource.Token);

        var endpoint = GetMcpEndpoint(server);
        var options = Options.Create(new ProjectMemoryProxyOptions { BasicMemoryEndpoint = endpoint, BasicMemoryConnectionTimeout = TimeSpan.FromSeconds(5) });
        await using var client = new BasicMemoryClient(options, NullLoggerFactory.Instance, NullLogger<BasicMemoryClient>.Instance);

        var tools = await client.ListToolsAsync(cancellationTokenSource.Token);
        var tool = tools.Single(tool => tool.Name == "search_notes");
        Assert.AreEqual("Searches notes in Basic Memory.", tool.Description);
        Assert.AreEqual("search_notes", tool.ProtocolTool.Name);

        var properties = tool.ProtocolTool.InputSchema.GetProperty("properties");
        Assert.IsTrue(properties.TryGetProperty("query", out _));
    }

    /// <summary>
    /// Verifies that cancellation requested before connecting to the upstream is propagated to the caller.
    /// </summary>
    [TestMethod]
    public async Task ListToolsAsyncPropagatesCancellation()
    {
        var options = Options.Create(new ProjectMemoryProxyOptions { BasicMemoryEndpoint = new Uri("http://127.0.0.1:1/mcp"), BasicMemoryConnectionTimeout = TimeSpan.FromSeconds(5) });
        await using var client = new BasicMemoryClient(options, NullLoggerFactory.Instance, NullLogger<BasicMemoryClient>.Instance);
        using var cancellationTokenSource = new CancellationTokenSource();

        await cancellationTokenSource.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => client.ListToolsAsync(cancellationTokenSource.Token));
    }

    #endregion

    #region Private Methods

    private static async Task<WebApplication> CreateTestServerAsync(CancellationToken cancellationToken)
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

    [McpServerToolType]
    private sealed class TestBasicMemoryTools
    {
        /// <summary>
        /// Provides a representative Basic Memory search tool for MCP discovery tests.
        /// </summary>
        [McpServerTool(Name = "search_notes", ReadOnly = true)]
        [System.ComponentModel.Description("Searches notes in Basic Memory.")]
        public static string SearchNotes([System.ComponentModel.Description("The search query.")] string query)
        {
            return query;
        }
    }

    #endregion
}
