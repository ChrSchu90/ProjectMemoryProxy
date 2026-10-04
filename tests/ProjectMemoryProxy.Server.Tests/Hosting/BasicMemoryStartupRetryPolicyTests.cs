namespace ProjectMemoryProxy.Server.Tests.Hosting;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectMemoryProxy.Server.Hosting;

/// <summary>
/// Tests for <see cref="BasicMemoryStartupRetryPolicy"/>
/// </summary>
[TestClass]
public sealed class BasicMemoryStartupRetryPolicyTests
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
    /// Verifies that a transient HTTP connectivity failure is retried and logged without attaching an exception stack trace.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsyncRetriesHttpRequestException()
    {
        var attemptCount = 0;
        var logger = new CapturingLogger();

        await BasicMemoryStartupRetryPolicy.ExecuteAsync(_ =>
            {
                attemptCount++;

                if (attemptCount == 1)
                    throw new HttpRequestException("connection refused");

                return Task.CompletedTask;
            },
            logger,
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.AreEqual(2, attemptCount);
        Assert.HasCount(1, logger.Entries);
        Assert.AreEqual(LogLevel.Warning, logger.Entries[0].LogLevel);
        Assert.IsNull(logger.Entries[0].Exception);
    }

    /// <summary>
    /// Verifies that a transient connection timeout is retried until initialization succeeds.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsyncRetriesTimeoutException()
    {
        var attemptCount = 0;

        await BasicMemoryStartupRetryPolicy.ExecuteAsync(_ =>
            {
                attemptCount++;

                if (attemptCount == 1)
                    throw new TimeoutException("timeout");

                return Task.CompletedTask;
            },
            new CapturingLogger(),
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.AreEqual(2, attemptCount);
    }

    /// <summary>
    /// Verifies that non-transient startup failures are propagated immediately instead of being retried.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsyncPropagatesNonTransientFailure()
    {
        var attemptCount = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            BasicMemoryStartupRetryPolicy.ExecuteAsync(_ =>
                {
                    attemptCount++;
                    throw new InvalidOperationException(
                        "invalid schema");
                },
                new CapturingLogger(),
                TimeSpan.Zero,
                CancellationToken.None));

        Assert.AreEqual(1, attemptCount);
    }

    /// <summary>
    /// Verifies that startup cancellation interrupts the retry delay and prevents another initialization attempt.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsyncPropagatesCancellationDuringRetryDelay()
    {
        var attemptCount = 0;
        var firstAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();
        var task = BasicMemoryStartupRetryPolicy.ExecuteAsync(_ =>
                {
                    attemptCount++;
                    firstAttempt.TrySetResult();

                    throw new HttpRequestException(
                        "connection refused");
                },
                new CapturingLogger(),
                TimeSpan.FromMinutes(1),
                cancellationTokenSource.Token);

        await firstAttempt.Task;
        await cancellationTokenSource.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
        Assert.AreEqual(1, attemptCount);
    }

    #endregion

    #region Private Methods

    #endregion

    #region Test Classes

    private sealed class CapturingLogger : ILogger
    {
        #region Properties

        public List<LogEntry> Entries { get; } = [];

        #endregion

        #region Public Methods

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, exception, formatter(state, exception)));
        }

        #endregion
    }

    private sealed record LogEntry(LogLevel LogLevel, Exception? Exception, string Message);

    #endregion
}
