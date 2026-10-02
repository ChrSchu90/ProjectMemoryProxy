namespace ProjectMemoryProxy.Persistence;

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Applies EF Core migrations to an isolated database copy before activating it.
/// </summary>
internal sealed class DatabaseMigrator
{
    #region Static Fields

    private static readonly string[] MigrationSidecarSuffixes = ["", "-journal", "-shm", "-wal"];

    #endregion

    #region Private Fields

    private readonly ILogger<DatabaseMigrator> _logger;
    private readonly DatabasePaths _paths;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a database migrator.
    /// </summary>
    /// <param name="paths">The managed database paths.</param>
    /// <param name="logger">The logger.</param>
    public DatabaseMigrator(DatabasePaths paths, ILogger<DatabaseMigrator> logger)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods


    /// <summary>
    /// Ensures the active database exists and is migrated to the current schema.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        DeleteMigrationArtifacts();

        if (!File.Exists(_paths.DatabasePath))
        {
            _logger.LogInformation("Creating lifecycle database.");
            await CreateInitialDatabaseAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!await HasPendingMigrationsAsync(_paths.DatabasePath, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogDebug("Lifecycle database schema is current.");
            return;
        }

        _logger.LogInformation("Applying lifecycle database migrations on an isolated copy.");

        try
        {
            CreateMigrationCopy();
            await ApplyAndValidateMigrationsAsync(_paths.MigrationPath, cancellationToken).ConfigureAwait(false);
            ActivateMigratedDatabase();
            _logger.LogInformation("Lifecycle database migration completed. Previous database retained as {BackupPath}.", _paths.BackupPath);
        }
        catch
        {
            TryDeleteMigrationArtifacts();
            throw;
        }
    }

    #endregion

    #region Private Methods

    private async Task CreateInitialDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ApplyAndValidateMigrationsAsync(_paths.MigrationPath, cancellationToken).ConfigureAwait(false);
            SqliteConnection.ClearAllPools();
            File.Move(_paths.MigrationPath, _paths.DatabasePath);
        }
        catch
        {
            TryDeleteMigrationArtifacts();
            throw;
        }
    }

    private void CreateMigrationCopy()
    {
        var sourceConnectionString = new SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        var destinationConnectionString = new SqliteConnectionStringBuilder { DataSource = _paths.MigrationPath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString();
        using var sourceConnection = new SqliteConnection(sourceConnectionString);
        using var destinationConnection = new SqliteConnection(destinationConnectionString);
        sourceConnection.Open();
        destinationConnection.Open();
        sourceConnection.BackupDatabase(destinationConnection);
    }

    private static ProjectMemoryProxyDbContext CreateContext(string databasePath)
    {
        var options = ContextOptions.Create(databasePath, pooling: false);
        return new ProjectMemoryProxyDbContext(options);
    }

    private static async Task<bool> HasPendingMigrationsAsync(string databasePath, CancellationToken cancellationToken)
    {
        await using var context = CreateContext(databasePath);
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
        return pendingMigrations.Any();
    }

    private static async Task ApplyAndValidateMigrationsAsync(string databasePath, CancellationToken cancellationToken)
    {
        await using (var context = CreateContext(databasePath))
        {
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
            if (pendingMigrations.Any())
                throw new InvalidOperationException("The migrated database still has pending EF Core migrations.");
        }

        var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(Convert.ToString(result, CultureInfo.InvariantCulture), "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"SQLite quick_check failed for the migrated database: {result}");
    }

    private void ActivateMigratedDatabase()
    {
        SqliteConnection.ClearAllPools();
        File.Replace(_paths.MigrationPath, _paths.DatabasePath, _paths.BackupPath);
        DeleteMigrationSidecars();
    }

    private void DeleteMigrationArtifacts()
    {
        foreach (var suffix in MigrationSidecarSuffixes)
        {
            var path = _paths.MigrationPath + suffix;
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private void DeleteMigrationSidecars()
    {
        foreach (var suffix in MigrationSidecarSuffixes.Where(static suffix => suffix.Length > 0))
        {
            var path = _paths.MigrationPath + suffix;
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private void TryDeleteMigrationArtifacts()
    {
        try
        {
            SqliteConnection.ClearAllPools();
            DeleteMigrationArtifacts();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to remove temporary migration database artifacts at {MigrationPath}.", _paths.MigrationPath);
        }
    }

    #endregion
}
