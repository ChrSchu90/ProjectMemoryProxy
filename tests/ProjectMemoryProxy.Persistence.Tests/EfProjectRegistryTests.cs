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
    /// Verifies that all registered context bindings are listed with exact context identifiers, targets, statuses, and deterministic ordering.
    /// </summary>
    [TestMethod]
    public async Task ListBindingsAsyncReturnsAllBindings()
    {
        var chatGptProjectId = Guid.NewGuid();
        var gitProjectId = Guid.NewGuid();
        await CreateDatabaseAsync(_databasePath,
            new ProjectRoutingEntity
            {
                MemoryProjectId = chatGptProjectId,
                MemoryProjectName = "chatgpt-project",
                Bindings =
                        {
                            new ContextBindingEntity
                                {
                                    BindingType = BindingType.ChatGptProject,
                                    BindingName = "chatty-mcp-and-aiharborvm",
                                    Status = Status.Inactive
                                }
                        }
            },
            new ProjectRoutingEntity
            {
                MemoryProjectId = gitProjectId,
                MemoryProjectName = "git-project",
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
        var bindings = await registry.ListBindingsAsync(CancellationToken.None);

        Assert.HasCount(2, bindings);
        Assert.AreEqual("chatgpt-project:chatty-mcp-and-aiharborvm", bindings[0].ContextId.ToString());
        Assert.AreEqual(chatGptProjectId, bindings[0].MemoryProjectId);
        Assert.AreEqual(Status.Inactive, bindings[0].Status);
        Assert.AreEqual("git:github.com/ChrSchu90/ProjectMemoryProxy", bindings[1].ContextId.ToString());
        Assert.AreEqual(gitProjectId, bindings[1].MemoryProjectId);
        Assert.AreEqual(Status.Active, bindings[1].Status);
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

    /// <summary>
    /// Verifies that a project-routing status is updated when the persisted status matches the expected status.
    /// </summary>
    [TestMethod]
    public async Task TryUpdateProjectStatusAsyncUpdatesMatchingStatus()
    {
        var memoryProjectId = Guid.NewGuid();
        await CreateDatabaseAsync(_databasePath, new ProjectRoutingEntity
        {
            MemoryProjectId = memoryProjectId,
            MemoryProjectName = "project-a",
            Status = Status.Active
        });

        var registry = CreateRegistry(_databasePath);
        var updated = await registry.TryUpdateProjectStatusAsync(memoryProjectId, Status.Active, Status.Inactive, CancellationToken.None);
        Assert.IsTrue(updated);

        var persisted = await registry.FindByMemoryProjectIdAsync(memoryProjectId, CancellationToken.None);
        Assert.IsNotNull(persisted);
        Assert.AreEqual(Status.Inactive, persisted.Status);
    }

    /// <summary>
    /// Verifies that a project-routing status is not changed when the persisted status no longer matches the expected status.
    /// </summary>
    [TestMethod]
    public async Task TryUpdateProjectStatusAsyncRejectsStaleExpectedStatus()
    {
        var memoryProjectId = Guid.NewGuid();
        await CreateDatabaseAsync(_databasePath, new ProjectRoutingEntity
        {
            MemoryProjectId = memoryProjectId,
            MemoryProjectName = "project-a",
            Status = Status.Inactive
        });

        var registry = CreateRegistry(_databasePath);
        var updated = await registry.TryUpdateProjectStatusAsync(memoryProjectId, Status.Active, Status.Inactive, CancellationToken.None);
        Assert.IsFalse(updated);

        var persisted = await registry.FindByMemoryProjectIdAsync(memoryProjectId, CancellationToken.None);
        Assert.IsNotNull(persisted);
        Assert.AreEqual(Status.Inactive, persisted.Status);
    }

    /// <summary>
    /// Verifies that a context-binding status is updated when the persisted status matches the expected status.
    /// </summary>
    [TestMethod]
    public async Task TryUpdateBindingStatusAsyncUpdatesMatchingStatus()
    {
        var memoryProjectId = Guid.NewGuid();
        await CreateDatabaseAsync(_databasePath, new ProjectRoutingEntity
        {
            MemoryProjectId = memoryProjectId,
            MemoryProjectName = "project-a",
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

        Assert.IsTrue(ContextId.TryParse("git:github.com/ChrSchu90/ProjectMemoryProxy", out var contextId));
        var registry = CreateRegistry(_databasePath);
        var updated = await registry.TryUpdateBindingStatusAsync(contextId!, Status.Active, Status.Inactive, CancellationToken.None);
        Assert.IsTrue(updated);

        var persisted = await registry.FindBindingAsync(contextId!, CancellationToken.None);
        Assert.IsNotNull(persisted);
        Assert.AreEqual(Status.Inactive, persisted.Status);
    }

    /// <summary>
    /// Verifies that a context-binding status is not changed when the persisted status no longer matches the expected status.
    /// </summary>
    [TestMethod]
    public async Task TryUpdateBindingStatusAsyncRejectsStaleExpectedStatus()
    {
        var memoryProjectId = Guid.NewGuid();
        await CreateDatabaseAsync(_databasePath, new ProjectRoutingEntity
        {
            MemoryProjectId = memoryProjectId,
            MemoryProjectName = "project-a",
            Status = Status.Active,
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

        Assert.IsTrue(ContextId.TryParse("git:github.com/ChrSchu90/ProjectMemoryProxy", out var contextId));
        var registry = CreateRegistry(_databasePath);
        var updated = await registry.TryUpdateBindingStatusAsync(contextId!, Status.Active, Status.Inactive, CancellationToken.None);
        Assert.IsFalse(updated);

        var persisted = await registry.FindBindingAsync(contextId!, CancellationToken.None);
        Assert.IsNotNull(persisted);
        Assert.AreEqual(Status.Inactive, persisted.Status);
    }

    /// <summary>
    /// Verifies that changing the status of an unknown project routing returns false without creating registry state.
    /// </summary>
    [TestMethod]
    public async Task TryUpdateProjectStatusAsyncReturnsFalseForUnknownProject()
    {
        var options = ContextOptions.Create(_databasePath, pooling: false);
        await using (var dbContext = new ProjectMemoryProxyDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
        }

        var registry = CreateRegistry(_databasePath);
        var updated = await registry.TryUpdateProjectStatusAsync(Guid.NewGuid(), Status.Active, Status.Inactive, CancellationToken.None);
        Assert.IsFalse(updated);
    }

    /// <summary>
    /// Verifies that changing the status of an unknown context binding returns false without creating registry state.
    /// </summary>
    [TestMethod]
    public async Task TryUpdateBindingStatusAsyncReturnsFalseForUnknownContext()
    {
        var options = ContextOptions.Create(_databasePath, pooling: false);
        await using (var dbContext = new ProjectMemoryProxyDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
        }

        Assert.IsTrue(ContextId.TryParse("git:github.com/ChrSchu90/ProjectMemoryProxy", out var contextId));
        var registry = CreateRegistry(_databasePath);
        var updated = await registry.TryUpdateBindingStatusAsync(contextId!, Status.Active, Status.Inactive, CancellationToken.None);
        Assert.IsFalse(updated);
    }

    /// <summary>
    /// Verifies that project-routing status updates reject unsupported expected and target status values.
    /// </summary>
    [TestMethod]
    public async Task TryUpdateProjectStatusAsyncRejectsUnsupportedStatuses()
    {
        var registry = CreateRegistry(_databasePath);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => registry.TryUpdateProjectStatusAsync(Guid.NewGuid(), (Status)int.MaxValue, Status.Active, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => registry.TryUpdateProjectStatusAsync(Guid.NewGuid(), Status.Active, (Status)int.MaxValue, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that context-binding status updates reject unsupported expected and target status values.
    /// </summary>
    [TestMethod]
    public async Task TryUpdateBindingStatusAsyncRejectsUnsupportedStatuses()
    {
        Assert.IsTrue(ContextId.TryParse("git:github.com/ChrSchu90/ProjectMemoryProxy", out var contextId));
        var registry = CreateRegistry(_databasePath);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => registry.TryUpdateBindingStatusAsync(contextId!, (Status)int.MaxValue, Status.Active, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => registry.TryUpdateBindingStatusAsync(contextId!, Status.Active, (Status)int.MaxValue, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that project-routing and context-binding status transitions immediately affect runtime context resolution.
    /// </summary>
    [TestMethod]
    public async Task StatusTransitionsAffectRuntimeContextResolution()
    {
        var options = ContextOptions.Create(_databasePath, pooling: false);
        await using (var dbContext = new ProjectMemoryProxyDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
        }

        var memoryProjectId = Guid.NewGuid();

        const string memoryProjectName = "project-a";
        Assert.IsTrue(ContextId.TryParse("git:github.com/ChrSchu90/ProjectMemoryProxy", out var contextId));

        var registry = CreateRegistry(_databasePath);
        var routingManager = new RoutingManager(registry, NullLogger<RoutingManager>.Instance);
        await registry.CreateAsync(memoryProjectId, memoryProjectName, Status.Active, CancellationToken.None);
        await registry.CreateBindingAsync(contextId!, memoryProjectId, Status.Active, CancellationToken.None);

        var resolved = await routingManager.ResolveContextAsync(contextId!, CancellationToken.None);
        Assert.AreEqual(ContextResolutionStatus.Resolved, resolved.Status);
        Assert.IsTrue(await registry.TryUpdateBindingStatusAsync(contextId!, Status.Active, Status.Inactive, CancellationToken.None));

        var bindingInactive = await routingManager.ResolveContextAsync(contextId!, CancellationToken.None);
        Assert.AreEqual(ContextResolutionStatus.BindingInactive, bindingInactive.Status);
        Assert.IsTrue(await registry.TryUpdateBindingStatusAsync(contextId!, Status.Inactive, Status.Active, CancellationToken.None));

        var bindingReactivated = await routingManager.ResolveContextAsync(contextId!, CancellationToken.None);
        Assert.AreEqual(ContextResolutionStatus.Resolved, bindingReactivated.Status);
        Assert.IsTrue(await registry.TryUpdateProjectStatusAsync(memoryProjectId, Status.Active, Status.Inactive, CancellationToken.None));

        var projectInactive = await routingManager.ResolveContextAsync(contextId!, CancellationToken.None);
        Assert.AreEqual(ContextResolutionStatus.ProjectRoutingInactive, projectInactive.Status);
        Assert.IsTrue(await registry.TryUpdateProjectStatusAsync(memoryProjectId, Status.Inactive, Status.Active, CancellationToken.None));

        var projectReactivated = await routingManager.ResolveContextAsync(contextId!, CancellationToken.None);
        Assert.AreEqual(ContextResolutionStatus.Resolved, projectReactivated.Status);
        Assert.AreEqual(memoryProjectId, projectReactivated.MemoryProjectId);
        Assert.AreEqual(memoryProjectName, projectReactivated.MemoryProjectName);
    }

    /// <summary>
    /// Verifies that removing a project routing physically deletes the routing and all dependent context bindings.
    /// </summary>
    [TestMethod]
    public async Task TryRemoveProjectAsyncRemovesRoutingAndDependentBindings()
    {
        var memoryProjectId = Guid.NewGuid();
        await CreateDatabaseAsync(_databasePath, new ProjectRoutingEntity
        {
            MemoryProjectId = memoryProjectId,
            MemoryProjectName = "project-a",
            Status = Status.Active,
            Bindings =
                {
                    new ContextBindingEntity
                        {
                            BindingType = BindingType.GitRepository,
                            BindingName = "github.com/ChrSchu90/ProjectMemoryProxy",
                            Status = Status.Active
                        },
                    new ContextBindingEntity
                        {
                            BindingType = BindingType.ChatGptProject,
                            BindingName = "chatty-mcp-and-aiharborvm",
                            Status = Status.Inactive
                        }
                }
        });

        Assert.IsTrue(ContextId.TryParse("git:github.com/ChrSchu90/ProjectMemoryProxy", out var gitContext));
        Assert.IsTrue(ContextId.TryParse("chatgpt-project:chatty-mcp-and-aiharborvm", out var chatGptContext));

        var registry = CreateRegistry(_databasePath);
        var removed = await registry.TryRemoveProjectAsync(memoryProjectId, CancellationToken.None);
        Assert.IsTrue(removed);
        Assert.IsNull(await registry.FindByMemoryProjectIdAsync(memoryProjectId, CancellationToken.None));
        Assert.IsNull(await registry.FindBindingAsync(gitContext!, CancellationToken.None));
        Assert.IsNull(await registry.FindBindingAsync(chatGptContext!, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that removing an unknown project routing returns false without changing registry state.
    /// </summary>
    [TestMethod]
    public async Task TryRemoveProjectAsyncReturnsFalseForUnknownProject()
    {
        var options = ContextOptions.Create(_databasePath, pooling: false);
        await using (var dbContext = new ProjectMemoryProxyDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
        }

        var registry = CreateRegistry(_databasePath);
        Assert.IsFalse(await registry.TryRemoveProjectAsync(Guid.NewGuid(), CancellationToken.None));
    }

    /// <summary>
    /// Verifies that removing a project routing rejects an empty Basic Memory project identifier before accessing persistence.
    /// </summary>
    [TestMethod]
    public async Task TryRemoveProjectAsyncRejectsEmptyProjectId()
    {
        var registry = CreateRegistry(_databasePath);
        await Assert.ThrowsAsync<ArgumentException>(() => registry.TryRemoveProjectAsync(Guid.Empty, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that removing an active exact context binding deletes only the binding and preserves its project routing.
    /// </summary>
    [TestMethod]
    public async Task TryRemoveBindingAsyncRemovesActiveBindingAndPreservesProjectRouting()
    {
        var memoryProjectId = Guid.NewGuid();
        await CreateDatabaseAsync(_databasePath, new ProjectRoutingEntity
        {
            MemoryProjectId = memoryProjectId,
            MemoryProjectName = "project-a",
            Status = Status.Active,
            Bindings =
                {
                    new ContextBindingEntity { BindingType = BindingType.GitRepository, BindingName = "github.com/ChrSchu90/ProjectMemoryProxy", Status = Status.Active },
                    new ContextBindingEntity { BindingType = BindingType.ChatGptProject, BindingName = "chatty-mcp-and-aiharborvm", Status = Status.Active }
                }
        });

        Assert.IsTrue(ContextId.TryParse("git:github.com/ChrSchu90/ProjectMemoryProxy", out var contextId));
        Assert.IsTrue(ContextId.TryParse("chatgpt-project:chatty-mcp-and-aiharborvm", out var otherContextId));
        var registry = CreateRegistry(_databasePath);
        var removed = await registry.TryRemoveBindingAsync(contextId!, CancellationToken.None);
        Assert.IsTrue(removed);
        Assert.IsNull(await registry.FindBindingAsync(contextId!, CancellationToken.None));
        Assert.IsNotNull(await registry.FindBindingAsync(otherContextId!, CancellationToken.None));

        var routing = await registry.FindByMemoryProjectIdAsync(memoryProjectId, CancellationToken.None);
        Assert.IsNotNull(routing);
        Assert.AreEqual(Status.Active, routing.Status);
    }

    /// <summary>
    /// Verifies that removing an inactive exact context binding physically deletes it instead of changing its status.
    /// </summary>
    [TestMethod]
    public async Task TryRemoveBindingAsyncRemovesInactiveBinding()
    {
        await CreateDatabaseAsync(_databasePath, new ProjectRoutingEntity
        {
            MemoryProjectId = Guid.NewGuid(),
            MemoryProjectName = "project-a",
            Status = Status.Active,
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

        Assert.IsTrue(ContextId.TryParse("git:github.com/ChrSchu90/ProjectMemoryProxy", out var contextId));
        var registry = CreateRegistry(_databasePath);
        var removed = await registry.TryRemoveBindingAsync(contextId!, CancellationToken.None);
        Assert.IsTrue(removed);
        Assert.IsNull(await registry.FindBindingAsync(contextId!, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that removing an unknown exact context binding returns false without creating or changing registry state.
    /// </summary>
    [TestMethod]
    public async Task TryRemoveBindingAsyncReturnsFalseForUnknownContext()
    {
        var options = ContextOptions.Create(_databasePath, pooling: false);
        await using (var dbContext = new ProjectMemoryProxyDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
        }

        Assert.IsTrue(ContextId.TryParse("git:github.com/ChrSchu90/ProjectMemoryProxy", out var contextId));
        var registry = CreateRegistry(_databasePath);
        Assert.IsFalse(await registry.TryRemoveBindingAsync(contextId!, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that physical binding removal changes runtime resolution to <c>NotBound</c> and a later new binding resolves again.
    /// </summary>
    [TestMethod]
    public async Task BindingRemovalAndRebindAffectRuntimeContextResolution()
    {
        var options = ContextOptions.Create(_databasePath, pooling: false);
        await using (var dbContext = new ProjectMemoryProxyDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
        }

        var memoryProjectId = Guid.NewGuid();
        const string memoryProjectName = "project-a";
        Assert.IsTrue(ContextId.TryParse("git:github.com/ChrSchu90/ProjectMemoryProxy", out var contextId));

        var registry = CreateRegistry(_databasePath);
        var routingManager = new RoutingManager(registry, NullLogger<RoutingManager>.Instance);
        await registry.CreateAsync(memoryProjectId, memoryProjectName, Status.Active, CancellationToken.None);
        await registry.CreateBindingAsync(contextId!, memoryProjectId, Status.Active, CancellationToken.None);

        var resolved = await routingManager.ResolveContextAsync(contextId!, CancellationToken.None);
        Assert.AreEqual(ContextResolutionStatus.Resolved, resolved.Status);

        Assert.IsTrue(await registry.TryRemoveBindingAsync(contextId!, CancellationToken.None));
        var notBound = await routingManager.ResolveContextAsync(contextId!, CancellationToken.None);
        Assert.AreEqual(ContextResolutionStatus.NotBound, notBound.Status);

        await registry.CreateBindingAsync(contextId!, memoryProjectId, Status.Active, CancellationToken.None);
        var rebound = await routingManager.ResolveContextAsync(contextId!, CancellationToken.None);
        Assert.AreEqual(ContextResolutionStatus.Resolved, rebound.Status);
        Assert.AreEqual(memoryProjectId, rebound.MemoryProjectId);
        Assert.AreEqual(memoryProjectName, rebound.MemoryProjectName);
    }

    #endregion

    #region Private Methods

    private static EfProjectRegistry CreateRegistry(string databasePath)
    {
        var options = ContextOptions.Create(databasePath, pooling: false);
        return new EfProjectRegistry(new TestDbContextFactory(options));
    }

    private static async Task CreateDatabaseAsync(string databasePath, params ProjectRoutingEntity[] projectRoutings)
    {
        var options = ContextOptions.Create(databasePath, pooling: false);

        await using var dbContext = new ProjectMemoryProxyDbContext(options);
        await dbContext.Database.MigrateAsync();

        foreach (var projectRouting in projectRoutings)
        {
            dbContext.RoutingProjects.Add(projectRouting);
        }

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
