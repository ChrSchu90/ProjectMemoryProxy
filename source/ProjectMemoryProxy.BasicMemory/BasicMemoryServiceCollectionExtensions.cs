namespace ProjectMemoryProxy.BasicMemory;

using Microsoft.Extensions.DependencyInjection;

using System;

/// <summary>
/// Provides Basic Memory service registrations.
/// </summary>
public static class BasicMemoryServiceCollectionExtensions
{
    #region Static Fields

    #endregion

    #region Private Fields

    #endregion

    #region Constructors

    #endregion

    #region Events

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <summary>
    /// Registers the Basic Memory MCP upstream integration.
    /// </summary>
    public static IServiceCollection AddBasicMemory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IBasicMemoryClient, BasicMemoryClient>();
        services.AddSingleton<BasicMemoryToolCatalog>();
        return services;
    }

    #endregion

    #region Private Methods

    #endregion
}
