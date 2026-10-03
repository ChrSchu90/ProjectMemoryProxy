namespace ProjectMemoryProxy.Persistence.Tests;

using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectMemoryProxy.Core.Routing;
using ProjectMemoryProxy.Persistence.Entities;
using ProjectMemoryProxy.Persistence.MemoryProject;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Tests for <see cref="EfProjectRegistry"/>
/// </summary>
[TestClass]
public sealed class EfProjectRegistryTests
{
    #region Private Fields

    private string _databasePath = null!;
    private string _testRoot = null!;

    #endregion

    #region Tests Constructors

    #endregion

    #region Test Preparation

    [TestInitialize]
    public void Initialize()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "ProjectMemoryProxy.Tests", Path.GetRandomFileName());
        _databasePath = Path.Combine(_testRoot, $"{Path.GetRandomFileName()}.db");
        Directory.CreateDirectory(_testRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    #endregion

    #region Tests

    /// <summary>
    /// Verifies that an exact context binding returns its project identifier and persisted statuses.
    /// </summary>
    [TestMethod]
    public async Task FindRouteAsyncReturnsExactRoute()
    {
        var memoryProjectId = Guid.NewGuid();
        await CreateDatabaseAsync(_databasePath,
            new ProjectRoutingEntity
            {
                MemoryProjectId = memoryProjectId,
                Status = Status.Active,
                Bindings =
                {
                    new ContextBindingEntity
                    {
                        BindingType = BindingType.GitRepository,
                        BindingName = "github.com/ChrSchu90/ProjectMemoryProxy",
                        Status = Status.Active
                    }
                }
            });

        var registry = CreateRegistry(_databasePath);
        Assert.IsTrue(ContextId.TryParse("git:github.com/ChrSchu90/ProjectMemoryProxy", out var contextId));

        var route = await registry.FindRouteAsync(contextId!, CancellationToken.None);
        Assert.IsNotNull(route);
        Assert.AreEqual(memoryProjectId, route.MemoryProjectId);
        Assert.AreEqual(Status.Active, route.ProjectRoutingStatus);
        Assert.AreEqual(Status.Active, route.BindingStatus);
    }

    /// <summary>
    /// Verifies that an unknown context returns no route.
    /// </summary>
    [TestMethod]
    public async Task FindRouteAsyncReturnsNullForUnknownContext()
    {
        await CreateDatabaseAsync(_databasePath,
            new ProjectRoutingEntity
            {
                MemoryProjectId = Guid.NewGuid(),
                Bindings =
                {
                    new ContextBindingEntity
                    {
                        BindingType = BindingType.GitRepository,
                        BindingName = "github.com/ChrSchu90/ProjectMemoryProxy"
                    }
                }
            });

        var registry = CreateRegistry(_databasePath);
        Assert.IsTrue(ContextId.TryParse("git:github.com/ChrSchu90/OtherRepository", out var contextId));

        var route = await registry.FindRouteAsync(contextId!, CancellationToken.None);
        Assert.IsNull(route);
    }

    /// <summary>
    /// Verifies that inactive binding state is returned to the core layer instead of being filtered by persistence.
    /// </summary>
    [TestMethod]
    public async Task FindRouteAsyncReturnsInactiveBindingStatus()
    {
        await CreateDatabaseAsync(_databasePath,
            new ProjectRoutingEntity
            {
                MemoryProjectId = Guid.NewGuid(),
                Status = Status.Active,
                Bindings =
                {
                    new ContextBindingEntity
                    {
                        BindingType = BindingType.ChatGptProject,
                        BindingName = "chatty-mcp-and-aiharborvm",
                        Status = Status.Inactive
                    }
                }
            });

        var registry = CreateRegistry(_databasePath);
        Assert.IsTrue(ContextId.TryParse("chatgpt-project:chatty-mcp-and-aiharborvm", out var contextId));

        var route = await registry.FindRouteAsync(contextId!, CancellationToken.None);
        Assert.IsNotNull(route);
        Assert.AreEqual(Status.Active, route.ProjectRoutingStatus);
        Assert.AreEqual(Status.Inactive, route.BindingStatus);
    }

    /// <summary>
    /// Verifies that inactive project routing state is returned to the core layer instead of being filtered by persistence.
    /// </summary>
    [TestMethod]
    public async Task FindRouteAsyncReturnsInactiveProjectRoutingStatus()
    {
        await CreateDatabaseAsync(_databasePath,
            new ProjectRoutingEntity
            {
                MemoryProjectId = Guid.NewGuid(),
                Status = Status.Inactive,
                Bindings =
                {
                    new ContextBindingEntity
                    {
                        BindingType = BindingType.GitRepository,
                        BindingName = "github.com/ChrSchu90/ProjectMemoryProxy",
                        Status = Status.Active
                    }
                }
            });

        var registry = CreateRegistry(_databasePath);
        Assert.IsTrue(ContextId.TryParse("git:github.com/ChrSchu90/ProjectMemoryProxy", out var contextId));

        var route = await registry.FindRouteAsync(contextId!, CancellationToken.None);
        Assert.IsNotNull(route);
        Assert.AreEqual(Status.Inactive, route.ProjectRoutingStatus);
        Assert.AreEqual(Status.Active, route.BindingStatus);
    }

    /// <summary>
    /// Verifies that the database prevents the same context from being bound to more than one project routing.
    /// </summary>
    [TestMethod]
    public async Task DatabaseRejectsDuplicateContextBinding()
    {
        var options = ContextOptions.Create(_databasePath, pooling: false);

        await using var dbContext = new ProjectMemoryProxyDbContext(options);
        await dbContext.Database.MigrateAsync();

        dbContext.RoutingProjects.AddRange(
            new ProjectRoutingEntity
            {
                MemoryProjectId = Guid.NewGuid(),
                Bindings =
                {
                    new ContextBindingEntity
                    {
                        BindingType = BindingType.GitRepository,
                        BindingName = "github.com/ChrSchu90/ProjectMemoryProxy"
                    }
                }
            },
            new ProjectRoutingEntity
            {
                MemoryProjectId = Guid.NewGuid(),
                Bindings =
                {
                    new ContextBindingEntity
                    {
                        BindingType = BindingType.GitRepository,
                        BindingName = "github.com/ChrSchu90/ProjectMemoryProxy"
                    }
                }
            });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    #endregion

    #region Private Methods

    private static EfProjectRegistry CreateRegistry(string databasePath)
    {
        var options = ContextOptions.Create(databasePath, pooling: false);
        return new EfProjectRegistry(new TestDbContextFactory(options));
    }

    private static async Task CreateDatabaseAsync(string databasePath, ProjectRoutingEntity projectRouting)
    {
        var options = ContextOptions.Create(databasePath, pooling: false);

        await using var dbContext = new ProjectMemoryProxyDbContext(options);
        await dbContext.Database.MigrateAsync();

        dbContext.RoutingProjects.Add(projectRouting);
        await dbContext.SaveChangesAsync();
    }

    private static void DeleteDatabase(string databasePath)
    {
        foreach (var suffix in new[] { "", "-journal", "-shm", "-wal" })
        {
            var path = databasePath + suffix;

            if (File.Exists(path))
                File.Delete(path);
        }
    }

    #endregion

    #region Test Classes

    private sealed class TestDbContextFactory : IDbContextFactory<ProjectMemoryProxyDbContext>
    {
        #region Private Fields

        private readonly DbContextOptions<ProjectMemoryProxyDbContext> _options;

        #endregion

        #region Constructors

        public TestDbContextFactory(DbContextOptions<ProjectMemoryProxyDbContext> options)
        {
            _options = options;
        }

        #endregion

        #region Public Methods

        public ProjectMemoryProxyDbContext CreateDbContext()
        {
            return new ProjectMemoryProxyDbContext(_options);
        }

        public Task<ProjectMemoryProxyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ProjectMemoryProxyDbContext(_options));
        }

        #endregion
    }

    #endregion
}
