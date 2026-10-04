namespace ProjectMemoryProxy.Persistence.Routing;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectMemoryProxy.Core.Routing;
using ProjectMemoryProxy.Persistence.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

    /// <inheritdoc />
    public async Task<ContextBinding?> FindBindingAsync(ContextId contextId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contextId);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var binding = await dbContext.RoutingProjects
                          .AsNoTracking()
                          .Where(project => project.Bindings.Any(item => item.BindingType == contextId.BindingType && item.BindingName == contextId.BindingName))
                          .Select(project => new
                          {
                              project.MemoryProjectId,
                              BindingStatus = project.Bindings
                                .Where(item => item.BindingType == contextId.BindingType && item.BindingName == contextId.BindingName)
                                .Select(item => item.Status)
                                .Single()
                          })
                          .SingleOrDefaultAsync(cancellationToken)
                          .ConfigureAwait(false);

        return binding != null ? new ContextBinding(contextId, binding.MemoryProjectId, binding.BindingStatus) : null;
    }

    /// <inheritdoc />
    public async Task<ContextBinding> CreateBindingAsync(ContextId contextId, Guid memoryProjectId, Status status, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contextId);
        if (memoryProjectId == Guid.Empty)
            throw new ArgumentException("The Basic Memory project identifier must not be empty.", nameof(memoryProjectId));

        if (status is not Status.Active and not Status.Inactive)
            throw new ArgumentOutOfRangeException(nameof(status), status, "The context binding status is unsupported.");


        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var project = await dbContext.RoutingProjects
                          .SingleOrDefaultAsync(item => item.MemoryProjectId == memoryProjectId, cancellationToken)
                          .ConfigureAwait(false);

        if (project == null)
            throw new InvalidOperationException($"No registered project routing exists for Basic Memory project '{memoryProjectId:D}'.");

        var entity = new ContextBindingEntity
        {
            BindingType = contextId.BindingType,
            BindingName = contextId.BindingName,
            Status = status
        };

        project.Bindings.Add(entity);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new ProjectRegistryConflictException($"A binding already exists for context '{contextId}'.", ex);
        }

        return new ContextBinding(contextId, memoryProjectId, entity.Status);
    }

    /// <inheritdoc />
    public async Task<bool> TryUpdateProjectStatusAsync(Guid memoryProjectId, Status expectedStatus, Status newStatus, CancellationToken cancellationToken = default)
    {
        if (memoryProjectId == Guid.Empty)
            throw new ArgumentException("The Basic Memory project identifier must not be empty.", nameof(memoryProjectId));

        if (expectedStatus is not Status.Active and not Status.Inactive)
            throw new ArgumentOutOfRangeException(nameof(expectedStatus), expectedStatus, "The expected project routing status is unsupported.");

        if (newStatus is not Status.Active and not Status.Inactive)
            throw new ArgumentOutOfRangeException(nameof(newStatus), newStatus, "The new project routing status is unsupported.");

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var updatedAt = DateTimeOffset.UtcNow;
        var affectedRows = await dbContext.RoutingProjects
                               .Where(project => project.MemoryProjectId == memoryProjectId && project.Status == expectedStatus)
                               .ExecuteUpdateAsync(setters => setters
                                       .SetProperty(project => project.Status, newStatus)
                                       .SetProperty(project => project.UpdatedAt, updatedAt),
                                   cancellationToken)
                               .ConfigureAwait(false);

        switch (affectedRows)
        {
            case 0:
                return false;
            case 1:
                return true;
            default:
                throw new InvalidOperationException($"Updating project routing '{memoryProjectId:D}' affected more than one row.");
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryUpdateBindingStatusAsync(ContextId contextId, Status expectedStatus, Status newStatus, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contextId);
        if (expectedStatus is not Status.Active and not Status.Inactive)
            throw new ArgumentOutOfRangeException(nameof(expectedStatus), expectedStatus, "The expected context binding status is unsupported.");

        if (newStatus is not Status.Active and not Status.Inactive)
            throw new ArgumentOutOfRangeException(nameof(newStatus), newStatus, "The new context binding status is unsupported.");

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var bindingType = contextId.BindingType.ToString();
        var expectedStatusValue = expectedStatus.ToString();
        var newStatusValue = newStatus.ToString();
        var updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // ContextBindingEntity is an owned collection mapped to its own table.
        // Use a direct conditional update here instead of projecting the owned collection through SelectMany, which is fragile for this mapping.
        var affectedRows = await dbContext.Database
            .ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE "ContextBindings"
                SET "Status" = {newStatusValue},
                    "UpdatedAt" = {updatedAt}
                WHERE "BindingType" = {bindingType}
                  AND "BindingName" = {contextId.BindingName}
                  AND "Status" = {expectedStatusValue}
                """,
                cancellationToken)
            .ConfigureAwait(false);

        switch (affectedRows)
        {
            case 0:
                return false;
            case 1:
                return true;
            default:
                throw new InvalidOperationException($"Updating context binding '{contextId}' affected more than one row.");
        }
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
