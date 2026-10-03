namespace ProjectMemoryProxy.BasicMemory.Tests;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

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
    /// Verifies that a known project-routed tool with its approved schema is automatically routable.
    /// </summary>
    [TestMethod]
    public void ClassifyAcceptsKnownAutomaticallyRoutedTool()
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
    /// Verifies that a known project-routed tool fails closed when its routing schema changes unexpectedly.
    /// </summary>
    [TestMethod]
    public void ClassifyBlocksKnownToolWithUnexpectedRoutingSchema()
    {
        var classifier = new BasicMemoryToolClassifier();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "project": { "type": "string" },
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

        var result = classifier.Classify("read_note", schema);
        Assert.AreEqual(ToolRoutingClassification.Blocked, result);
    }

    /// <summary>
    /// Verifies that the compatibility search tool requires an explicit adapter.
    /// </summary>
    [TestMethod]
    public void ClassifyMarksSearchAsExplicitAdapter()
    {
        var classifier = new BasicMemoryToolClassifier();
        var result = classifier.Classify("search", ParseSchema("""{"type":"object"}"""));
        Assert.AreEqual(ToolRoutingClassification.ExplicitAdapter, result);
    }

    /// <summary>
    /// Verifies that project-management tools are classified as project lifecycle operations.
    /// </summary>
    [TestMethod]
    public void ClassifyMarksProjectManagementAsLifecycle()
    {
        var classifier = new BasicMemoryToolClassifier();
        var result = classifier.Classify("create_memory_project", ParseSchema("""{"type":"object"}"""));
        Assert.AreEqual(ToolRoutingClassification.ProjectLifecycle, result);
    }

    /// <summary>
    /// Verifies that an unknown upstream tool is blocked even when its schema appears harmless.
    /// </summary>
    [TestMethod]
    public void ClassifyBlocksUnknownTool()
    {
        var classifier = new BasicMemoryToolClassifier();
        var result = classifier.Classify("new_basic_memory_tool", ParseSchema("""{"type":"object","properties":{"query":{"type":"string"}}}"""));
        Assert.AreEqual(ToolRoutingClassification.Blocked, result);
    }

    /// <summary>
    /// Verifies that upstream tool names are matched using exact ordinal casing.
    /// </summary>
    [TestMethod]
    public void ClassifyUsesExactToolName()
    {
        var classifier = new BasicMemoryToolClassifier();
        var result = classifier.Classify("Search_Notes", ParseSchema("""{"type":"object"}"""));
        Assert.AreEqual(ToolRoutingClassification.Blocked, result);
    }

    /// <summary>
    /// Verifies that diagnostics remain blocked until explicitly placed on the global allowlist.
    /// </summary>
    [TestMethod]
    public void ClassifyBlocksDiagnosticsByDefault()
    {
        var classifier = new BasicMemoryToolClassifier();
        var result = classifier.Classify("basic_memory_diagnostics", ParseSchema("""{"type":"object"}"""));
        Assert.AreEqual(ToolRoutingClassification.Blocked, result);
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
