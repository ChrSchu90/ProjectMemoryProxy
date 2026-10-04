namespace ProjectMemoryProxy.BasicMemory.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Protocol;
using ProjectMemoryProxy.Core.Configuration;

/// <summary>
/// Live contract tests for the supported Basic Memory upstream version.
/// </summary>
[TestClass]
public sealed class BasicMemoryContractTests
{
    #region Static Fields

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
        "fetch",
        "search"
    ];

    private static readonly string[] ProjectLifecycleTools =
    [
        "create_memory_project",
        "delete_project",
        "list_memory_projects"
    ];

    private static readonly string[] IntentionallyBlockedTools =
    [
        "list_workspaces"
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
    /// Verifies that a configured live Basic Memory endpoint matches the canonical tool, routing, exposure, and diagnostics contract expected by the proxy.
    /// </summary>
    [TestMethod]
    public async Task LiveBasicMemoryEndpointMatchesExpectedContract()
    {
        var endpoint = GetTestBasicMemoryEndpointOrSkipTest();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellationToken = cancellationTokenSource.Token;
        var options = Options.Create(new ProjectMemoryProxyOptions
        {
            BasicMemoryEndpoint = endpoint,
            BasicMemoryConnectionTimeout = TimeSpan.FromSeconds(10)
        });

        await using var client = new BasicMemoryClient(options, NullLoggerFactory.Instance, NullLogger<BasicMemoryClient>.Instance);
        using var upstreamCatalog = new BasicMemoryToolCatalog(client, NullLogger<BasicMemoryToolCatalog>.Instance);
        using var mirroredCatalog = new BasicMemoryMirroredToolCatalog(upstreamCatalog, NullLogger<BasicMemoryMirroredToolCatalog>.Instance);

        await upstreamCatalog.InitializeAsync(cancellationToken);
        Assert.HasCount(21, upstreamCatalog.Tools);
        AssertToolNames(GetExpectedUpstreamToolNames(), upstreamCatalog.Tools.Select(tool => tool.ProtocolTool.Name));

        var classifier = new BasicMemoryToolClassifier();
        var classifications = upstreamCatalog.Tools.Select(tool => new { tool.ProtocolTool.Name, Classification = classifier.Classify(tool.ProtocolTool.Name, tool.ProtocolTool.InputSchema) }).ToArray();
        AssertClassification(AutomaticallyRoutedTools, classifications.Where(tool => tool.Classification == ToolRoutingClassification.AutomaticallyRouted).Select(tool => tool.Name));
        AssertClassification(GlobalAllowlistedTools, classifications.Where(tool => tool.Classification == ToolRoutingClassification.GlobalAllowlisted).Select(tool => tool.Name));
        AssertClassification(ExplicitAdapterTools, classifications.Where(tool => tool.Classification == ToolRoutingClassification.ExplicitAdapter).Select(tool => tool.Name));
        AssertClassification(ProjectLifecycleTools, classifications.Where(tool => tool.Classification == ToolRoutingClassification.ProjectLifecycle).Select(tool => tool.Name));
        AssertClassification(IntentionallyBlockedTools, classifications.Where(tool => tool.Classification == ToolRoutingClassification.IntentionallyBlocked).Select(tool => tool.Name));
        AssertClassification(Array.Empty<string>(), classifications.Where(tool => tool.Classification == ToolRoutingClassification.Blocked).Select(tool => tool.Name));

        await mirroredCatalog.InitializeAsync(cancellationToken);
        Assert.HasCount(15, mirroredCatalog.Tools);
        AssertToolNames(AutomaticallyRoutedTools.Concat(GlobalAllowlistedTools), mirroredCatalog.Tools.Select(tool => tool.Name));

        var automaticallyRouted = mirroredCatalog.Tools.Where(tool => tool.Classification == ToolRoutingClassification.AutomaticallyRouted).ToArray();
        Assert.HasCount(14, automaticallyRouted);
        AssertToolNames(AutomaticallyRoutedTools, automaticallyRouted.Select(tool => tool.Name));

        foreach (var tool in automaticallyRouted)
        {
            AssertAutomaticallyRoutedPublicSchema(tool.Name, tool.PublicInputSchema);
        }

        var diagnostics = mirroredCatalog.Tools.Single(tool => tool.Name == "basic_memory_diagnostics");
        Assert.AreEqual(ToolRoutingClassification.GlobalAllowlisted, diagnostics.Classification);
        Assert.IsFalse(diagnostics.PublicInputSchema.GetProperty("properties").TryGetProperty("context_id", out _));

        var diagnosticsResult = await diagnostics.UpstreamTool.CallAsync(new Dictionary<string, object?>(), cancellationToken: cancellationToken);
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

    private static void AssertClassification(IEnumerable<string> expected, IEnumerable<string> actual)
    {
        AssertToolNames(expected, actual);
    }

    private static void AssertToolNames(IEnumerable<string> expected, IEnumerable<string> actual)
    {
        var expectedNames = expected.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var actualNames = actual.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(expectedNames, actualNames);
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

    #endregion

    #region Test Classes

    #endregion
}
