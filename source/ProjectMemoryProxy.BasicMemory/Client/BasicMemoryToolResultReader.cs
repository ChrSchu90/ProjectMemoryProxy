namespace ProjectMemoryProxy.BasicMemory.Client;

using System;
using System.Linq;
using System.Text.Json;
using ModelContextProtocol.Protocol;

/// <summary>
/// Reads and validates structured results returned by Basic Memory MCP tools.
/// </summary>
internal static class BasicMemoryToolResultReader
{
    #region Static Fields

    #endregion

    #region Private Fields

    #endregion

    #region Constructors

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <summary>
    /// Throws when a Basic Memory MCP tool result represents an MCP tool error.
    /// </summary>
    public static void ThrowIfError(CallToolResult result, string toolName)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (result.IsError is not true)
            return;

        var errorText = string.Join(Environment.NewLine, result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorText) ?
                                                $"Basic Memory '{toolName}' returned an MCP tool error." :
                                                $"Basic Memory '{toolName}' returned an MCP tool error: {errorText}");
    }

    /// <summary>
    /// Gets the object payload from a successful structured Basic Memory MCP tool result.
    /// </summary>
    public static JsonElement GetObjectPayload(CallToolResult result, string toolName)
    {
        ThrowIfError(result, toolName);

        if (result.StructuredContent is not { } structuredContent)
            throw new InvalidOperationException($"Basic Memory '{toolName}' returned no structured content.");

        var payload = structuredContent;
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("result", out var wrappedResult))
            payload = wrappedResult;

        if (payload.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"Basic Memory '{toolName}' returned an unsupported structured result.");

        return payload;
    }

    /// <summary>
    /// Gets a required non-empty string property from a Basic Memory result payload.
    /// </summary>
    public static string GetRequiredString(JsonElement element, string propertyName, string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
            throw new InvalidOperationException($"Basic Memory '{toolName}' returned no valid '{propertyName}' value.");

        return property.GetString()!;
    }

    /// <summary>
    /// Gets a required Boolean property from a Basic Memory result payload.
    /// </summary>
    public static bool GetRequiredBoolean(JsonElement element, string propertyName, string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidOperationException($"Basic Memory '{toolName}' returned no valid '{propertyName}' value.");

        return property.GetBoolean();
    }

    #endregion

    #region Private Methods

    #endregion
}
