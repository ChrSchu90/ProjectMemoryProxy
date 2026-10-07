namespace ProjectMemoryProxy.Server.Tests.Hosting;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectMemoryProxy.BasicMemory.Health;
using ProjectMemoryProxy.Server.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Tests for <see cref="BasicMemoryHealthCheck"/>
/// </summary>
[TestClass]
public sealed class BasicMemoryHealthCheckTests
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
    /// Verifies that a successful Basic Memory probe produces a healthy readiness result.
    /// </summary>
    [TestMethod]
    public async Task CheckHealthAsyncReturnsHealthyWhenProbeSucceeds()
    {
        var probe = new StubBasicMemoryHealthProbe(_ => Task.CompletedTask);
        var healthCheck = new BasicMemoryHealthCheck(probe);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());
        Assert.AreEqual(HealthStatus.Healthy, result.Status);
        Assert.AreEqual(1, probe.CallCount);
    }

    /// <summary>
    /// Verifies that a Basic Memory probe failure produces an unhealthy readiness result with the original exception.
    /// </summary>
    [TestMethod]
    public async Task CheckHealthAsyncReturnsUnhealthyWhenProbeFails()
    {
        var expectedException = new InvalidOperationException("Diagnostics failed.");
        var probe = new StubBasicMemoryHealthProbe(_ => Task.FromException(expectedException));
        var healthCheck = new BasicMemoryHealthCheck(probe);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());
        Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
        Assert.AreEqual("Basic Memory MCP diagnostics failed.", result.Description);
        Assert.AreSame(expectedException, result.Exception);
        Assert.AreEqual(1, probe.CallCount);
    }

    /// <summary>
    /// Verifies that request cancellation is propagated instead of being converted into an unhealthy result.
    /// </summary>
    [TestMethod]
    public async Task CheckHealthAsyncPropagatesCancellation()
    {
        var probe = new StubBasicMemoryHealthProbe(cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        var healthCheck = new BasicMemoryHealthCheck(probe);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => healthCheck.CheckHealthAsync(new HealthCheckContext(), cancellationTokenSource.Token));
        Assert.AreEqual(1, probe.CallCount);
    }

    #endregion

    #region Private Methods

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

        #region Properties

        public int CallCount { get; private set; }

        #endregion

        #region Public Methods

        public Task CheckAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return _check(cancellationToken);
        }

        #endregion
    }

    #endregion
}
