namespace ProjectMemoryProxy.BasicMemory.Tests.Health;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Server;
using ProjectMemoryProxy.BasicMemory.Client;
using ProjectMemoryProxy.BasicMemory.Discovery;
using ProjectMemoryProxy.BasicMemory.Health;
using ProjectMemoryProxy.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.AspNetCore;

/// <summary>
/// Tests for <see cref="BasicMemoryHealthProbe"/>.
/// </summary>
[TestClass]
public sealed class BasicMemoryHealthProbeTests
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
    /// Verifies that a successful <c>basic_memory_diagnostics</c> call completes the health probe successfully.
    /// </summary>
    [TestMethod]
    public async Task CheckAsyncSucceedsWhenDiagnosticsSucceeds()
    {
        await using var fixture = await CreateFixtureAsync(throwOnCall: false);
        var probe = new BasicMemoryHealthProbe(fixture.ToolCatalog);

        await probe.CheckAsync();
        Assert.AreEqual(1, fixture.State.CallCount);
    }

    /// <summary>
    /// Verifies that an MCP tool error from <c>basic_memory_diagnostics</c> fails the health probe.
    /// </summary>
    [TestMethod]
    public async Task CheckAsyncThrowsWhenDiagnosticsReturnsToolError()
    {
        await using var fixture = await CreateFixtureAsync(throwOnCall: true);
        var probe = new BasicMemoryHealthProbe(fixture.ToolCatalog);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => probe.CheckAsync());
        StringAssert.Contains(exception.Message, BasicMemoryToolNames.Diagnostics);
        Assert.AreEqual(1, fixture.State.CallCount);
    }

    /// <summary>
    /// Verifies that cancellation is propagated without converting it into a health failure.
    /// </summary>
    [TestMethod]
    public async Task CheckAsyncPropagatesCancellation()
    {
        var client = new StubBasicMemoryClient();
        using var toolCatalog = new BasicMemoryToolCatalog(client, NullLogger<BasicMemoryToolCatalog>.Instance);
        var probe = new BasicMemoryHealthProbe(toolCatalog);
        using var cancellationTokenSource = new CancellationTokenSource();

        await cancellationTokenSource.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => probe.CheckAsync(cancellationTokenSource.Token));
        Assert.AreEqual(0, client.ListToolsCallCount);
    }

    #endregion

    #region Private Methods

    private static async Task<TestFixture> CreateFixtureAsync(bool throwOnCall, CancellationToken cancellationToken = default)
    {
        var state = new DiagnosticsState(throwOnCall);
        var server = await StartBasicMemoryServerAsync(state, cancellationToken);

        try
        {
            var options = Options.Create(new ProjectMemoryProxyOptions
            {
                BasicMemoryEndpoint = GetMcpEndpoint(server),
                BasicMemoryConnectionTimeout = TimeSpan.FromSeconds(5)
            });

            var client = new BasicMemoryClient(options, NullLoggerFactory.Instance, NullLogger<BasicMemoryClient>.Instance);

            try
            {
                var toolCatalog = new BasicMemoryToolCatalog(client, NullLogger<BasicMemoryToolCatalog>.Instance);
                return new TestFixture(server, client, toolCatalog, state);
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

    private static async Task<WebApplication> StartBasicMemoryServerAsync(DiagnosticsState state, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddSingleton(state);
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

        return new Uri($"{addresses.Single().TrimEnd('/')}/mcp");
    }

    #endregion

    #region Test Classes

    private sealed class DiagnosticsState
    {
        #region Constructors

        public DiagnosticsState(bool throwOnCall)
        {
            ThrowOnCall = throwOnCall;
        }

        #endregion

        #region Properties

        public bool ThrowOnCall { get; }

        public int CallCount { get; private set; }

        #endregion

        #region Public Methods

        public string Execute()
        {
            CallCount++;

            if (ThrowOnCall)
                throw new InvalidOperationException("Diagnostics failed.");

            return "ok";
        }

        #endregion
    }

    [McpServerToolType]
    private sealed class TestBasicMemoryTools
    {
        #region Private Fields

        private readonly DiagnosticsState _state;

        #endregion

        #region Constructors

        public TestBasicMemoryTools(DiagnosticsState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Provides the Basic Memory diagnostics tool used by health-probe tests.
        /// </summary>
        [McpServerTool(Name = "basic_memory_diagnostics", ReadOnly = true)]
        public string Diagnostics()
        {
            return _state.Execute();
        }

        #endregion
    }

    private sealed class StubBasicMemoryClient : IBasicMemoryClient
    {
        #region Properties

        public int ListToolsCallCount { get; private set; }

        #endregion

        #region Public Methods

        public Task<IReadOnlyList<McpClientTool>> ListToolsAsync(CancellationToken cancellationToken = default)
        {
            ListToolsCallCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<McpClientTool>>([]);
        }

        #endregion
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        #region Constructors

        public TestFixture(WebApplication server, BasicMemoryClient client, BasicMemoryToolCatalog toolCatalog, DiagnosticsState state)
        {
            Server = server;
            Client = client;
            ToolCatalog = toolCatalog;
            State = state;
        }

        #endregion

        #region Properties

        public WebApplication Server { get; }

        public BasicMemoryClient Client { get; }

        public BasicMemoryToolCatalog ToolCatalog { get; }

        public DiagnosticsState State { get; }

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

    #endregion
}
