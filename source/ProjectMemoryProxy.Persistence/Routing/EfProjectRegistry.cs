namespace ProjectMemoryProxy.Persistence.Routing;

using Microsoft.EntityFrameworkCore;
using ProjectMemoryProxy.Core.Routing;
using ProjectMemoryProxy.Persistence.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

/// <summary>
/// Stores memory project lifecycle metadata in the EF Core database.
/// </summary>
internal sealed class EfProjectRegistry : IProjectRegistry
{
    #region Static Fields

    private const int SqliteUniqueConstraintErrorCode = 2067;
    private const int SqliteConstraintErrorCode = 19;

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

    /// <inheritdoc />
    public async Task<ProjectRoute?> FindRouteAsync(ContextId contextId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contextId);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var route = await dbContext.RoutingProjects
                        .AsNoTracking()
                        .Where(p => p.Bindings.Any(binding => binding.BindingType == contextId.BindingType && binding.BindingName == contextId.BindingName))
                        .Select(p => new ProjectRoute(p.MemoryProjectId, p.MemoryProjectName, p.Status, p.Bindings
                            .Where(b => b.BindingType == contextId.BindingType && b.BindingName == contextId.BindingName)
                            .Select(b => b.Status)
                            .Single()))
                        .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return route;
    }

    /// <inheritdoc />
    public async Task<ProjectRouting?> FindByMemoryProjectIdAsync(Guid memoryProjectId, CancellationToken cancellationToken = default)
    {
        if (memoryProjectId == Guid.Empty)
            throw new ArgumentException("The Basic Memory project identifier must not be empty.", nameof(memoryProjectId));

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await dbContext.RoutingProjects
            .AsNoTracking()
            .Where(project => project.MemoryProjectId == memoryProjectId)
            .Select(project => new ProjectRouting(project.MemoryProjectId, project.MemoryProjectName, project.Status))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ProjectRouting?> FindByMemoryProjectNameAsync(string memoryProjectName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryProjectName);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await dbContext.RoutingProjects
            .AsNoTracking()
            .Where(project => project.MemoryProjectName == memoryProjectName)
            .Select(project => new ProjectRouting(project.MemoryProjectId, project.MemoryProjectName, project.Status))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ProjectRouting> CreateAsync(Guid memoryProjectId, string memoryProjectName, Status status, CancellationToken cancellationToken = default)
    {
        if (memoryProjectId == Guid.Empty)
            throw new ArgumentException("The Basic Memory project identifier must not be empty.", nameof(memoryProjectId));

        ArgumentException.ThrowIfNullOrWhiteSpace(memoryProjectName);

        if (status is not Status.Active and not Status.Inactive)
            throw new ArgumentOutOfRangeException(nameof(status), status, "The project routing status is unsupported.");

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = new ProjectRoutingEntity
        {
            MemoryProjectId = memoryProjectId,
            MemoryProjectName = memoryProjectName,
            Status = status
        };

        dbContext.RoutingProjects.Add(entity);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new ProjectRegistryConflictException("A project routing with the same Basic Memory project identifier or name already exists.", ex);
        }

        return new ProjectRouting(entity.MemoryProjectId, entity.MemoryProjectName, entity.Status);
    }

    #endregion

    #region Private Methods

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqliteException
        {
            SqliteErrorCode: SqliteConstraintErrorCode,
            SqliteExtendedErrorCode: SqliteUniqueConstraintErrorCode
        };
    }

    #endregion
}
