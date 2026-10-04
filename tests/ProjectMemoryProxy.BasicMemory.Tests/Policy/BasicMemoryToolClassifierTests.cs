namespace ProjectMemoryProxy.BasicMemory.Tests.Policy;

using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectMemoryProxy.BasicMemory.Policy;

/// <summary>
/// Tests for <see cref="BasicMemoryToolClassifier"/>
/// </summary>
[TestClass]
public sealed class BasicMemoryToolClassifierTests
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
    /// Verifies that a newly discovered tool with a supported project selector is automatically routed without requiring a tool-name allowlist entry.
    /// </summary>
    [TestMethod]
    public void ClassifyAutomaticallyRoutesFutureToolBySchema()
    {
        var classifier = new BasicMemoryToolClassifier();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "pattern": { "type": "string" },
                "project_id": { "type": ["string", "null"] }
              }
            }
            """);

        var result = classifier.Classify("grep", schema);
        Assert.AreEqual(ToolRoutingClassification.AutomaticallyRouted, result);
    }

    /// <summary>
    /// Verifies that an existing Basic Memory tool remains automatically routable when its schema exposes supported local project routing.
    /// </summary>
    [TestMethod]
    public void ClassifyAutomaticallyRoutesExistingToolBySchema()
    {
        var classifier = new BasicMemoryToolClassifier();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "query": { "type": "string" },
                "project": { "type": ["string", "null"] },
                "project_id": { "type": ["string", "null"] },
                "search_all_projects": { "type": "boolean" }
              }
            }
            """);

        var result = classifier.Classify("search_notes", schema);
        Assert.AreEqual(ToolRoutingClassification.AutomaticallyRouted, result);
    }

    /// <summary>
    /// Verifies that a newly discovered tool without a supported project selector is blocked by the generic routing path.
    /// </summary>
    [TestMethod]
    public void ClassifyBlocksFutureToolWithoutProjectSelector()
    {
        var classifier = new BasicMemoryToolClassifier();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "query": { "type": "string" }
              }
            }
            """);

        var result = classifier.Classify("new_basic_memory_tool", schema);
        Assert.AreEqual(ToolRoutingClassification.Blocked, result);
    }

    /// <summary>
    /// Verifies that a tool is blocked when its schema contains routing semantics that cannot be safely controlled by the generic proxy path.
    /// </summary>
    [TestMethod]
    public void ClassifyBlocksToolWithUnsafeRoutingSchema()
    {
        var classifier = new BasicMemoryToolClassifier();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "project_id": { "type": "string" },
                "options": {
                  "type": "object",
                  "properties": {
                    "tenant_id": { "type": "string" }
                  }
                }
              }
            }
            """);

        var result = classifier.Classify("new_basic_memory_tool", schema);
        Assert.AreEqual(ToolRoutingClassification.Blocked, result);
    }

    /// <summary>
    /// Verifies that compatibility tools requiring dedicated proxy behavior are classified as explicit adapters.
    /// </summary>
    [TestMethod]
    [DataRow("some_placeholder_tool")]
    public void ClassifyMarksCompatibilityToolsAsExplicitAdapters(string toolName)
    {
        var classifier = new BasicMemoryToolClassifier();

        var result = classifier.Classify(toolName, ParseSchema("""{"type":"object"}"""));
        Assert.AreEqual(ToolRoutingClassification.ExplicitAdapter, result);
    }

    /// <summary>
    /// Verifies that local Basic Memory project-management tools are classified as project lifecycle operations.
    /// </summary>
    [TestMethod]
    [DataRow("create_memory_project")]
    [DataRow("delete_project")]
    [DataRow("list_memory_projects")]
    public void ClassifyMarksProjectManagementAsLifecycle(string toolName)
    {
        var classifier = new BasicMemoryToolClassifier();

        var result = classifier.Classify(toolName, ParseSchema("""{"type":"object"}"""));
        Assert.AreEqual(ToolRoutingClassification.ProjectLifecycle, result);
    }

    /// <summary>
    /// Verifies that cloud workspace discovery is intentionally blocked because workspace routing is outside the supported local proxy scope.
    /// </summary>
    [TestMethod]
    [DataRow("search")]
    [DataRow("fetch")]
    [DataRow("list_workspaces")]
    public void ClassifyMarksWorkspaceDiscoveryAsIntentionallyBlocked(string toolName)
    {
        var classifier = new BasicMemoryToolClassifier();

        var result = classifier.Classify(toolName, ParseSchema("""{"type":"object"}"""));
        Assert.AreEqual(ToolRoutingClassification.IntentionallyBlocked, result);
    }

    /// <summary>
    /// Verifies that special tool names use exact ordinal casing before falling back to generic schema-driven classification.
    /// </summary>
    [TestMethod]
    public void ClassifyUsesExactToolNameForSpecialClassification()
    {
        var classifier = new BasicMemoryToolClassifier();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "project_id": { "type": "string" }
              }
            }
            """);

        var result = classifier.Classify("Search", schema);
        Assert.AreEqual(ToolRoutingClassification.AutomaticallyRouted, result);
    }

    /// <summary>
    /// Verifies that Basic Memory diagnostics are explicitly approved as a global operation.
    /// </summary>
    [TestMethod]
    public void ClassifyAllowsDiagnosticsAsGlobalOperation()
    {
        var classifier = new BasicMemoryToolClassifier();

        var result = classifier.Classify("basic_memory_diagnostics", ParseSchema("""{"type":"object","properties":{}}"""));
        Assert.AreEqual(ToolRoutingClassification.GlobalAllowlisted, result);
    }

    #endregion

    #region Private Methods

    private static JsonElement ParseSchema(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    #endregion

    #region Test Classes

    #endregion
}
