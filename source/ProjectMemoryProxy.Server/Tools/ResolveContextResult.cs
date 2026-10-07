namespace ProjectMemoryProxy.Server.Tools;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// Represents the public result of resolving a technical context identifier.
/// </summary>
internal sealed record ResolveContextResult(
    [property: JsonPropertyName("context_id")] string ContextId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("project_id"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] Guid? ProjectId);
