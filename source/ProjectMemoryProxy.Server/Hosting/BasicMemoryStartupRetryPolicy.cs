namespace ProjectMemoryProxy.Server.Hosting;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Retries transient Basic Memory startup connectivity failures until initialization succeeds or startup is canceled.
/// </summary>
internal static class BasicMemoryStartupRetryPolicy
{
    #region Static Fields

    #endregion

    #region Private Fields

    #endregion

    #region Constructors

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <summary>
    /// Executes Basic Memory startup initialization with retry behavior for transient connectivity failures.
    /// </summary>
    /// <param name="initializeAsync">The Basic Memory startup initialization operation.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="retryDelay">The delay between transient retries.</param>
    /// <param name="cancellationToken">The startup cancellation token.</param>
    public static async Task ExecuteAsync(Func<CancellationToken, Task> initializeAsync, ILogger logger, TimeSpan retryDelay, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(initializeAsync);
        ArgumentNullException.ThrowIfNull(logger);

        if (retryDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retryDelay), retryDelay, "The retry delay must not be negative.");

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await initializeAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (HttpRequestException exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.LogWarning("Basic Memory MCP is not reachable yet: {ErrorMessage} Retrying in {RetryDelay}.", exception.Message, retryDelay);
            }
            catch (TimeoutException exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.LogWarning("Timed out while connecting to Basic Memory MCP: {ErrorMessage} Retrying in {RetryDelay}.", exception.Message, retryDelay);
            }

            await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    #endregion

    #region Private Methods

    #endregion
}
