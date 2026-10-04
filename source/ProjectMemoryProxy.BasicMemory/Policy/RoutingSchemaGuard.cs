namespace ProjectMemoryProxy.BasicMemory.Policy;

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

    private static readonly HashSet<string> CanonicalRoutingSelectorNames = new(StringComparer.Ordinal)
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

    private static readonly HashSet<string> UnsupportedRoutingSelectorNames = new(StringComparer.Ordinal)
    {
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

    private static readonly HashSet<string> RoutingTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "project",
        "projects",
        "workspace",
        "tenant"
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
    /// Analyzes an MCP input schema and determines whether its project routing can be controlled generically by the proxy.
    /// </summary>
    public RoutingSchemaAnalysis Analyze(JsonElement inputSchema)
    {
        if (inputSchema.ValueKind != JsonValueKind.Object)
            return Blocked(RoutingSchemaStatus.UnsupportedSchema);

        var state = new AnalysisState();
        var activeReferences = new HashSet<string>(StringComparer.Ordinal);
        
        var failure = AnalyzeSchema(inputSchema, inputSchema, 0, false, state, activeReferences);
        if (failure.HasValue)
            return Blocked(failure.Value, state);

        foreach (var selector in UnsupportedRoutingSelectorNames)
        {
            if (state.RequiredProperties.Contains(selector))
                return Blocked(RoutingSchemaStatus.RequiredUnsupportedSelector, state);
        }

        var hasProjectId = state.RoutingSelectors.Contains("project_id");
        var hasProject = state.RoutingSelectors.Contains("project");
        if (!hasProjectId && !hasProject)
            return Blocked(RoutingSchemaStatus.NoProjectSelector, state);

        if (hasProjectId)
        {
            return new RoutingSchemaAnalysis(RoutingSchemaStatus.AutomaticallyRoutable, ProjectSelectorKind.ProjectId, InjectProjectName: hasProject && state.RequiredProperties.Contains("project"), GetSuppressedProperties(state));
        }

        return new RoutingSchemaAnalysis(RoutingSchemaStatus.AutomaticallyRoutable, ProjectSelectorKind.ProjectName, InjectProjectName: true, GetSuppressedProperties(state));
    }

    #endregion

    #region Private Methods

    private static RoutingSchemaStatus? AnalyzeSchema(JsonElement rootSchema, JsonElement schema, int logicalDepth, bool alternativeRouting, AnalysisState state, HashSet<string> activeReferences)
    {
        if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return null;

        if (schema.ValueKind != JsonValueKind.Object || schema.TryGetProperty("$dynamicRef", out _))
        {
            return RoutingSchemaStatus.UnsupportedSchema;
        }

        if (schema.TryGetProperty("$ref", out var referenceElement))
        {
            if (referenceElement.ValueKind != JsonValueKind.String)
                return RoutingSchemaStatus.UnsupportedSchema;

            var reference = referenceElement.GetString();

            if (reference == null || !TryResolveLocalReference(rootSchema, reference, out var referencedSchema) || !activeReferences.Add(reference))
            {
                return RoutingSchemaStatus.UnsupportedSchema;
            }
            
            var failure = AnalyzeSchema(rootSchema, referencedSchema, logicalDepth, alternativeRouting, state, activeReferences);
            activeReferences.Remove(reference);
            if (failure.HasValue)
                return failure;
        }

        foreach (var schemaProperty in schema.EnumerateObject())
        {
            RoutingSchemaStatus? failure;
            switch (schemaProperty.Name)
            {
                case "$ref":
                case "$defs":
                case "definitions":
                    continue;
                case "properties":
                    failure = AnalyzeProperties(rootSchema, schemaProperty.Value, logicalDepth, alternativeRouting, state, activeReferences);
                    break;
                case "required":
                    failure = AnalyzeRequired(schemaProperty.Value, logicalDepth, alternativeRouting, state);
                    break;
                case "allOf":
                    failure = AnalyzeSchemaArray(rootSchema, schemaProperty.Value, logicalDepth, alternativeRouting, state, activeReferences);
                    break;
                case "oneOf":
                case "anyOf":
                    failure = AnalyzeSchemaArray(rootSchema, schemaProperty.Value, logicalDepth, true, state, activeReferences);
                    break;
                case "items":
                case "prefixItems":
                case "contains":
                case "additionalProperties":
                    failure = AnalyzeNestedSchemas(rootSchema, schemaProperty.Value, logicalDepth + 1, alternativeRouting, state, activeReferences);
                    break;
                case "not":
                case "if":
                case "then":
                case "else":
                    failure = AnalyzeNestedSchemas(rootSchema, schemaProperty.Value, logicalDepth, true, state, activeReferences);
                    break;
                case "dependentSchemas":
                    failure = AnalyzeDependentSchemas(rootSchema, schemaProperty.Value, logicalDepth, state, activeReferences);
                    break;
                case "patternProperties":
                case "dependentRequired":
                case "dependencies":
                    return RoutingSchemaStatus.UnsupportedSchema;
                default:
                    continue;
            }

            if (failure.HasValue)
                return failure;
        }

        return null;
    }

    private static RoutingSchemaStatus? AnalyzeProperties(JsonElement rootSchema, JsonElement properties, int logicalDepth, bool alternativeRouting, AnalysisState state, HashSet<string> activeReferences)
    {
        if (properties.ValueKind != JsonValueKind.Object)
            return RoutingSchemaStatus.UnsupportedSchema;

        foreach (var property in properties.EnumerateObject())
        {
            var failure = RegisterRoutingSelector(property.Name, logicalDepth, alternativeRouting, state);
            if (failure.HasValue)
                return failure;

            failure = AnalyzeSchema(rootSchema, property.Value, logicalDepth + 1, alternativeRouting, state, activeReferences);
            if (failure.HasValue)
                return failure;
        }

        return null;
    }

    private static RoutingSchemaStatus? AnalyzeRequired(JsonElement required, int logicalDepth, bool alternativeRouting, AnalysisState state)
    {
        if (required.ValueKind != JsonValueKind.Array)
            return RoutingSchemaStatus.UnsupportedSchema;

        foreach (var requiredProperty in required.EnumerateArray())
        {
            if (requiredProperty.ValueKind != JsonValueKind.String)
                return RoutingSchemaStatus.UnsupportedSchema;

            var propertyName = requiredProperty.GetString();

            if (propertyName == null)
                return RoutingSchemaStatus.UnsupportedSchema;

            if (logicalDepth == 0 && !alternativeRouting)
                state.RequiredProperties.Add(propertyName);

            var failure = RegisterRoutingSelector(propertyName, logicalDepth, alternativeRouting, state);
            if (failure.HasValue)
                return failure;
        }

        return null;
    }

    private static RoutingSchemaStatus? RegisterRoutingSelector(string propertyName, int logicalDepth, bool alternativeRouting, AnalysisState state)
    {
        if (!IsRoutingLikeName(propertyName))
            return null;

        if (alternativeRouting)
            return RoutingSchemaStatus.AlternativeRoutingSelector;

        if (logicalDepth != 0)
            return RoutingSchemaStatus.NestedRoutingSelector;

        if (!CanonicalRoutingSelectorNames.Contains(propertyName))
            return RoutingSchemaStatus.UnknownRoutingSelector;

        state.RoutingSelectors.Add(propertyName);
        state.SuppressedProperties.Add(propertyName);
        return null;
    }

    private static RoutingSchemaStatus? AnalyzeSchemaArray(JsonElement rootSchema, JsonElement schemas, int logicalDepth, bool alternativeRouting, AnalysisState state, HashSet<string> activeReferences)
    {
        if (schemas.ValueKind != JsonValueKind.Array)
            return RoutingSchemaStatus.UnsupportedSchema;

        foreach (var schema in schemas.EnumerateArray())
        {
            var failure = AnalyzeSchema(rootSchema, schema, logicalDepth, alternativeRouting, state, activeReferences);
            if (failure.HasValue)
                return failure;
        }

        return null;
    }

    private static RoutingSchemaStatus? AnalyzeNestedSchemas(JsonElement rootSchema, JsonElement schemas, int logicalDepth, bool alternativeRouting, AnalysisState state, HashSet<string> activeReferences)
    {
        if (schemas.ValueKind != JsonValueKind.Array)
        {
            return AnalyzeSchema(rootSchema, schemas, logicalDepth, alternativeRouting, state, activeReferences);
        }

        foreach (var schema in schemas.EnumerateArray())
        {
            var failure = AnalyzeSchema(rootSchema, schema, logicalDepth, alternativeRouting, state, activeReferences);
            if (failure.HasValue)
                return failure;
        }

        return null;
    }

    private static RoutingSchemaStatus? AnalyzeDependentSchemas(JsonElement rootSchema, JsonElement dependentSchemas, int logicalDepth, AnalysisState state, HashSet<string> activeReferences)
    {
        if (dependentSchemas.ValueKind != JsonValueKind.Object)
            return RoutingSchemaStatus.UnsupportedSchema;

        foreach (var dependentSchema in dependentSchemas.EnumerateObject())
        {
            if (IsRoutingLikeName(dependentSchema.Name))
                return RoutingSchemaStatus.AlternativeRoutingSelector;
            
            var failure = AnalyzeSchema(rootSchema, dependentSchema.Value, logicalDepth, true, state, activeReferences);
            if (failure.HasValue)
                return failure;
        }

        return null;
    }

    private static bool IsRoutingLikeName(string name)
    {
        if (CanonicalRoutingSelectorNames.Contains(name))
            return true;

        var normalizedName = NormalizeName(name);

        if (NormalizedRoutingSelectorNames.Contains(normalizedName))
            return true;

        return ContainsRoutingToken(name);
    }

    private static string NormalizeName(string name)
    {
        var normalizedName = new StringBuilder(name.Length);

        foreach (var character in name)
        {
            if (char.IsLetterOrDigit(character))
                normalizedName.Append(char.ToLowerInvariant(character));
        }

        return normalizedName.ToString();
    }

    private static bool ContainsRoutingToken(string name)
    {
        var token = new StringBuilder(name.Length);
        char? previousCharacter = null;

        foreach (var character in name)
        {
            if (!char.IsLetterOrDigit(character))
            {
                if (IsRoutingToken(token))
                    return true;

                token.Clear();
                previousCharacter = null;
                continue;
            }

            if (char.IsUpper(character) && previousCharacter.HasValue && char.IsLower(previousCharacter.Value))
            {
                if (IsRoutingToken(token))
                    return true;

                token.Clear();
            }

            token.Append(char.ToLowerInvariant(character));
            previousCharacter = character;
        }

        return IsRoutingToken(token);
    }

    private static bool IsRoutingToken(StringBuilder token)
    {
        return RoutingTokens.Contains(token.ToString());
    }

    private static RoutingSchemaAnalysis Blocked(RoutingSchemaStatus status, AnalysisState? state = null)
    {
        return new RoutingSchemaAnalysis(status, ProjectSelectorKind.None, InjectProjectName: false, state == null ? [] : GetSuppressedProperties(state));
    }

    private static IReadOnlyList<string> GetSuppressedProperties(AnalysisState state)
    {
        var properties = new string[state.SuppressedProperties.Count];
        state.SuppressedProperties.CopyTo(properties);
        Array.Sort(properties, StringComparer.Ordinal);
        return properties;
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
        foreach (var segment in reference[2..].Split('/'))
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

    #region Nested Classes

    private sealed class AnalysisState
    {
        internal HashSet<string> RoutingSelectors { get; } = new(StringComparer.Ordinal);

        internal HashSet<string> RequiredProperties { get; } = new(StringComparer.Ordinal);

        internal HashSet<string> SuppressedProperties { get; } = new(StringComparer.Ordinal);
    }

    #endregion
}
