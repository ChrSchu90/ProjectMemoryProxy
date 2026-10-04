namespace ProjectMemoryProxy.BasicMemory.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ProjectMemoryProxy.BasicMemory.Client;
using ProjectMemoryProxy.BasicMemory.Discovery;
using ProjectMemoryProxy.BasicMemory.Policy;
using ProjectMemoryProxy.BasicMemory.Projects;
using ProjectMemoryProxy.Core.Configuration;
using ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Live contract tests for the supported Basic Memory upstream version.
/// </summary>
[TestClass]
public sealed class BasicMemoryContractTests
{
    #region Static Fields

    private static readonly Guid MemoryProjectId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private const string MemoryProjectName = "basic-memory-contract-test";

    private static readonly string[] AutomaticallyRoutedTools =
    [
        "build_context",
        "delete_note",
        "edit_note",
        "list_directory",
        "move_note",
        "read_content",
        "read_note",
        "recent_activity",
        "schema_diff",
        "schema_infer",
        "schema_validate",
        "search_notes",
        "view_note",
        "write_note"
    ];

    private static readonly string[] GlobalAllowlistedTools =
    [
        "basic_memory_diagnostics"
    ];

    private static readonly string[] ExplicitAdapterTools =
    [
        //"some_placeholder_tool"
    ];

    private static readonly string[] ProjectLifecycleTools =
    [
        "create_memory_project",
        "delete_project",
        "list_memory_projects"
    ];

    private static readonly string[] IntentionallyBlockedTools =
    [
        "list_workspaces",
        "fetch",
        "search"
    ];

    private static readonly HashSet<string> ForbiddenPublicRoutingProperties = new(StringComparer.Ordinal)
    {
        "project",
        "project_id",
        "projects",
        "search_all_projects",
        "tenant",
        "tenant_id",
        "workspace",
        "workspace_id"
    };

    #endregion

    #region Private Fields

    #endregion

    #region Tests Constructors

    #endregion

    #region Test Preparation

    #endregion

    #region Tests

    /// <summary>
    /// Verifies that a configured live Basic Memory endpoint matches the reviewed upstream contract and that ProjectMemoryProxy exposes exactly the tools allowed by its routing policy.
    /// </summary>
    [TestMethod]
    public async Task LiveBasicMemoryEndpointMatchesExpectedContract()
    {
        var endpoint = GetTestBasicMemoryEndpointOrSkipTest();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var cancellationToken = cancellationTokenSource.Token;
        var options = Options.Create(new ProjectMemoryProxyOptions
        {
            BasicMemoryEndpoint = endpoint,
            BasicMemoryConnectionTimeout = TimeSpan.FromSeconds(10)
        });

        await using var upstreamClient = new BasicMemoryClient(options, NullLoggerFactory.Instance, NullLogger<BasicMemoryClient>.Instance);
        using var upstreamCatalog = new BasicMemoryToolCatalog(upstreamClient, NullLogger<BasicMemoryToolCatalog>.Instance);
        await upstreamCatalog.InitializeAsync(cancellationToken);

        var projectDirectory = new BasicMemoryProjectDirectory(upstreamCatalog);
        var projects = await projectDirectory.ListAsync(cancellationToken);
        Console.WriteLine($"Local Basic Memory projects: {projects.Count}");

        var classifier = new BasicMemoryToolClassifier();
        var classifications = upstreamCatalog.Tools
            .Select(tool =>
            {
                var classification = classifier.Classify(tool.ProtocolTool.Name, tool.ProtocolTool.InputSchema, out var routingAnalysis);
                return new ClassifiedTool(tool.ProtocolTool.Name, classification, routingAnalysis, tool.ProtocolTool.InputSchema.Clone());
            })
            .OrderBy(tool => tool.Name, StringComparer.Ordinal)
            .ToArray();

        WriteUpstreamSnapshot(endpoint, classifications);
        await using var proxyServer = await StartProxyServerAsync(endpoint, cancellationToken);
        await using var proxyClient = await CreateMcpClientAsync(GetMcpEndpoint(proxyServer), cancellationToken);
        var proxyTools = (await proxyClient.ListToolsAsync(cancellationToken: cancellationToken))
            .OrderBy(tool => tool.Name, StringComparer.Ordinal)
            .ToArray();

        WriteProxySnapshot(classifications, proxyTools);
        AssertToolNames(GetExpectedUpstreamToolNames(), classifications.Select(tool => tool.Name), "reviewed Basic Memory upstream contract");
        AssertClassification(AutomaticallyRoutedTools, classifications, ToolRoutingClassification.AutomaticallyRouted);
        AssertClassification(GlobalAllowlistedTools, classifications, ToolRoutingClassification.GlobalAllowlisted);
        AssertClassification(ExplicitAdapterTools, classifications, ToolRoutingClassification.ExplicitAdapter);
        AssertClassification(ProjectLifecycleTools, classifications, ToolRoutingClassification.ProjectLifecycle);
        AssertClassification(IntentionallyBlockedTools, classifications, ToolRoutingClassification.IntentionallyBlocked);
        AssertToolNames(
            Array.Empty<string>(),
            classifications
                .Where(tool => tool.Classification == ToolRoutingClassification.Blocked)
                .Select(tool => tool.Name), "unexpectedly blocked tools");

        var expectedProxyToolNames = classifications
            .Where(tool => tool.Classification is ToolRoutingClassification.AutomaticallyRouted or ToolRoutingClassification.GlobalAllowlisted)
            .Select(tool => tool.Name)
            .ToArray();

        AssertToolNames(expectedProxyToolNames, proxyTools.Select(tool => tool.Name), "ProjectMemoryProxy tools/list exposure");
        var expectedHiddenToolNames = classifications
            .Where(tool => tool.Classification is ToolRoutingClassification.ExplicitAdapter or ToolRoutingClassification.ProjectLifecycle or ToolRoutingClassification.IntentionallyBlocked or ToolRoutingClassification.Blocked)
            .Select(tool => tool.Name)
            .ToArray();

        var actualHiddenToolNames = classifications
            .Select(tool => tool.Name)
            .Except(proxyTools.Select(tool => tool.Name), StringComparer.Ordinal)
            .ToArray();

        AssertToolNames(expectedHiddenToolNames, actualHiddenToolNames, "tools hidden by ProjectMemoryProxy policy");
        var proxyToolsByName = proxyTools.ToDictionary(tool => tool.Name, StringComparer.Ordinal);
        foreach (var automaticallyRoutedToolName in classifications.Where(tool => tool.Classification == ToolRoutingClassification.AutomaticallyRouted).Select(tool => tool.Name))
        {
            AssertAutomaticallyRoutedPublicSchema(automaticallyRoutedToolName, proxyToolsByName[automaticallyRoutedToolName].ProtocolTool.InputSchema);
        }

        var diagnostics = proxyToolsByName["basic_memory_diagnostics"];
        Assert.IsFalse(diagnostics.ProtocolTool.InputSchema.GetProperty("properties").TryGetProperty("context_id", out _));

        var diagnosticsResult = await diagnostics.CallAsync(new Dictionary<string, object?>(), cancellationToken: cancellationToken);
        var diagnosticsText = string.Join(Environment.NewLine, diagnosticsResult.Content.OfType<TextContentBlock>().Select(block => block.Text));
        Assert.Contains("# Basic Memory Diagnostics", diagnosticsText);
    }

    #endregion

    #region Private Methods

    private static Uri GetTestBasicMemoryEndpointOrSkipTest()
    {
        const string EndpointEnvironmentVariable = "PROJECTMEMORYPROXY_TEST_BASIC_MEMORY_ENDPOINT";
        var endpointValue = Environment.GetEnvironmentVariable(EndpointEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(endpointValue))
        {
            Assert.Inconclusive("Endpoint not specified.");
            return null!;
        }

        if (!string.Equals(endpointValue, endpointValue.Trim(), StringComparison.Ordinal) ||
            !Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            Assert.Fail($"{EndpointEnvironmentVariable} must be an absolute HTTP or HTTPS URI without surrounding whitespace.");
            return null!;
        }

        return endpoint;
    }

    private static string[] GetExpectedUpstreamToolNames()
    {
        return AutomaticallyRoutedTools
            .Concat(GlobalAllowlistedTools)
            .Concat(ExplicitAdapterTools)
            .Concat(ProjectLifecycleTools)
            .Concat(IntentionallyBlockedTools)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task<WebApplication> StartProxyServerAsync(Uri basicMemoryEndpoint, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddLogging();
        builder.Services.AddSingleton(Options.Create(new ProjectMemoryProxyOptions
        {
            BasicMemoryEndpoint = basicMemoryEndpoint,
            BasicMemoryConnectionTimeout = TimeSpan.FromSeconds(10)
        }));

        builder.Services.AddSingleton<IProjectRegistry>(new StubProjectRegistry(new ProjectRoute(MemoryProjectId, MemoryProjectName, Status.Active, Status.Active)));
        builder.Services.AddSingleton<RoutingManager>();
        builder.Services.AddBasicMemory();
        builder.Services.AddMcpServer().WithHttpTransport(options => { options.SessionMode = HttpServerSessionMode.Stateless; });

        var app = builder.Build();

        try
        {
            await app.Services.RegisterBasicMemoryMirroredToolsAsync(cancellationToken);
            app.MapMcp("/mcp");
            await app.StartAsync(cancellationToken);
            return app;
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    private static async Task<McpClient> CreateMcpClientAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp,
            ConnectionTimeout = TimeSpan.FromSeconds(10),
            EnableStandaloneGetStream = false
        });

        return await McpClient.CreateAsync(transport, loggerFactory: NullLoggerFactory.Instance, cancellationToken: cancellationToken);
    }

    private static Uri GetMcpEndpoint(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ??
                        throw new InvalidOperationException("The test MCP server did not publish any addresses.");

        return new Uri($"{addresses.Single().TrimEnd('/')}/mcp");
    }

    private static void AssertClassification(IEnumerable<string> expectedToolNames, IEnumerable<ClassifiedTool> classifications, ToolRoutingClassification expectedClassification)
    {
        var actualToolNames = classifications.Where(tool => tool.Classification == expectedClassification).Select(tool => tool.Name);
        AssertToolNames(expectedToolNames, actualToolNames, $"{expectedClassification} classification");
    }

    private static void AssertToolNames(IEnumerable<string> expected, IEnumerable<string> actual, string contractName)
    {
        var expectedNames = expected.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var actualNames = actual.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var missingNames = expectedNames.Except(actualNames, StringComparer.Ordinal).ToArray();
        var unexpectedNames = actualNames.Except(expectedNames, StringComparer.Ordinal).ToArray();
        Assert.IsTrue(expectedNames.SequenceEqual(actualNames, StringComparer.Ordinal),
            $"{contractName} changed. " +
            $"Missing: [{FormatNames(missingNames)}]. " +
            $"Unexpected: [{FormatNames(unexpectedNames)}]. " +
            $"Expected: [{FormatNames(expectedNames)}]. " +
            $"Actual: [{FormatNames(actualNames)}].");
    }

    private static void AssertAutomaticallyRoutedPublicSchema(string toolName, JsonElement schema)
    {
        Assert.AreEqual(JsonValueKind.Object, schema.ValueKind, $"Tool '{toolName}' must expose an object input schema.");
        Assert.IsTrue(schema.TryGetProperty("properties", out var properties), $"Tool '{toolName}' must expose input properties.");
        Assert.AreEqual(JsonValueKind.Object, properties.ValueKind, $"Tool '{toolName}' input properties must be an object.");
        Assert.IsTrue(properties.TryGetProperty("context_id", out _), $"Tool '{toolName}' must expose context_id.");

        Assert.IsTrue(schema.TryGetProperty("required", out var required), $"Tool '{toolName}' must declare required properties.");
        Assert.AreEqual(JsonValueKind.Array, required.ValueKind, $"Tool '{toolName}' required must be an array.");
        Assert.IsTrue(required.EnumerateArray().Any(value => value.ValueKind == JsonValueKind.String && value.GetString() == "context_id"), $"Tool '{toolName}' must require context_id.");

        Assert.IsTrue(schema.TryGetProperty("additionalProperties", out var additionalProperties), $"Tool '{toolName}' must define additionalProperties.");
        Assert.AreEqual(JsonValueKind.False, additionalProperties.ValueKind, $"Tool '{toolName}' must reject undeclared public arguments.");

        AssertDoesNotExposeForbiddenRoutingProperties(toolName, schema);
    }

    private static void AssertDoesNotExposeForbiddenRoutingProperties(string toolName, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("properties") && property.Value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var declaredProperty in property.Value.EnumerateObject())
                        {
                            Assert.DoesNotContain(declaredProperty.Name, ForbiddenPublicRoutingProperties, $"Tool '{toolName}' must not expose routing property '{declaredProperty.Name}'.");
                        }
                    }

                    if (property.NameEquals("required") && property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var requiredProperty in property.Value.EnumerateArray())
                        {
                            if (requiredProperty.ValueKind == JsonValueKind.String)
                            {
                                var requiredPropertyName = requiredProperty.GetString();
                                Assert.IsFalse(requiredPropertyName != null && ForbiddenPublicRoutingProperties.Contains(requiredPropertyName), $"Tool '{toolName}' must not require routing property '{requiredPropertyName}'.");
                            }
                        }
                    }

                    AssertDoesNotExposeForbiddenRoutingProperties(toolName, property.Value);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    AssertDoesNotExposeForbiddenRoutingProperties(toolName, item);
                }

                break;
        }
    }

    private static void WriteUpstreamSnapshot(Uri endpoint, IReadOnlyList<ClassifiedTool> classifications)
    {
        Console.WriteLine("Basic Memory contract snapshot");
        Console.WriteLine("==============================");
        Console.WriteLine($"Endpoint: {endpoint}");
        Console.WriteLine($"Upstream tools: {classifications.Count}");

        var reviewedToolNames = GetExpectedUpstreamToolNames();
        var upstreamToolNames = classifications
                .Select(tool => tool.Name)
                .ToArray();

        var missingReviewedTools = reviewedToolNames
                .Except(upstreamToolNames, StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

        var unreviewedUpstreamTools = upstreamToolNames
                .Except(reviewedToolNames, StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

        Console.WriteLine($"Missing reviewed tools: [{FormatNames(missingReviewedTools)}]");
        Console.WriteLine($"Unreviewed upstream tools: [{FormatNames(unreviewedUpstreamTools)}]");
        Console.WriteLine();

        foreach (var classification in Enum.GetValues<ToolRoutingClassification>())
        {
            var tools = classifications.Where(tool => tool.Classification == classification).ToArray();
            Console.WriteLine($"{classification}: {tools.Length}");
            foreach (var tool in tools)
            {
                var routingStatus = tool.RoutingAnalysis?.Status.ToString() ?? "<not analyzed>";
                var projectSelector = tool.RoutingAnalysis?.ProjectSelector.ToString() ?? "<none>";
                var suppressedProperties = tool.RoutingAnalysis == null ? "<none>" : FormatNames(tool.RoutingAnalysis.SuppressedProperties);
                Console.WriteLine(
                    $"  {tool.Name} | " +
                    $"routing={routingStatus} | " +
                    $"selector={projectSelector} | " +
                    $"suppressed=[{suppressedProperties}]");
            }

            Console.WriteLine();
        }

        var schemaDiagnostics = classifications.Where(tool =>
            {
                var expectedClassification = GetExpectedClassification(tool.Name);
                return expectedClassification == null ||
                       expectedClassification != tool.Classification ||
                       tool.Classification == ToolRoutingClassification.Blocked;
            }).ToArray();

        if (schemaDiagnostics.Length > 0)
        {
            Console.WriteLine("Input schemas requiring review");
            Console.WriteLine("------------------------------");
            foreach (var tool in schemaDiagnostics)
            {
                Console.WriteLine($"{tool.Name}: {tool.InputSchema.GetRawText()}");
            }

            Console.WriteLine();
        }
    }

    private static ToolRoutingClassification? GetExpectedClassification(string toolName)
    {
        if (AutomaticallyRoutedTools.Contains(toolName, StringComparer.Ordinal))
            return ToolRoutingClassification.AutomaticallyRouted;

        if (GlobalAllowlistedTools.Contains(toolName, StringComparer.Ordinal))
            return ToolRoutingClassification.GlobalAllowlisted;

        if (ExplicitAdapterTools.Contains(toolName, StringComparer.Ordinal))
            return ToolRoutingClassification.ExplicitAdapter;

        if (ProjectLifecycleTools.Contains(toolName, StringComparer.Ordinal))
            return ToolRoutingClassification.ProjectLifecycle;

        if (IntentionallyBlockedTools.Contains(toolName, StringComparer.Ordinal))
            return ToolRoutingClassification.IntentionallyBlocked;

        return null;
    }

    private static void WriteProxySnapshot(IReadOnlyList<ClassifiedTool> classifications, IReadOnlyList<McpClientTool> proxyTools)
    {
        var upstreamToolNames = classifications
                .Select(tool => tool.Name)
                .ToArray();

        var proxyToolNames = proxyTools
                .Select(tool => tool.Name)
                .ToArray();

        var upstreamOnly = upstreamToolNames
                .Except(proxyToolNames, StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

        var proxyOnly = proxyToolNames
                .Except(upstreamToolNames, StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

        var expectedProxyToolNames = classifications
                .Where(tool => tool.Classification is ToolRoutingClassification.AutomaticallyRouted or ToolRoutingClassification.GlobalAllowlisted)
                .Select(tool => tool.Name)
                .ToArray();

        var expectedProxyToolsMissing = expectedProxyToolNames
                .Except(proxyToolNames, StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

        Console.WriteLine("ProjectMemoryProxy exposure snapshot");
        Console.WriteLine("====================================");
        Console.WriteLine($"Proxy tools: {proxyToolNames.Length}");

        foreach (var toolName in proxyToolNames.OrderBy(name => name, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {toolName}");
        }

        Console.WriteLine($"Upstream-only tools: {upstreamOnly.Length}");
        foreach (var toolName in upstreamOnly)
        {
            Console.WriteLine($"  {toolName}");
        }

        Console.WriteLine($"Proxy-only tools: {proxyOnly.Length}");
        foreach (var toolName in proxyOnly)
        {
            Console.WriteLine($"  {toolName}");
        }

        if (expectedProxyToolsMissing.Length > 0)
        {
            Console.WriteLine("Input schemas for policy-exposed tools missing from proxy tools/list");
            Console.WriteLine("--------------------------------------------------------------");
            foreach (var toolName in expectedProxyToolsMissing)
            {
                var tool = classifications.Single(classification => classification.Name == toolName);
                Console.WriteLine($"{tool.Name}: {tool.InputSchema.GetRawText()}");
            }
        }

        if (proxyOnly.Length > 0)
        {
            Console.WriteLine("Public input schemas for proxy-only tools");
            Console.WriteLine("-----------------------------------------");
            foreach (var toolName in proxyOnly)
            {
                var tool = proxyTools.Single(proxyTool => proxyTool.Name == toolName);
                Console.WriteLine($"{tool.Name}: " + $"{tool.ProtocolTool.InputSchema.GetRawText()}");
            }
        }

        Console.WriteLine();
    }

    private static string FormatNames(IEnumerable<string> names)
    {
        var values = names.ToArray();
        return values.Length == 0 ? "<none>" : string.Join(", ", values);
    }

    #endregion

    #region Test Classes

    private sealed record ClassifiedTool(string Name, ToolRoutingClassification Classification, RoutingSchemaAnalysis? RoutingAnalysis, JsonElement InputSchema);

    private sealed class StubProjectRegistry : IProjectRegistry
    {
        #region Private Fields

        private readonly ProjectRoute? _route;

        #endregion

        #region Constructors

        public StubProjectRegistry(ProjectRoute? route)
        {
            _route = route;
        }

        #endregion

        #region Public Methods

        public Task<ProjectRoute?> FindRouteAsync(ContextId contextId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_route);
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

        #endregion
    }

    #endregion
}
