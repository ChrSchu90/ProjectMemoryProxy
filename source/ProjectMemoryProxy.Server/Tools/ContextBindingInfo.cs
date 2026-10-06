namespace ProjectMemoryProxy.Server.Tools;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// Represents an exact technical context binding exposed through the MCP control plane.
/// </summary>
internal sealed record ContextBindingInfo(
    [property: JsonPropertyName("context_id")] string ContextId,
    [property: JsonPropertyName("project_id")] Guid ProjectId,
    [property: JsonPropertyName("status")] string Status);
