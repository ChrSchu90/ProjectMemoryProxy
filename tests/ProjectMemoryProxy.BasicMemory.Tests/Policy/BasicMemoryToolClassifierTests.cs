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
    /// Verifies that the Basic Memory 1.0 search schema is automatically routable when cross-project selectors are optional.
    /// </summary>
    [TestMethod]
    public void ClassifyAutomaticallyRoutesSearchWithOptionalCrossProjectSelectors()
    {
        var classifier = new BasicMemoryToolClassifier();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "query": { "type": ["string", "null"] },
                "project": { "type": ["string", "null"] },
                "projects": { "type": ["array", "null"] },
                "all_projects": { "type": "boolean" }
              }
            }
            """);

        Assert.AreEqual(ToolRoutingClassification.AutomaticallyRouted, classifier.Classify("bm_search", schema));
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
    [DataRow("bm_project")]
    [DataRow("bm_project_delete")]
    public void ClassifyMarksProjectManagementAsLifecycle(string toolName)
    {
        var classifier = new BasicMemoryToolClassifier();

        var result = classifier.Classify(toolName, ParseSchema("""{"type":"object"}"""));
        Assert.AreEqual(ToolRoutingClassification.ProjectLifecycle, result);
    }

    /// <summary>
    /// Verifies that the consolidated Basic Memory 1.0 project tool cannot bypass PMP lifecycle policy even though it has a project selector.
    /// </summary>
    [TestMethod]
    public void ClassifyHidesProjectManagementWithRoutableSchema()
    {
        var classifier = new BasicMemoryToolClassifier();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "command": { "enum": ["list", "create", "recent"], "type": "string" },
                "project": { "type": ["string", "null"] }
              }
            }
            """);

        Assert.AreEqual(ToolRoutingClassification.ProjectLifecycle, classifier.Classify("bm_project", schema));
    }

    /// <summary>
    /// Verifies that unscoped compatibility and hosted-agent tool names stay blocked if an upstream release exposes them.
    /// </summary>
    [TestMethod]
    [DataRow("bm_agent")]
    [DataRow("fetch")]
    [DataRow("search")]
    [DataRow("list_workspaces")]
    public void ClassifyKeepsUnscopedAndHostedToolsBlocked(string toolName)
    {
        var classifier = new BasicMemoryToolClassifier();
        var schema = ParseSchema("""{"type":"object","properties":{"project":{"type":"string"}}}""");

        Assert.AreEqual(ToolRoutingClassification.IntentionallyBlocked, classifier.Classify(toolName, schema));
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

        var result = classifier.Classify("BM_PROJECT", schema);
        Assert.AreEqual(ToolRoutingClassification.AutomaticallyRouted, result);
    }

    /// <summary>
    /// Verifies that the Basic Memory manual stays hidden even if a later upstream version adds a routable project selector.
    /// </summary>
    [TestMethod]
    public void ClassifyIntentionallyBlocksManualWithOrWithoutProjectSelector()
    {
        var classifier = new BasicMemoryToolClassifier();
        var noProjectSchema = ParseSchema("""{"type":"object","properties":{}}""");
        var projectSchema = ParseSchema("""{"type":"object","properties":{"project":{"type":"string"}}}""");

        Assert.AreEqual(ToolRoutingClassification.IntentionallyBlocked, classifier.Classify("bm_man", noProjectSchema));
        Assert.AreEqual(ToolRoutingClassification.IntentionallyBlocked, classifier.Classify("bm_man", projectSchema));
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
