namespace ProjectMemoryProxy.BasicMemory.Mirroring;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProjectMemoryProxy.BasicMemory.Policy;

/// <summary>
/// Rewrites an automatically routable Basic Memory input schema into the public proxy schema.
/// </summary>
internal sealed class PublicToolInputSchemaRewriter
{
    #region Static Fields

    private const string ContextIdPropertyName = "context_id";

    private static readonly HashSet<string> UnsupportedTopLevelKeywords = new(StringComparer.Ordinal)
    {
        "$ref",
        "allOf",
        "anyOf",
        "oneOf",
        "not",
        "if",
        "then",
        "else",
        "dependentSchemas",
        "dependentRequired",
        "patternProperties",
        "propertyNames",
        "minProperties",
        "maxProperties",
        "unevaluatedProperties"
    };

    #endregion

    #region Private Fields

    #endregion

    #region Constructors

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <summary>
    /// Attempts to rewrite an automatically routable upstream schema into a public proxy schema.
    /// </summary>
    /// <param name="inputSchema">The canonical upstream input schema.</param>
    /// <param name="analysis">The routing analysis for the schema.</param>
    /// <param name="publicSchema">The rewritten public schema when successful.</param>
    /// <returns>
    /// <see langword="true"/> when the schema can be rewritten safely;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public bool TryRewrite(JsonElement inputSchema, RoutingSchemaAnalysis analysis, out JsonElement publicSchema)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        publicSchema = default;

        if (!analysis.IsAutomaticallyRoutable || !CanRewrite(inputSchema, analysis))
        {
            return false;
        }

        var root = JsonNode.Parse(inputSchema.GetRawText()) as JsonObject;
        if (root == null)
            return false;

        var properties = root["properties"] as JsonObject;
        if (properties == null)
            return false;

        var suppressedProperties = new HashSet<string>(analysis.SuppressedProperties, StringComparer.Ordinal);
        foreach (var propertyName in suppressedProperties)
        {
            properties.Remove(propertyName);
        }

        properties[ContextIdPropertyName] = new JsonObject
        {
            ["type"] = "string",
            ["description"] = "Canonical technical context identifier used by ProjectMemoryProxy for server-controlled project routing."
        };

        RewriteRequiredProperties(root, suppressedProperties);
        root["type"] = "object";

        // The public proxy contract accepts only explicitly advertised arguments.
        // Routing selectors removed above therefore cannot be reintroduced as
        // arbitrary additional properties.
        root["additionalProperties"] = false;

        publicSchema = JsonSerializer.SerializeToElement(root);
        return true;
    }

    #endregion

    #region Private Methods

    private static bool CanRewrite(JsonElement inputSchema, RoutingSchemaAnalysis analysis)
    {
        if (inputSchema.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var keyword in UnsupportedTopLevelKeywords)
        {
            if (inputSchema.TryGetProperty(keyword, out _))
                return false;
        }

        if (inputSchema.TryGetProperty("type", out var type))
        {
            if (type.ValueKind != JsonValueKind.String || type.GetString() != "object")
            {
                return false;
            }
        }

        if (!inputSchema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (properties.TryGetProperty(ContextIdPropertyName, out _))
        {
            return false;
        }

        if (!HasCanonicalProjectSelector(properties, analysis.ProjectSelector))
        {
            return false;
        }

        if (inputSchema.TryGetProperty("additionalProperties", out var additionalProperties) && additionalProperties.ValueKind != JsonValueKind.False)
        {
            return false;
        }

        if (!inputSchema.TryGetProperty("required", out var required))
        {
            return true;
        }

        if (required.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var requiredProperty in required.EnumerateArray())
        {
            if (requiredProperty.ValueKind != JsonValueKind.String)
                return false;

            if (requiredProperty.GetString() == ContextIdPropertyName)
                return false;
        }

        return true;
    }

    private static bool HasCanonicalProjectSelector(JsonElement properties, ProjectSelectorKind projectSelector)
    {
        return projectSelector switch
        {
            ProjectSelectorKind.ProjectId => properties.TryGetProperty("project_id", out _),
            ProjectSelectorKind.ProjectName => properties.TryGetProperty("project", out _),
            _ => false
        };
    }

    private static void RewriteRequiredProperties(JsonObject root, HashSet<string> suppressedProperties)
    {
        var publicRequired = new JsonArray();
        if (root["required"] is JsonArray required)
        {
            foreach (var requiredProperty in required)
            {
                if (requiredProperty is not JsonValue value || !value.TryGetValue<string>(out var propertyName))
                {
                    continue;
                }

                if (!suppressedProperties.Contains(propertyName))
                {
                    publicRequired.Add(propertyName);
                }
            }
        }

        publicRequired.Add(ContextIdPropertyName);
        root["required"] = publicRequired;
    }

    #endregion
}
