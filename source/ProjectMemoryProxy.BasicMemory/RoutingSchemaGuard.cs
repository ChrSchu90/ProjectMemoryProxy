namespace ProjectMemoryProxy.BasicMemory;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

/// <summary>
/// Validates routing-sensitive portions of Basic Memory MCP input schemas.
/// </summary>
internal sealed class RoutingSchemaGuard
{
    #region Static Fields

    private static readonly HashSet<string> RoutingSelectorNames = new(StringComparer.Ordinal)
    {
        "project",
        "project_id",
        "projects",
        "workspace",
        "workspace_id",
        "tenant",
        "tenant_id",
        "search_all_projects"
    };

    private static readonly HashSet<string> NormalizedRoutingSelectorNames = new(StringComparer.Ordinal)
    {
        "project",
        "projectid",
        "projects",
        "workspace",
        "workspaceid",
        "tenant",
        "tenantid",
        "searchallprojects"
    };


    #endregion

    #region Private Fields

    #endregion

    #region Constructors

    /// <summary>
    /// Analyzes an MCP input schema against the expected top-level routing selectors.
    /// </summary>
    public RoutingSchemaGuardStatus Analyze(JsonElement inputSchema, IReadOnlyCollection<string> expectedTopLevelSelectors)
    {
        ArgumentNullException.ThrowIfNull(expectedTopLevelSelectors);


        if (inputSchema.ValueKind != JsonValueKind.Object)
            return RoutingSchemaGuardStatus.UnsupportedSchema;

        var expectedSelectors = new HashSet<string>(expectedTopLevelSelectors, StringComparer.Ordinal);
        if (!expectedSelectors.IsSubsetOf(RoutingSelectorNames))
            throw new ArgumentException("Expected routing selectors must use known canonical selector names.", nameof(expectedTopLevelSelectors));

        var discoveredSelectors = new HashSet<string>(StringComparer.Ordinal);
        var activeReferences = new HashSet<string>(StringComparer.Ordinal);

        var status = AnalyzeSchema(inputSchema, inputSchema, logicalDepth: 0, discoveredSelectors, activeReferences);
        if (status != RoutingSchemaGuardStatus.Compatible)
            return status;

        return discoveredSelectors.SetEquals(expectedSelectors) ?
                   RoutingSchemaGuardStatus.Compatible :
                   RoutingSchemaGuardStatus.UnexpectedRoutingSelector;
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    #endregion

    #region Private Methods

    private static RoutingSchemaGuardStatus AnalyzeSchema(JsonElement rootSchema, JsonElement schema, int logicalDepth, HashSet<string> discoveredSelectors, HashSet<string> activeReferences)
    {
        if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return RoutingSchemaGuardStatus.Compatible;

        if (schema.ValueKind != JsonValueKind.Object)
            return RoutingSchemaGuardStatus.UnsupportedSchema;

        if (schema.TryGetProperty("$dynamicRef", out _))
            return RoutingSchemaGuardStatus.UnsupportedSchema;

        if (schema.TryGetProperty("$ref", out var referenceElement))
        {
            if (referenceElement.ValueKind != JsonValueKind.String)
                return RoutingSchemaGuardStatus.UnsupportedSchema;

            var reference = referenceElement.GetString();
            if (reference == null || !TryResolveLocalReference(rootSchema, reference, out var referencedSchema) || !activeReferences.Add(reference))
            {
                return RoutingSchemaGuardStatus.UnsupportedSchema;
            }

            var referenceStatus = AnalyzeSchema(rootSchema, referencedSchema, logicalDepth, discoveredSelectors, activeReferences);
            activeReferences.Remove(reference);

            if (referenceStatus != RoutingSchemaGuardStatus.Compatible)
                return referenceStatus;
        }

        foreach (var schemaProperty in schema.EnumerateObject())
        {
            RoutingSchemaGuardStatus status;
            switch (schemaProperty.Name)
            {
                case "$ref":
                case "$defs":
                case "definitions":
                    continue;
                case "properties":
                    status = AnalyzeProperties(
                        rootSchema,
                        schemaProperty.Value,
                        logicalDepth,
                        discoveredSelectors,
                        activeReferences);
                    break;
                case "oneOf":
                case "anyOf":
                case "allOf":
                    status = AnalyzeSchemaArray(
                        rootSchema,
                        schemaProperty.Value,
                        logicalDepth,
                        discoveredSelectors,
                        activeReferences);
                    break;
                case "items":
                case "prefixItems":
                case "contains":
                    status = AnalyzeNestedSchemas(
                        rootSchema,
                        schemaProperty.Value,
                        logicalDepth + 1,
                        discoveredSelectors,
                        activeReferences);
                    break;
                case "not":
                case "if":
                case "then":
                case "else":
                    status = AnalyzeNestedSchemas(
                        rootSchema,
                        schemaProperty.Value,
                        logicalDepth,
                        discoveredSelectors,
                        activeReferences);
                    break;
                case "dependentSchemas":
                    status = AnalyzeDependentSchemas(
                        rootSchema,
                        schemaProperty.Value,
                        logicalDepth,
                        discoveredSelectors,
                        activeReferences);
                    break;
                case "patternProperties":
                    return RoutingSchemaGuardStatus.UnsupportedSchema;
                default:
                    continue;
            }

            if (status != RoutingSchemaGuardStatus.Compatible)
                return status;
        }

        return RoutingSchemaGuardStatus.Compatible;
    }

    private static RoutingSchemaGuardStatus AnalyzeProperties(JsonElement rootSchema, JsonElement properties, int logicalDepth, HashSet<string> discoveredSelectors, HashSet<string> activeReferences)
    {
        if (properties.ValueKind != JsonValueKind.Object)
            return RoutingSchemaGuardStatus.UnsupportedSchema;

        foreach (var property in properties.EnumerateObject())
        {
            if (IsRoutingLikeName(property.Name))
            {
                if (logicalDepth != 0 || !RoutingSelectorNames.Contains(property.Name))
                    return RoutingSchemaGuardStatus.UnexpectedRoutingSelector;

                discoveredSelectors.Add(property.Name);
            }
            
            var status = AnalyzeSchema(rootSchema, property.Value, logicalDepth + 1, discoveredSelectors, activeReferences);
            if (status != RoutingSchemaGuardStatus.Compatible)
                return status;
        }

        return RoutingSchemaGuardStatus.Compatible;
    }

    private static RoutingSchemaGuardStatus AnalyzeSchemaArray(JsonElement rootSchema, JsonElement schemas, int logicalDepth, HashSet<string> discoveredSelectors, HashSet<string> activeReferences)
    {
        if (schemas.ValueKind != JsonValueKind.Array)
            return RoutingSchemaGuardStatus.UnsupportedSchema;

        foreach (var schema in schemas.EnumerateArray())
        {
            var status = AnalyzeSchema(rootSchema, schema, logicalDepth, discoveredSelectors, activeReferences);
            if (status != RoutingSchemaGuardStatus.Compatible)
                return status;
        }

        return RoutingSchemaGuardStatus.Compatible;
    }

    private static RoutingSchemaGuardStatus AnalyzeNestedSchemas(JsonElement rootSchema, JsonElement schemas, int logicalDepth, HashSet<string> discoveredSelectors, HashSet<string> activeReferences)
    {
        if (schemas.ValueKind == JsonValueKind.Array)
        {
            foreach (var schema in schemas.EnumerateArray())
            {
                var status = AnalyzeSchema(rootSchema, schema, logicalDepth, discoveredSelectors, activeReferences);
                if (status != RoutingSchemaGuardStatus.Compatible)
                    return status;
            }

            return RoutingSchemaGuardStatus.Compatible;
        }

        return AnalyzeSchema(rootSchema, schemas, logicalDepth, discoveredSelectors, activeReferences);
    }

    private static RoutingSchemaGuardStatus AnalyzeDependentSchemas(JsonElement rootSchema, JsonElement dependentSchemas, int logicalDepth, HashSet<string> discoveredSelectors, HashSet<string> activeReferences)
    {
        if (dependentSchemas.ValueKind != JsonValueKind.Object)
            return RoutingSchemaGuardStatus.UnsupportedSchema;

        foreach (var dependentSchema in dependentSchemas.EnumerateObject())
        {
            var status = AnalyzeSchema(
                rootSchema,
                dependentSchema.Value,
                logicalDepth,
                discoveredSelectors,
                activeReferences);

            if (status != RoutingSchemaGuardStatus.Compatible)
                return status;
        }

        return RoutingSchemaGuardStatus.Compatible;
    }

    private static bool IsRoutingLikeName(string name)
    {
        if (RoutingSelectorNames.Contains(name))
            return true;

        var normalizedName = new StringBuilder(name.Length);

        foreach (var character in name)
        {
            if (character is '_' or '-')
                continue;

            normalizedName.Append(char.ToLowerInvariant(character));
        }

        return NormalizedRoutingSelectorNames.Contains(normalizedName.ToString());
    }

    private static bool TryResolveLocalReference(JsonElement rootSchema, string reference, out JsonElement resolvedSchema)
    {
        if (reference == "#")
        {
            resolvedSchema = rootSchema;
            return true;
        }

        if (!reference.StartsWith("#/", StringComparison.Ordinal))
        {
            resolvedSchema = default;
            return false;
        }

        var current = rootSchema;
        var segments = reference[2..].Split('/');
        foreach (var segment in segments)
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                resolvedSchema = default;
                return false;
            }

            var decodedSegment = segment
                .Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);

            if (!current.TryGetProperty(decodedSegment, out current))
            {
                resolvedSchema = default;
                return false;
            }
        }

        resolvedSchema = current;
        return true;
    }

    #endregion
}
