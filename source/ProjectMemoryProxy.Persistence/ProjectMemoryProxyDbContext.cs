namespace ProjectMemoryProxy.Persistence;

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ProjectMemoryProxy.Persistence.Converters;
using ProjectMemoryProxy.Persistence.Entities;

/// <summary>
/// Provides EF Core access to the ProjectMemoryProxy lifecycle database.
/// </summary>
public sealed class ProjectMemoryProxyDbContext : DbContext
{
    #region Static Fields

    #endregion

    #region Private Fields

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new ProjectMemoryProxy database context.
    /// </summary>
    /// <param name="options">The configured EF Core options.</param>
    public ProjectMemoryProxyDbContext(DbContextOptions<ProjectMemoryProxyDbContext> options)
        : base(options)
    {
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the persisted projects that are available for routing.
    /// </summary>
    internal DbSet<ProjectRoutingEntity> RoutingProjects => Set<ProjectRoutingEntity>();

    #endregion

    #region Public Methods

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Configure DateTimeOffset properties to use Unix milliseconds conversion.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToUnixMillisecondsConverter>();

        // Store enums as strings in the database for better readability and maintainability.
        configurationBuilder.Properties<BindingType>().HaveConversion<EnumToStringConverter<BindingType>>();
        configurationBuilder.Properties<Status>().HaveConversion<EnumToStringConverter<Status>>();
    }

    #endregion

    #region Private Methods

    #endregion

    #region ContextFactory

    /// <summary>
    /// Creates a database context for EF Core design-time tooling.
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedType.Local")]
    private sealed class ProjectMemoryProxyDbContextFactory : IDesignTimeDbContextFactory<ProjectMemoryProxyDbContext>
    {
        #region Public Methods

        /// <inheritdoc />
        public ProjectMemoryProxyDbContext CreateDbContext(string[] args)
        {
            var designTimeRoot = Path.Combine(Path.GetTempPath(), "ProjectMemoryProxy.EFDesign", Path.GetRandomFileName());
            Directory.CreateDirectory(designTimeRoot);

            var databasePath = Path.Combine(designTimeRoot, "projectmemoryproxy.db");
            var options = ContextOptions.Create(databasePath, pooling: false);
            return new ProjectMemoryProxyDbContext(options);
        }

        #endregion
    }

    #endregion
}
