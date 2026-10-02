namespace ProjectMemoryProxy.Persistence.MemoryProject;

using System;
using Microsoft.EntityFrameworkCore;
using ProjectMemoryProxy.Core.MemoryProject;

/// <summary>
/// Stores memory project lifecycle metadata in the DevHatch EF Core database.
/// </summary>
internal sealed class EfProjectRegistry : IProjectRegistry
{
    #region Static Fields

    #endregion

    #region Private Fields

    private readonly IDbContextFactory<ProjectMemoryProxyDbContext> _dbContextFactory;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="EfProjectRegistry"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <exception cref="ArgumentNullException">dbContextFactory</exception>
    public EfProjectRegistry(IDbContextFactory<ProjectMemoryProxyDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    #endregion

    #region Private Methods

    #endregion
}
