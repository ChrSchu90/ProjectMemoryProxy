namespace ProjectMemoryProxy.Server.Tools;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// Represents a registered Basic Memory project routing exposed through the MCP control plane.
/// </summary>
internal sealed record ProjectRoutingInfo(
    [property: JsonPropertyName("project_id")] Guid ProjectId,
    [property: JsonPropertyName("project_name")] string ProjectName,
    [property: JsonPropertyName("status")] string Status);
