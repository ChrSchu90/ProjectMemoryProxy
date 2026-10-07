namespace ProjectMemoryProxy.Server.Hosting;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProjectMemoryProxy.BasicMemory.Health;


/// <summary>
/// Reports whether ProjectMemoryProxy can successfully communicate with Basic Memory.
/// </summary>
internal sealed class BasicMemoryHealthCheck : IHealthCheck
{
    #region Static Fields

    #endregion

    #region Private Fields

    private readonly IBasicMemoryHealthProbe _healthProbe;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicMemoryHealthCheck"/> class.
    /// </summary>
    public BasicMemoryHealthCheck(IBasicMemoryHealthProbe healthProbe)
    {
        _healthProbe = healthProbe ?? throw new ArgumentNullException(nameof(healthProbe));
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _healthProbe.CheckAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Basic Memory MCP diagnostics failed.", ex);
        }
    }

    #endregion

    #region Private Methods

    #endregion
}
