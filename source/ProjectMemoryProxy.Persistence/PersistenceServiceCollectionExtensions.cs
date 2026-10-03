namespace ProjectMemoryProxy.Persistence;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectMemoryProxy.Core.Configuration;
using ProjectMemoryProxy.Core.Routing;
using ProjectMemoryProxy.Persistence.MemoryProject;

/// <summary>
/// Provides dependency-injection registration for persistence services.
/// </summary>
public static class PersistenceServiceCollectionExtensions
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
    /// Registers the SQLite persistence services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(static serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ProjectMemoryProxyOptions>>().Value;
            return new DatabasePaths(options.DataDirectory);
        });

        services.AddDbContextFactory<ProjectMemoryProxyDbContext>((serviceProvider, optionsBuilder) =>
        {
            var paths = serviceProvider.GetRequiredService<DatabasePaths>();
            ContextOptions.Configure(optionsBuilder, paths.DatabasePath, pooling: true);
        });

        services.AddSingleton<DatabaseMigrator>();
        services.AddSingleton<IProjectRegistry, EfProjectRegistry>();
        services.AddSingleton<RoutingManager>();
        return services;
    }

    /// <summary>
    /// Ensures the lifecycle database is migrated before the server accepts requests.
    /// </summary>
    /// <param name="serviceProvider">The application service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static Task MigrateDatabaseAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        return serviceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(cancellationToken);
    }

    #endregion

    #region Private Methods

    #endregion
}
