namespace ProjectMemoryProxy.BasicMemory.Tests.Mirroring;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol;
using ProjectMemoryProxy.BasicMemory.Mirroring;
using ProjectMemoryProxy.BasicMemory.Policy;
using ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Tests for <see cref="BasicMemoryInvocationArgumentBuilder"/>
/// </summary>
[TestClass]
public sealed class BasicMemoryInvocationArgumentBuilderTests
{
    #region Static Fields

    #endregion

    #region Private Fields

    #endregion

    #region Tests Constructors

    #endregion

    #region Test Preparation

    #endregion

    #region Tests

    /// <summary>
    /// Verifies that a validated context is replaced with the server-controlled Basic Memory project identifier.
    /// </summary>
    [TestMethod]
    public async Task BuildAsyncInjectsProjectId()
    {
        var memoryProjectId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var builder = CreateBuilder(new ProjectRoute(memoryProjectId, "project-memory-proxy", Status.Active, Status.Active));
        var routingAnalysis = CreateProjectIdAnalysis();
        var publicSchema =
            ParseJson(
                """
                {
                  "type": "object",
                  "properties": {
                    "query": {
                      "type": "string"
                    },
                    "context_id": {
                      "type": "string"
                    }
                  },
                  "required": [
                    "query",
                    "context_id"
                  ],
                  "additionalProperties": false
                }
                """);

        var arguments = new Dictionary<string, JsonElement>
        {
            ["query"] = ParseJson("\"routing\""),
            ["context_id"] = ParseJson("\"git:github.com/ChrSchu90/ProjectMemoryProxy\"")
        };

        var result = await builder.BuildAsync(routingAnalysis, publicSchema, arguments);
        Assert.IsFalse(result.ContainsKey("context_id"));
        Assert.AreEqual(memoryProjectId.ToString("D"), result["project_id"]);
        Assert.AreEqual("routing", ((JsonElement)result["query"]!).GetString());
    }

    /// <summary>
    /// Verifies that a project-name-only upstream contract receives the validated Basic Memory project name.
    /// </summary>
    [TestMethod]
    public async Task BuildAsyncInjectsProjectName()
    {
        var builder = CreateBuilder(new ProjectRoute(Guid.NewGuid(), "project-memory-proxy", Status.Active, Status.Active));
        var routingAnalysis = CreateProjectNameAnalysis();
        var result = await builder.BuildAsync(
                         routingAnalysis,
                         CreateContextOnlySchema(),
                         new Dictionary<string, JsonElement>
                         {
                             ["context_id"] = ParseJson("\"git:github.com/ChrSchu90/ProjectMemoryProxy\"")
                         });

        Assert.AreEqual("project-memory-proxy", result["project"]);
        Assert.IsFalse(result.ContainsKey("project_id"));
    }

    /// <summary>
    /// Verifies that arguments not exposed by the rewritten public schema are rejected before upstream invocation.
    /// </summary>
    [TestMethod]
    public async Task BuildAsyncRejectsHiddenRoutingArgument()
    {
        var builder = CreateBuilder(new ProjectRoute(Guid.NewGuid(), "project-memory-proxy", Status.Active, Status.Active));
        var exception = await Assert.ThrowsAsync<McpProtocolException>(() => builder.BuildAsync(
                            CreateProjectIdAnalysis(),
                            CreateContextOnlySchema(),
                            new Dictionary<string, JsonElement>
                            {
                                ["context_id"] = ParseJson("\"git:github.com/ChrSchu90/ProjectMemoryProxy\""),
                                ["workspace"] = ParseJson("\"cloud\"")
                            }));

        Assert.AreEqual(
            McpErrorCode.InvalidParams,
            exception.ErrorCode);
    }

    /// <summary>
    /// Verifies that an unbound context cannot produce upstream Basic Memory arguments.
    /// </summary>
    [TestMethod]
    public async Task BuildAsyncRejectsUnboundContext()
    {
        var builder = CreateBuilder(null);
        await Assert.ThrowsAsync<McpProtocolException>(() => builder.BuildAsync(
                CreateProjectIdAnalysis(),
                CreateContextOnlySchema(),
                new Dictionary<string, JsonElement>
                {
                    ["context_id"] = ParseJson("\"git:github.com/ChrSchu90/ProjectMemoryProxy\"")
                }));
    }

    #endregion

    #region Private Methods

    private static BasicMemoryInvocationArgumentBuilder CreateBuilder(ProjectRoute? route)
    {
        var routingManager = new RoutingManager(new StubProjectRegistry(route), NullLogger<RoutingManager>.Instance);
        return new BasicMemoryInvocationArgumentBuilder(routingManager);
    }

    private static RoutingSchemaAnalysis CreateProjectIdAnalysis()
    {
        return new RoutingSchemaAnalysis(RoutingSchemaStatus.AutomaticallyRoutable, ProjectSelectorKind.ProjectId, InjectProjectName: false, ["project_id"]);
    }

    private static RoutingSchemaAnalysis CreateProjectNameAnalysis()
    {
        return new RoutingSchemaAnalysis(RoutingSchemaStatus.AutomaticallyRoutable, ProjectSelectorKind.ProjectName, InjectProjectName: true, ["project"]);
    }

    private static RoutingSchemaAnalysis CreateProjectIdWithRequiredProjectNameAnalysis()
    {
        return new RoutingSchemaAnalysis(RoutingSchemaStatus.AutomaticallyRoutable, ProjectSelectorKind.ProjectId, InjectProjectName: true, ["project", "project_id"]);
    }

    private static JsonElement CreateContextOnlySchema()
    {
        return ParseJson(
            """
            {
              "type": "object",
              "properties": {
                "context_id": {
                  "type": "string"
                }
              },
              "required": [
                "context_id"
              ],
              "additionalProperties": false
            }
            """);
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    #endregion

    #region Test Classes

    private sealed class StubProjectRegistry : IProjectRegistry
    {
        #region Private Fields

        private readonly Func<ContextId, CancellationToken, Task<ProjectRoute?>> _findRoute;

        #endregion

        #region Constructors

        public StubProjectRegistry(ProjectRoute? route)
            : this((_, _) => Task.FromResult(route))
        {
        }

        public StubProjectRegistry(Func<ContextId, CancellationToken, Task<ProjectRoute?>> findRoute)
        {
            _findRoute = findRoute ?? throw new ArgumentNullException(nameof(findRoute));
        }

        #endregion

        #region Properties

        public int FindRouteCallCount { get; private set; }

        #endregion

        #region Public Methods

        public Task<ProjectRoute?> FindRouteAsync(ContextId contextId, CancellationToken cancellationToken = default)
        {
            FindRouteCallCount++;
            return _findRoute(contextId, cancellationToken);
        }

        public Task<ProjectRouting?> FindByMemoryProjectIdAsync(Guid memoryProjectId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProjectRouting?> FindByMemoryProjectNameAsync(string memoryProjectName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProjectRouting> CreateAsync(Guid memoryProjectId, string memoryProjectName, Status status, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ContextBinding?> FindBindingAsync(ContextId contextId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ContextBinding> CreateBindingAsync(ContextId contextId, Guid memoryProjectId, Status status, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TryUpdateProjectStatusAsync(Guid memoryProjectId, Status expectedStatus, Status newStatus, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TryUpdateBindingStatusAsync(ContextId contextId, Status expectedStatus, Status newStatus, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TryRemoveBindingAsync(ContextId contextId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TryRemoveProjectAsync(Guid memoryProjectId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ProjectRouting>> ListProjectsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ProjectRouting>>(Array.Empty<ProjectRouting>());
        }

        #endregion
    }

    #endregion
}
