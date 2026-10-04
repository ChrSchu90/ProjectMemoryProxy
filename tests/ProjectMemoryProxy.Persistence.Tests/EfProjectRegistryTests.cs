namespace ProjectMemoryProxy.Persistence.Tests;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectMemoryProxy.Core.Routing;
using ProjectMemoryProxy.Persistence.Entities;
using ProjectMemoryProxy.Persistence.Routing;

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
        var memoryProjectName = Guid.NewGuid().ToString("N");
        await CreateDatabaseAsync(_databasePath,
            new ProjectRoutingEntity
            {
                MemoryProjectId = memoryProjectId,
                MemoryProjectName = memoryProjectName,
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
        Assert.AreEqual(memoryProjectName, route.MemoryProjectName);
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
                MemoryProjectName = Guid.NewGuid().ToString("N"),
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
                MemoryProjectName = Guid.NewGuid().ToString("N"),
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
                MemoryProjectName = Guid.NewGuid().ToString("N"),
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
                MemoryProjectName = "project-a",
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
                MemoryProjectName = "project-b",
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

    /// <summary>
    /// Verifies that the database prevents two local project routings from using the same Basic Memory project name.
    /// </summary>
    [TestMethod]
    public async Task DatabaseRejectsDuplicateMemoryProjectName()
    {
        var options = ContextOptions.Create(_databasePath, pooling: false);

        await using var dbContext = new ProjectMemoryProxyDbContext(options);
        await dbContext.Database.MigrateAsync();

        dbContext.RoutingProjects.AddRange(
            new ProjectRoutingEntity
            {
                MemoryProjectId = Guid.NewGuid(),
                MemoryProjectName = "shared-project-name"
            },
            new ProjectRoutingEntity
            {
                MemoryProjectId = Guid.NewGuid(),
                MemoryProjectName = "shared-project-name"
            });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    /// <summary>
    /// Verifies that a registered project routing can be found by its Basic Memory external identifier.
    /// </summary>
    [TestMethod]
    public async Task FindByMemoryProjectIdAsyncReturnsRouting()
    {
        var memoryProjectId = Guid.NewGuid();
        var memoryProjectName = "project-a";
        await CreateDatabaseAsync(_databasePath, new ProjectRoutingEntity
        {
            MemoryProjectId = memoryProjectId,
            MemoryProjectName = memoryProjectName,
            Status = Status.Inactive
        });

        var registry = CreateRegistry(_databasePath);
        var routing = await registry.FindByMemoryProjectIdAsync(memoryProjectId, CancellationToken.None);
        Assert.IsNotNull(routing);
        Assert.AreEqual(memoryProjectId, routing.MemoryProjectId);
        Assert.AreEqual(memoryProjectName, routing.MemoryProjectName);
        Assert.AreEqual(Status.Inactive, routing.Status);
    }

    /// <summary>
    /// Verifies that a registered project routing can be found by its exact Basic Memory project name.
    /// </summary>
    [TestMethod]
    public async Task FindByMemoryProjectNameAsyncReturnsRouting()
    {
        var memoryProjectId = Guid.NewGuid();
        var memoryProjectName = "project-a";
        await CreateDatabaseAsync(_databasePath, new ProjectRoutingEntity
        {
            MemoryProjectId = memoryProjectId,
            MemoryProjectName = memoryProjectName,
            Status = Status.Active
        });

        var registry = CreateRegistry(_databasePath);
        var routing = await registry.FindByMemoryProjectNameAsync(memoryProjectName, CancellationToken.None);
        Assert.IsNotNull(routing);
        Assert.AreEqual(memoryProjectId, routing.MemoryProjectId);
        Assert.AreEqual(memoryProjectName, routing.MemoryProjectName);
        Assert.AreEqual(Status.Active, routing.Status);
    }

    /// <summary>
    /// Verifies that project-routing lookups return no result when the requested Basic Memory identity is not registered.
    /// </summary>
    [TestMethod]
    public async Task ProjectRoutingLookupsReturnNullWhenRoutingDoesNotExist()
    {
        await CreateDatabaseAsync(_databasePath, new ProjectRoutingEntity
        {
            MemoryProjectId = Guid.NewGuid(),
            MemoryProjectName = "project-a"
        });

        var registry = CreateRegistry(_databasePath);
        var byId = await registry.FindByMemoryProjectIdAsync(Guid.NewGuid(), CancellationToken.None);
        var byName = await registry.FindByMemoryProjectNameAsync("missing-project", CancellationToken.None);
        Assert.IsNull(byId);
        Assert.IsNull(byName);
    }

    /// <summary>
    /// Verifies that creating a project routing persists its Basic Memory identity and requested initial status.
    /// </summary>
    [TestMethod]
    public async Task CreateAsyncPersistsProjectRouting()
    {
        var options = ContextOptions.Create(_databasePath, pooling: false);
        await using (var dbContext = new ProjectMemoryProxyDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
        }

        var memoryProjectId = Guid.NewGuid();
        var memoryProjectName = "project-a";
        var registry = CreateRegistry(_databasePath);

        var created = await registry.CreateAsync(memoryProjectId, memoryProjectName, Status.Inactive, CancellationToken.None);
        Assert.AreEqual(memoryProjectId, created.MemoryProjectId);
        Assert.AreEqual(memoryProjectName, created.MemoryProjectName);
        Assert.AreEqual(Status.Inactive, created.Status);

        var persisted = await registry.FindByMemoryProjectIdAsync(memoryProjectId, CancellationToken.None);
        Assert.IsNotNull(persisted);
        Assert.AreEqual(created, persisted);
    }

    /// <summary>
    /// Verifies that creating a project routing rejects unsupported status values before modifying persistence.
    /// </summary>
    [TestMethod]
    public async Task CreateAsyncRejectsUnsupportedStatus()
    {
        var options = ContextOptions.Create(_databasePath, pooling: false);
        await using (var dbContext = new ProjectMemoryProxyDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
        }

        var registry = CreateRegistry(_databasePath);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => registry.CreateAsync(Guid.NewGuid(), "project-a", (Status)int.MaxValue, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that an exact registered context binding can be found with its target project identifier and persisted status.
    /// </summary>
    [TestMethod]
    public async Task FindBindingAsyncReturnsExactBinding()
    {
        var memoryProjectId = Guid.NewGuid();
        await CreateDatabaseAsync(_databasePath, new ProjectRoutingEntity
        {
            MemoryProjectId = memoryProjectId,
            MemoryProjectName = "project-a",
            Bindings =
                {
                    new ContextBindingEntity
                        {
                            BindingType = BindingType.GitRepository,
                            BindingName = "github.com/ChrSchu90/ProjectMemoryProxy",
                            Status = Status.Inactive
                        }
                }
        });

        var registry = CreateRegistry(_databasePath);
        Assert.IsTrue(ContextId.TryParse("git:github.com/ChrSchu90/ProjectMemoryProxy", out var contextId));

        var binding = await registry.FindBindingAsync(contextId!, CancellationToken.None);
        Assert.IsNotNull(binding);
        Assert.AreEqual(contextId, binding.ContextId);
        Assert.AreEqual(memoryProjectId, binding.MemoryProjectId);
        Assert.AreEqual(Status.Inactive, binding.Status);
    }

    /// <summary>
    /// Verifies that binding lookup returns no result when the exact technical context is not registered.
    /// </summary>
    [TestMethod]
    public async Task FindBindingAsyncReturnsNullForUnknownContext()
    {
        await CreateDatabaseAsync(_databasePath,
            new ProjectRoutingEntity
            {
                MemoryProjectId = Guid.NewGuid(),
                MemoryProjectName = "project-a",
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

        var binding = await registry.FindBindingAsync(contextId!, CancellationToken.None);
        Assert.IsNull(binding);
    }

    /// <summary>
    /// Verifies that creating a context binding persists the requested target project and initial status.
    /// </summary>
    [TestMethod]
    public async Task CreateBindingAsyncPersistsBinding()
    {
        var options = ContextOptions.Create(_databasePath, pooling: false);
        await using (var dbContext = new ProjectMemoryProxyDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
        }

        var memoryProjectId = Guid.NewGuid();
        var registry = CreateRegistry(_databasePath);

        await registry.CreateAsync(memoryProjectId, "project-a", Status.Active, CancellationToken.None);
        Assert.IsTrue(ContextId.TryParse("chatgpt-project:chatty-mcp-and-aiharborvm", out var contextId));

        var created = await registry.CreateBindingAsync(contextId!, memoryProjectId, Status.Inactive, CancellationToken.None);
        Assert.AreEqual(contextId, created.ContextId);
        Assert.AreEqual(memoryProjectId, created.MemoryProjectId);
        Assert.AreEqual(Status.Inactive, created.Status);

        var persisted = await registry.FindBindingAsync(contextId!, CancellationToken.None);
        Assert.IsNotNull(persisted);
        Assert.AreEqual(created, persisted);
    }

    /// <summary>
    /// Verifies that creating a context binding fails when its target project routing is not registered.
    /// </summary>
    [TestMethod]
    public async Task CreateBindingAsyncRejectsMissingProjectRouting()
    {
        var options = ContextOptions.Create(_databasePath, pooling: false);
        await using (var dbContext = new ProjectMemoryProxyDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
        }

        var registry = CreateRegistry(_databasePath);
        Assert.IsTrue(ContextId.TryParse("git:github.com/ChrSchu90/ProjectMemoryProxy", out var contextId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => registry.CreateBindingAsync(contextId!, Guid.NewGuid(), Status.Active, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that creating a duplicate exact context binding reports a registry conflict instead of leaking an EF Core exception.
    /// </summary>
    [TestMethod]
    public async Task CreateBindingAsyncReportsDuplicateContextConflict()
    {
        var options = ContextOptions.Create(_databasePath, pooling: false);
        await using (var dbContext = new ProjectMemoryProxyDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
        }

        var projectAId = Guid.NewGuid();
        var projectBId = Guid.NewGuid();
        var registry = CreateRegistry(_databasePath);

        await registry.CreateAsync(projectAId, "project-a", Status.Active, CancellationToken.None);
        await registry.CreateAsync(projectBId, "project-b", Status.Active, CancellationToken.None);
        Assert.IsTrue(ContextId.TryParse("git:github.com/ChrSchu90/ProjectMemoryProxy", out var contextId));

        await registry.CreateBindingAsync(contextId!, projectAId, Status.Active, CancellationToken.None);
        await Assert.ThrowsAsync<ProjectRegistryConflictException>(() => registry.CreateBindingAsync(contextId!, projectBId, Status.Active, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that creating a context binding rejects unsupported status values before modifying persistence.
    /// </summary>
    [TestMethod]
    public async Task CreateBindingAsyncRejectsUnsupportedStatus()
    {
        var options = ContextOptions.Create(_databasePath, pooling: false);
        await using (var dbContext = new ProjectMemoryProxyDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
        }

        var memoryProjectId = Guid.NewGuid();
        var registry = CreateRegistry(_databasePath);

        await registry.CreateAsync(memoryProjectId, "project-a", Status.Active, CancellationToken.None);
        Assert.IsTrue(ContextId.TryParse("git:github.com/ChrSchu90/ProjectMemoryProxy", out var contextId));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => registry.CreateBindingAsync(contextId!, memoryProjectId, (Status)int.MaxValue, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that a project routing and active context binding created through the registry resolve through the runtime routing manager.
    /// </summary>
    [TestMethod]
    public async Task CreatedProjectAndBindingResolveThroughRoutingManager()
    {
        var options = ContextOptions.Create(_databasePath, pooling: false);
        await using (var dbContext = new ProjectMemoryProxyDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
        }

        var memoryProjectId = Guid.NewGuid(); const string memoryProjectName = "project-a";
        Assert.IsTrue(ContextId.TryParse("git:github.com/ChrSchu90/ProjectMemoryProxy", out var contextId));
        var registry = CreateRegistry(_databasePath);
        await registry.CreateAsync(memoryProjectId, memoryProjectName, Status.Active, CancellationToken.None);
        await registry.CreateBindingAsync(contextId!, memoryProjectId, Status.Active, CancellationToken.None);
        var routingManager = new RoutingManager(registry, NullLogger<RoutingManager>.Instance);

        var resolution = await routingManager.ResolveContextAsync(contextId!, CancellationToken.None);
        Assert.AreEqual(ContextResolutionStatus.Resolved, resolution.Status);
        Assert.AreEqual(memoryProjectId, resolution.MemoryProjectId);
        Assert.AreEqual(memoryProjectName, resolution.MemoryProjectName);
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
