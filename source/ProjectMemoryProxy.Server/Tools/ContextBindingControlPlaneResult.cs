namespace ProjectMemoryProxy.Server.Tools;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a context-binding control-plane operation result.
/// </summary>
internal sealed record ContextBindingControlPlaneResult(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("binding")] ContextBindingInfo? Binding);
