namespace ProjectMemoryProxy.Persistence.MemoryProject;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Stores memory project lifecycle metadata in the EF Core database.
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

    #endregion

    #region Private Methods

    #endregion
}
