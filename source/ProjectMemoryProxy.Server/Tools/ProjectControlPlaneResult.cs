namespace ProjectMemoryProxy.Server.Tools;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a project control-plane operation result.
/// </summary>
internal sealed record ProjectControlPlaneResult(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("project")] ProjectRoutingInfo? Project);
