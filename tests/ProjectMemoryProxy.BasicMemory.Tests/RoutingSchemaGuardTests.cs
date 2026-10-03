namespace ProjectMemoryProxy.BasicMemory.Tests;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

/// <summary>
/// Tests for <see cref="RoutingSchemaGuard"/>
/// </summary>
[TestClass]
public sealed class RoutingSchemaGuardTests
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
    /// Verifies that a simple schema without routing selectors matches an empty routing contract.
    /// </summary>
    [TestMethod]
    public void AnalyzeAcceptsSafeSimpleSchema()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "query": { "type": "string" }
              }
            }
            """);

        var result = guard.Analyze(schema, []);
        Assert.AreEqual(RoutingSchemaGuardStatus.Compatible, result);
    }

    /// <summary>
    /// Verifies that approved top-level routing selectors match the expected routing contract.
    /// </summary>
    [TestMethod]
    public void AnalyzeAcceptsExpectedTopLevelSelectors()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "project": { "type": ["string", "null"] },
                "project_id": { "type": ["string", "null"] },
                "query": { "type": "string" }
              }
            }
            """);

        var result = guard.Analyze(schema, ["project", "project_id"]);
        Assert.AreEqual(RoutingSchemaGuardStatus.Compatible, result);
    }

    /// <summary>
    /// Verifies that a routing selector inside a nested object fails closed.
    /// </summary>
    [TestMethod]
    public void AnalyzeRejectsNestedRoutingSelector()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "options": {
                  "type": "object",
                  "properties": {
                    "project_id": { "type": "string" }
                  }
                }
              }
            }
            """);

        var result = guard.Analyze(schema, []);
        Assert.AreEqual(RoutingSchemaGuardStatus.UnexpectedRoutingSelector, result);
    }

    /// <summary>
    /// Verifies that a routing selector inside an array item fails closed.
    /// </summary>
    [TestMethod]
    public void AnalyzeRejectsRoutingSelectorInsideArrayItem()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "items": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "workspace": { "type": "string" }
                    }
                  }
                }
              }
            }
            """);

        var result = guard.Analyze(schema, []);
        Assert.AreEqual(RoutingSchemaGuardStatus.UnexpectedRoutingSelector, result);
    }

    /// <summary>
    /// Verifies that routing selectors declared through <c>oneOf</c> are analyzed.
    /// </summary>
    [TestMethod]
    public void AnalyzeTraversesOneOf()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "oneOf": [
                {
                  "type": "object",
                  "properties": {
                    "project_id": { "type": "string" }
                  }
                }
              ]
            }
            """);

        var result = guard.Analyze(schema, ["project_id"]);
        Assert.AreEqual(RoutingSchemaGuardStatus.Compatible, result);
    }

    /// <summary>
    /// Verifies that routing selectors declared through <c>anyOf</c> are analyzed.
    /// </summary>
    [TestMethod]
    public void AnalyzeTraversesAnyOf()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "anyOf": [
                {
                  "type": "object",
                  "properties": {
                    "project_id": { "type": "string" }
                  }
                }
              ]
            }
            """);

        var result = guard.Analyze(schema, ["project_id"]);
        Assert.AreEqual(RoutingSchemaGuardStatus.Compatible, result);
    }

    /// <summary>
    /// Verifies that routing selectors declared through <c>allOf</c> are analyzed.
    /// </summary>
    [TestMethod]
    public void AnalyzeTraversesAllOf()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "allOf": [
                {
                  "type": "object",
                  "properties": {
                    "project_id": { "type": "string" }
                  }
                }
              ]
            }
            """);

        var result = guard.Analyze(schema, ["project_id"]);
        Assert.AreEqual(RoutingSchemaGuardStatus.Compatible, result);
    }

    /// <summary>
    /// Verifies that a local JSON Schema reference is resolved before routing analysis.
    /// </summary>
    [TestMethod]
    public void AnalyzeResolvesLocalReference()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "$ref": "#/$defs/input",
              "$defs": {
                "input": {
                  "type": "object",
                  "properties": {
                    "project": { "type": "string" },
                    "project_id": { "type": "string" }
                  }
                }
              }
            }
            """);

        var result = guard.Analyze(schema, ["project", "project_id"]);
        Assert.AreEqual(RoutingSchemaGuardStatus.Compatible, result);
    }

    /// <summary>
    /// Verifies that an external JSON Schema reference fails closed.
    /// </summary>
    [TestMethod]
    public void AnalyzeRejectsExternalReference()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "$ref": "https://example.invalid/schema.json"
            }
            """);

        var result = guard.Analyze(schema, []);
        Assert.AreEqual(RoutingSchemaGuardStatus.UnsupportedSchema, result);
    }

    /// <summary>
    /// Verifies that a cyclic local JSON Schema reference fails closed.
    /// </summary>
    [TestMethod]
    public void AnalyzeRejectsCyclicReference()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "$ref": "#/$defs/input",
              "$defs": {
                "input": {
                  "$ref": "#/$defs/input"
                }
              }
            }
            """);

        var result = guard.Analyze(schema, []);
        Assert.AreEqual(RoutingSchemaGuardStatus.UnsupportedSchema, result);
    }

    /// <summary>
    /// Verifies that malformed <c>properties</c> schema content fails closed.
    /// </summary>
    [TestMethod]
    public void AnalyzeRejectsMalformedProperties()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": []
            }
            """);

        var result = guard.Analyze(schema, []);
        Assert.AreEqual(RoutingSchemaGuardStatus.UnsupportedSchema, result);
    }

    /// <summary>
    /// Verifies that similarly named non-routing properties do not match routing selectors.
    /// </summary>
    [TestMethod]
    public void AnalyzeAllowsSafeSimilarlyNamedProperties()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "project_name": { "type": "string" },
                "workspace_label": { "type": "string" },
                "tenant_note": { "type": "string" }
              }
            }
            """);

        var result = guard.Analyze(schema, []);
        Assert.AreEqual(RoutingSchemaGuardStatus.Compatible, result);
    }

    /// <summary>
    /// Verifies that a case or separator variant of a routing selector fails closed.
    /// </summary>
    [TestMethod]
    public void AnalyzeRejectsRoutingSelectorAlias()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "project_id": { "type": "string" },
                "ProjectId": { "type": "string" }
              }
            }
            """);

        var result = guard.Analyze(schema, ["project_id"]);
        Assert.AreEqual(RoutingSchemaGuardStatus.UnexpectedRoutingSelector, result);
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
