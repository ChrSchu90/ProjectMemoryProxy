namespace ProjectMemoryProxy.Server.Tools;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Represents the exact context-binding inventory exposed through the MCP control plane.
/// </summary>
internal sealed record ListContextBindingsResult(
    [property: JsonPropertyName("bindings")] IReadOnlyList<ContextBindingInfo> Bindings);
