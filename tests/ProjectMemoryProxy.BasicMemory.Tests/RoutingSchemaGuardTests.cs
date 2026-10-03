namespace ProjectMemoryProxy.BasicMemory.Tests;

using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
    /// Verifies that a schema without a project selector is not considered automatically routable.
    /// </summary>
    [TestMethod]
    public void AnalyzeRejectsSchemaWithoutProjectSelector()
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

        var result = guard.Analyze(schema);
        Assert.AreEqual(RoutingSchemaStatus.NoProjectSelector, result.Status);
        Assert.AreEqual(ProjectSelectorKind.None, result.ProjectSelector);
        Assert.IsFalse(result.IsAutomaticallyRoutable);
    }

    /// <summary>
    /// Verifies that a top-level <c>project_id</c> selector uses the canonical project identifier routing path.
    /// </summary>
    [TestMethod]
    public void AnalyzeRoutesProjectId()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "query": { "type": "string" },
                "project_id": { "type": ["string", "null"] }
              }
            }
            """);

        var result = guard.Analyze(schema);
        Assert.AreEqual(RoutingSchemaStatus.AutomaticallyRoutable, result.Status);
        Assert.AreEqual(ProjectSelectorKind.ProjectId, result.ProjectSelector);
        Assert.IsFalse(result.InjectProjectName);
        CollectionAssert.AreEqual(new[] { "project_id" }, result.SuppressedProperties.ToArray());
    }

    /// <summary>
    /// Verifies that a tool exposing only <c>project</c> can be routed with the validated local project name.
    /// </summary>
    [TestMethod]
    public void AnalyzeRoutesProjectName()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "query": { "type": "string" },
                "project": { "type": ["string", "null"] }
              }
            }
            """);

        var result = guard.Analyze(schema);
        Assert.AreEqual(RoutingSchemaStatus.AutomaticallyRoutable, result.Status);
        Assert.AreEqual(ProjectSelectorKind.ProjectName, result.ProjectSelector);
        Assert.IsTrue(result.InjectProjectName);
        CollectionAssert.AreEqual(new[] { "project" }, result.SuppressedProperties.ToArray());
    }

    /// <summary>
    /// Verifies that <c>project_id</c> takes precedence when both supported local project selectors are available.
    /// </summary>
    [TestMethod]
    public void AnalyzePrefersProjectId()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "project": { "type": ["string", "null"] },
                "project_id": { "type": ["string", "null"] }
              }
            }
            """);

        var result = guard.Analyze(schema);
        Assert.AreEqual(RoutingSchemaStatus.AutomaticallyRoutable, result.Status);
        Assert.AreEqual(ProjectSelectorKind.ProjectId, result.ProjectSelector);
        Assert.IsFalse(result.InjectProjectName);
        CollectionAssert.AreEqual(new[] { "project", "project_id" }, result.SuppressedProperties.ToArray());
    }

    /// <summary>
    /// Verifies that a required <c>project</c> selector is injected alongside <c>project_id</c> when the upstream schema requires both.
    /// </summary>
    [TestMethod]
    public void AnalyzeRequiresProjectNameWhenProjectIsRequired()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "project": { "type": "string" },
                "project_id": { "type": "string" }
              },
              "required": [
                "project"
              ]
            }
            """);

        var result = guard.Analyze(schema);
        Assert.AreEqual(RoutingSchemaStatus.AutomaticallyRoutable, result.Status);
        Assert.AreEqual(ProjectSelectorKind.ProjectId, result.ProjectSelector);
        Assert.IsTrue(result.InjectProjectName);
    }

    /// <summary>
    /// Verifies that optional cloud and cross-project selectors are suppressed without blocking an otherwise locally routable tool.
    /// </summary>
    [TestMethod]
    public void AnalyzeSuppressesOptionalUnsupportedSelectors()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "query": { "type": "string" },
                "project_id": { "type": "string" },
                "workspace": { "type": ["string", "null"] },
                "tenant_id": { "type": ["string", "null"] },
                "search_all_projects": { "type": "boolean" }
              }
            }
            """);

        var result = guard.Analyze(schema);
        Assert.AreEqual(RoutingSchemaStatus.AutomaticallyRoutable, result.Status);
        Assert.AreEqual(ProjectSelectorKind.ProjectId, result.ProjectSelector);
        CollectionAssert.AreEqual(
            new[]
            {
                "project_id",
                "search_all_projects",
                "tenant_id",
                "workspace"
            },
            result.SuppressedProperties.ToArray());
    }

    /// <summary>
    /// Verifies that required workspace routing semantics fail closed because cloud workspaces are outside the supported proxy scope.
    /// </summary>
    [TestMethod]
    public void AnalyzeRejectsRequiredWorkspaceSelector()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "project_id": { "type": "string" },
                "workspace": { "type": "string" }
              },
              "required": [
                "workspace"
              ]
            }
            """);

        var result = guard.Analyze(schema);
        Assert.AreEqual(RoutingSchemaStatus.RequiredUnsupportedSelector, result.Status);
        Assert.IsFalse(result.IsAutomaticallyRoutable);
    }

    /// <summary>
    /// Verifies that routing selectors inside nested objects fail closed.
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
                "project_id": { "type": "string" },
                "options": {
                  "type": "object",
                  "properties": {
                    "workspace": { "type": "string" }
                  }
                }
              }
            }
            """);

        var result = guard.Analyze(schema);
        Assert.AreEqual(RoutingSchemaStatus.NestedRoutingSelector, result.Status);
    }

    /// <summary>
    /// Verifies that routing selectors inside array items fail closed.
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
                "project_id": { "type": "string" },
                "entries": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "project": { "type": "string" }
                    }
                  }
                }
              }
            }
            """);

        var result = guard.Analyze(schema);
        Assert.AreEqual(RoutingSchemaStatus.NestedRoutingSelector, result.Status);
    }

    /// <summary>
    /// Verifies that alternative project-routing semantics expressed through <c>oneOf</c> fail closed.
    /// </summary>
    [TestMethod]
    public void AnalyzeRejectsRoutingSelectorInsideOneOf()
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
                },
                {
                  "type": "object",
                  "properties": {
                    "project": { "type": "string" }
                  }
                }
              ]
            }
            """);

        var result = guard.Analyze(schema);
        Assert.AreEqual(RoutingSchemaStatus.AlternativeRoutingSelector, result.Status);
    }

    /// <summary>
    /// Verifies that non-alternative composition through <c>allOf</c> may provide a supported local project selector.
    /// </summary>
    [TestMethod]
    public void AnalyzeRoutesProjectIdThroughAllOf()
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

        var result = guard.Analyze(schema);
        Assert.AreEqual(RoutingSchemaStatus.AutomaticallyRoutable, result.Status);
        Assert.AreEqual(ProjectSelectorKind.ProjectId, result.ProjectSelector);
    }

    /// <summary>
    /// Verifies that local JSON Schema references are resolved before routing analysis.
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
                    "project_id": { "type": "string" }
                  }
                }
              }
            }
            """);

        var result = guard.Analyze(schema);
        Assert.AreEqual(RoutingSchemaStatus.AutomaticallyRoutable, result.Status);
        Assert.AreEqual(ProjectSelectorKind.ProjectId, result.ProjectSelector);
    }

    /// <summary>
    /// Verifies that external JSON Schema references fail closed.
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

        var result = guard.Analyze(schema);
        Assert.AreEqual(RoutingSchemaStatus.UnsupportedSchema, result.Status);
    }

    /// <summary>
    /// Verifies that cyclic local JSON Schema references fail closed.
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

        var result = guard.Analyze(schema);
        Assert.AreEqual(RoutingSchemaStatus.UnsupportedSchema, result.Status);
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

        var result = guard.Analyze(schema);
        Assert.AreEqual(RoutingSchemaStatus.UnsupportedSchema, result.Status);
    }

    /// <summary>
    /// Verifies that unknown routing-like aliases fail closed instead of being interpreted as supported selectors.
    /// </summary>
    [TestMethod]
    public void AnalyzeRejectsUnknownRoutingSelectorAlias()
    {
        var guard = new RoutingSchemaGuard();
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "project_id": { "type": "string" },
                "project_name": { "type": "string" }
              }
            }
            """);

        var result = guard.Analyze(schema);
        Assert.AreEqual(RoutingSchemaStatus.UnknownRoutingSelector, result.Status);
    }

    /// <summary>
    /// Verifies that case variants of canonical routing selectors fail closed.
    /// </summary>
    [TestMethod]
    public void AnalyzeRejectsRoutingSelectorCaseAlias()
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

        var result = guard.Analyze(schema);
        Assert.AreEqual(RoutingSchemaStatus.UnknownRoutingSelector, result.Status);
    }

    /// <summary>
    /// Verifies that alternative project-routing semantics expressed through <c>anyOf</c> fail closed.
    /// </summary>
    [TestMethod]
    public void AnalyzeRejectsRoutingSelectorInsideAnyOf()
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
                },
                {
                  "type": "object",
                  "properties": {
                    "query": { "type": "string" }
                  }
                }
              ]
            }
            """);

        var result = guard.Analyze(schema);
        Assert.AreEqual(RoutingSchemaStatus.AlternativeRoutingSelector, result.Status);
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
