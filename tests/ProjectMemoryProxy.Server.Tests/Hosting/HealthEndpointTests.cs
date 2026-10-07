namespace ProjectMemoryProxy.Server.Tests.Hosting;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectMemoryProxy.BasicMemory.Health;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using ProjectMemoryProxy.Server.Hosting;

/// <summary>
/// Tests for <see cref="HealthEndpoint"/>
/// </summary>
[TestClass]
public sealed class HealthEndpointTests
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
    /// Verifies that liveness and readiness return HTTP 200 when ProjectMemoryProxy and Basic Memory are healthy.
    /// </summary>
    [TestMethod]
    public async Task HealthEndpointsReturnOkWhenBasicMemoryIsHealthy()
    {
        await using var app = await StartServerAsync(new StubBasicMemoryHealthProbe(_ => Task.CompletedTask));
        using var client = new HttpClient { BaseAddress = GetBaseAddress(app) };
        using var liveResponse = await client.GetAsync("health/live");
        using var readyResponse = await client.GetAsync("health/ready");
        Assert.AreEqual(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, readyResponse.StatusCode);
    }

    /// <summary>
    /// Verifies that liveness remains HTTP 200 while readiness returns HTTP 503 when Basic Memory is unavailable.
    /// </summary>
    [TestMethod]
    public async Task ReadyEndpointReturnsServiceUnavailableWhenBasicMemoryFails()
    {
        var probe = new StubBasicMemoryHealthProbe(_ => Task.FromException(new InvalidOperationException("Diagnostics failed.")));
        await using var app = await StartServerAsync(probe);
        using var client = new HttpClient { BaseAddress = GetBaseAddress(app) };
        using var liveResponse = await client.GetAsync("health/live");
        using var readyResponse = await client.GetAsync("health/ready");
        Assert.AreEqual(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, readyResponse.StatusCode);
    }

    #endregion

    #region Private Methods

    private static async Task<WebApplication> StartServerAsync(IBasicMemoryHealthProbe healthProbe, CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddSingleton(healthProbe);
        builder.Services.AddHealthChecks().AddCheck<BasicMemoryHealthCheck>("basic-memory", tags: ["ready"]);

        var app = builder.Build();
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = healthCheck => healthCheck.Tags.Contains("ready")
        });

        await app.StartAsync(cancellationToken);

        return app;
    }

    private static Uri GetBaseAddress(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ??
                        throw new InvalidOperationException("The test HTTP server did not publish any addresses.");

        return new Uri($"{addresses.Single().TrimEnd('/')}/");
    }

    #endregion

    #region Test Classes

    private sealed class StubBasicMemoryHealthProbe : IBasicMemoryHealthProbe
    {
        #region Private Fields

        private readonly Func<CancellationToken, Task> _check;

        #endregion

        #region Constructors

        public StubBasicMemoryHealthProbe(Func<CancellationToken, Task> check)
        {
            _check = check ?? throw new ArgumentNullException(nameof(check));
        }

        #endregion

        #region Public Methods

        public Task CheckAsync(CancellationToken cancellationToken = default)
        {
            return _check(cancellationToken);
        }

        #endregion
    }

    #endregion
}
