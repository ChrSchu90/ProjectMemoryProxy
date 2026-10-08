namespace ProjectMemoryProxy.Server.Hosting;

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Maps the production liveness and readiness endpoint contracts.
/// </summary>
internal static class HealthEndpointMapping
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
    /// Adds the health endpoints used both by Docker and by HTTP integration tests.
    /// </summary>
    public static IEndpointRouteBuilder MapProjectMemoryProxyHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
        return endpoints;
    }

    #endregion

    #region Private Methods

    #endregion
}
