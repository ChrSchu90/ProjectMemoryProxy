namespace ProjectMemoryProxy.BasicMemory.Tests;

using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="PublicToolInputSchemaRewriter"/>
/// </summary>
[TestClass]
public sealed class PublicToolInputSchemaRewriterTests
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
    /// Verifies that routing selectors are removed and <c>context_id</c> is added to a supported public tool schema.
    /// </summary>
    [TestMethod]
    public void TryRewriteReplacesRoutingSelectorsWithContextId()
    {
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "query": {
                  "type": "string"
                },
                "project": {
                  "type": ["string", "null"]
                },
                "project_id": {
                  "type": ["string", "null"]
                },
                "workspace": {
                  "type": ["string", "null"]
                },
                "search_all_projects": {
                  "type": "boolean"
                }
              },
              "required": [
                "query"
              ]
            }
            """);

        var analysis = new RoutingSchemaGuard().Analyze(schema);
        var rewriter = new PublicToolInputSchemaRewriter();
        Assert.IsTrue(rewriter.TryRewrite(schema, analysis, out var publicSchema));

        var properties = publicSchema.GetProperty("properties");
        Assert.IsTrue(properties.TryGetProperty("query", out _));
        Assert.IsTrue(properties.TryGetProperty("context_id", out var contextId));
        Assert.AreEqual("string", contextId.GetProperty("type").GetString());
        Assert.IsFalse(properties.TryGetProperty("project", out _));
        Assert.IsFalse(properties.TryGetProperty("project_id", out _));
        Assert.IsFalse(properties.TryGetProperty("workspace", out _));
        Assert.IsFalse(properties.TryGetProperty("search_all_projects", out _));

        var required = publicSchema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ToArray();
        CollectionAssert.AreEqual(new[] { "query", "context_id" }, required);
        Assert.IsFalse(publicSchema.GetProperty("additionalProperties").GetBoolean());
    }

    /// <summary>
    /// Verifies that required upstream routing selectors are removed from the public required-property set.
    /// </summary>
    [TestMethod]
    public void TryRewriteRemovesRequiredRoutingSelectors()
    {
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "query": {
                  "type": "string"
                },
                "project": {
                  "type": "string"
                },
                "project_id": {
                  "type": "string"
                }
              },
              "required": [
                "query",
                "project",
                "project_id"
              ]
            }
            """);

        var analysis = new RoutingSchemaGuard().Analyze(schema);
        var rewriter = new PublicToolInputSchemaRewriter();
        Assert.IsTrue(rewriter.TryRewrite(schema, analysis, out var publicSchema));

        var required = publicSchema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ToArray();
        CollectionAssert.AreEqual(new[] { "query", "context_id" }, required);
    }

    /// <summary>
    /// Verifies that nested non-routing argument schemas are preserved unchanged.
    /// </summary>
    [TestMethod]
    public void TryRewritePreservesNestedArgumentSchema()
    {
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "project_id": {
                  "type": "string"
                },
                "metadata": {
                  "type": "object",
                  "properties": {
                    "status": {
                      "type": "string"
                    }
                  },
                  "required": [
                    "status"
                  ],
                  "additionalProperties": false
                }
              }
            }
            """);

        var analysis = new RoutingSchemaGuard().Analyze(schema);
        var rewriter = new PublicToolInputSchemaRewriter();
        Assert.IsTrue(rewriter.TryRewrite(schema, analysis, out var publicSchema));

        var metadata = publicSchema.GetProperty("properties").GetProperty("metadata");
        Assert.AreEqual(
            "string",
            metadata
                .GetProperty("properties")
                .GetProperty("status")
                .GetProperty("type")
                .GetString());

        CollectionAssert.AreEqual(
            new[]
            {
                "status"
            },
            metadata
                .GetProperty("required")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());

        Assert.IsFalse(metadata.GetProperty("additionalProperties").GetBoolean());
    }

    /// <summary>
    /// Verifies that an upstream <c>context_id</c> argument is rejected because the name is reserved by the proxy contract.
    /// </summary>
    [TestMethod]
    public void TryRewriteRejectsContextIdCollision()
    {
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "project_id": {
                  "type": "string"
                },
                "context_id": {
                  "type": "string"
                }
              }
            }
            """);
        
        var analysis = new RoutingSchemaGuard().Analyze(schema);
        var rewriter = new PublicToolInputSchemaRewriter();
        Assert.IsFalse(rewriter.TryRewrite(schema, analysis, out _));
    }

    /// <summary>
    /// Verifies that composed schemas remain fail closed until public rewriting supports their validation semantics explicitly.
    /// </summary>
    [TestMethod]
    public void TryRewriteRejectsComposedSchema()
    {
        var schema = ParseSchema(
            """
            {
              "allOf": [
                {
                  "type": "object",
                  "properties": {
                    "project_id": {
                      "type": "string"
                    }
                  }
                }
              ]
            }
            """);

        var analysis = new RoutingSchemaGuard().Analyze(schema);
        Assert.IsTrue(analysis.IsAutomaticallyRoutable);

        var rewriter = new PublicToolInputSchemaRewriter();
        Assert.IsFalse(rewriter.TryRewrite(schema, analysis, out _));
    }

    /// <summary>
    /// Verifies that permissive top-level additional properties are rejected because they could reintroduce hidden routing arguments.
    /// </summary>
    [TestMethod]
    public void TryRewriteRejectsPermissiveAdditionalProperties()
    {
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "properties": {
                "project_id": {
                  "type": "string"
                },
                "query": {
                  "type": "string"
                }
              },
              "additionalProperties": true
            }
            """);

        var analysis = new RoutingSchemaGuard().Analyze(schema);
        var rewriter = new PublicToolInputSchemaRewriter();
        Assert.IsFalse(rewriter.TryRewrite(schema, analysis, out _));
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
