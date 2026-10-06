namespace ProjectMemoryProxy.Server.Tools;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Represents the registered project-routing inventory exposed through the MCP control plane.
/// </summary>
internal sealed record ListProjectsResult(
    [property: JsonPropertyName("projects")] IReadOnlyList<ProjectRoutingInfo> Projects);
