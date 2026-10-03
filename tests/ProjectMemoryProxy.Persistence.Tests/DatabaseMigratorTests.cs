namespace ProjectMemoryProxy.Persistence.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Tests for <see cref="DatabaseMigrator"/>
/// </summary>
[TestClass]
public sealed class DatabaseMigratorTests
{
    #region Private Fields

    private string _testRoot = null!;

    #endregion

    #region Tests Constructors

    #endregion

    #region Test Preparation

    [TestInitialize]
    public void Initialize()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "ProjectMemoryProxy.Tests", Path.GetRandomFileName());
        Directory.CreateDirectory(_testRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }


    #endregion

    #region Tests

    [TestMethod]
    public void NoPendingChanges()
    {
        var databasePath = Path.Combine(_testRoot, "model-check.db");
        using var context = CreateContext(databasePath);
        Assert.IsFalse(context.Database.HasPendingModelChanges());
    }

    [TestMethod]
    public async Task CreatesCurrentDatabaseWithoutBackup()
    {
        var paths = new DatabasePaths(_testRoot);
        var migrator = CreateMigrator(paths);
        await migrator.MigrateAsync(CancellationToken.None);
        Assert.IsTrue(File.Exists(paths.DatabasePath));
        Assert.IsFalse(File.Exists(paths.MigrationPath));
        Assert.IsFalse(File.Exists(paths.BackupPath));
        Assert.IsFalse(await HasPendingMigrationsAsync(paths.DatabasePath));
    }

    [TestMethod]
    public async Task DoesNotCreateBackupWithoutMigrating()
    {
        var paths = new DatabasePaths(_testRoot);
        var migrator = CreateMigrator(paths);
        await migrator.MigrateAsync(CancellationToken.None);
        await migrator.MigrateAsync(CancellationToken.None);
        Assert.IsTrue(File.Exists(paths.DatabasePath));
        Assert.IsFalse(File.Exists(paths.MigrationPath));
        Assert.IsFalse(File.Exists(paths.BackupPath));
    }

    [TestMethod]
    public async Task MigrationCopyAndBacksUpOriginal()
    {
        var paths = new DatabasePaths(_testRoot);
        CreateLegacyDatabase(paths.DatabasePath);
        var migrator = CreateMigrator(paths);
        await migrator.MigrateAsync(CancellationToken.None);
        Assert.IsTrue(File.Exists(paths.DatabasePath));
        Assert.IsTrue(File.Exists(paths.BackupPath));
        Assert.IsFalse(File.Exists(paths.MigrationPath));
        Assert.IsTrue(TableExists(paths.DatabasePath, "LegacyMarker"));
        Assert.IsTrue(TableExists(paths.DatabasePath, "ProjectRoutings"));
        Assert.IsTrue(TableExists(paths.BackupPath, "LegacyMarker"));
        Assert.IsFalse(TableExists(paths.BackupPath, "ProjectRoutings"));
    }

    [TestMethod]
    public async Task StaleMigrationRecreatesDatabase()
    {
        var paths = new DatabasePaths(_testRoot);
        await File.WriteAllTextAsync(paths.MigrationPath, "stale");
        var migrator = CreateMigrator(paths);
        await migrator.MigrateAsync(CancellationToken.None);
        Assert.IsTrue(File.Exists(paths.DatabasePath));
        Assert.IsFalse(File.Exists(paths.MigrationPath));
        Assert.IsFalse(await HasPendingMigrationsAsync(paths.DatabasePath));
    }

    #endregion

    #region Private Methods

    private static DatabaseMigrator CreateMigrator(DatabasePaths paths)
    {
        return new DatabaseMigrator(paths, NullLogger<DatabaseMigrator>.Instance);
    }

    private static ProjectMemoryProxyDbContext CreateContext(string databasePath)
    {
        return new ProjectMemoryProxyDbContext(ContextOptions.Create(databasePath, pooling: false));
    }

    private static async Task<bool> HasPendingMigrationsAsync(string databasePath)
    {
        await using var context = CreateContext(databasePath);
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        return pendingMigrations.Any();
    }

    private static void CreateLegacyDatabase(string databasePath)
    {
        using var connection = OpenConnection(databasePath, SqliteOpenMode.ReadWriteCreate);
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE LegacyMarker (Id INTEGER NOT NULL PRIMARY KEY); INSERT INTO LegacyMarker (Id) VALUES (1);";
        command.ExecuteNonQuery();
    }

    private static bool TableExists(string databasePath, string tableName)
    {
        using var connection = OpenConnection(databasePath, SqliteOpenMode.ReadOnly);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    private static SqliteConnection OpenConnection(string databasePath, SqliteOpenMode mode)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = mode, Pooling = false }.ToString();
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    #endregion

    #region Test Classes

    #endregion
}
