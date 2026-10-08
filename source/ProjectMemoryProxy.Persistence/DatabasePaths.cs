namespace ProjectMemoryProxy.Persistence;

using System;
using System.Diagnostics;
using System.IO;

/// <summary>
/// Contains the managed database files.
/// </summary>
[DebuggerDisplay("Database: {DatabasePath}")]
internal sealed class DatabasePaths
{
    #region Static Fields

    #endregion

    #region Private Fields

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes paths below a workspace storage root.
    /// </summary>
    /// <param name="databaseDir">The absolute ProjectMemoryProxy workspace storage root.</param>
    /// <exception cref="ArgumentException">Thrown when the path is empty, relative, or does not exist.</exception>
    public DatabasePaths(string databaseDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseDir);

        if (!Path.IsPathFullyQualified(databaseDir))
            throw new ArgumentException("The database directory must be an absolute path.", nameof(databaseDir));

        var normalizedRootPath = Path.GetFullPath(databaseDir);
        if (!Directory.Exists(normalizedRootPath))
            throw new ArgumentException("The database directory must exist.", nameof(databaseDir));

        DatabasePath = Path.Combine(normalizedRootPath, "projectmemoryproxy.db");
        MigrationPath = DatabasePath + ".migrate";
        BackupPath = DatabasePath + ".pre-migration";
    }


    #endregion

    #region Properties

    /// <summary>
    /// Gets the active lifecycle database path.
    /// </summary>
    public string DatabasePath { get; }

    /// <summary>
    /// Gets the temporary migration database path.
    /// </summary>
    public string MigrationPath { get; }

    /// <summary>
    /// Gets the single previous-version backup database path.
    /// </summary>
    public string BackupPath { get; }

    #endregion

    #region Public Methods

    #endregion

    #region Private Methods

    #endregion
}
