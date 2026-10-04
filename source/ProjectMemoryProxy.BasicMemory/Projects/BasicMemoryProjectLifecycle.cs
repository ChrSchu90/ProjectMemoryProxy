namespace ProjectMemoryProxy.BasicMemory.Projects;

using ModelContextProtocol.Protocol;
using ProjectMemoryProxy.BasicMemory.Discovery;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Executes explicit Basic Memory project lifecycle operations.
/// </summary>
internal sealed class BasicMemoryProjectLifecycle : IBasicMemoryProjectLifecycle
{
    #region Static Fields

    private const string CreateMemoryProjectToolName = "create_memory_project";
    private const string DeleteProjectToolName = "delete_project";

    #endregion

    #region Private Fields

    private readonly BasicMemoryToolCatalog _toolCatalog;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicMemoryProjectLifecycle"/> class.
    /// </summary>
    /// <param name="toolCatalog">The discovered Basic Memory tool catalog.</param>
    public BasicMemoryProjectLifecycle(BasicMemoryToolCatalog toolCatalog)
    {
        _toolCatalog = toolCatalog ?? throw new ArgumentNullException(nameof(toolCatalog));
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public async Task<BasicMemoryProjectCreationResult> CreateAsync(string memoryProjectName, string memoryProjectPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryProjectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryProjectPath);

        await _toolCatalog.InitializeAsync(cancellationToken).ConfigureAwait(false);
        if (!_toolCatalog.TryGetTool(CreateMemoryProjectToolName, out var tool))
            throw new InvalidOperationException($"Basic Memory does not expose the required '{CreateMemoryProjectToolName}' project lifecycle tool.");

        var result = await tool.CallAsync(new Dictionary<string, object?>
        {
            ["project_name"] = memoryProjectName,
            ["project_path"] = memoryProjectPath,
            ["set_default"] = false,
            ["output_format"] = "json"
        }, cancellationToken: cancellationToken).ConfigureAwait(false);

        var payload = GetResultPayload(result);
        if (payload.TryGetProperty("error", out var errorElement) && errorElement.ValueKind == JsonValueKind.String)
            throw new InvalidOperationException($"Basic Memory '{CreateMemoryProjectToolName}' failed with error '{errorElement.GetString()}'.");

        var returnedName = GetRequiredString(payload, "name");
        var externalIdValue = GetRequiredString(payload, "external_id");
        var returnedPath = GetRequiredString(payload, "path");
        var created = GetRequiredBoolean(payload, "created");
        var alreadyExists = GetRequiredBoolean(payload, "already_exists");

        if (created == alreadyExists)
            throw new InvalidOperationException($"Basic Memory '{CreateMemoryProjectToolName}' returned inconsistent creation flags.");

        if (!Guid.TryParse(externalIdValue, out var memoryProjectId) || memoryProjectId == Guid.Empty)
            throw new InvalidOperationException($"Basic Memory '{CreateMemoryProjectToolName}' returned invalid external_id '{externalIdValue}'.");

        var status = created ? BasicMemoryProjectCreationStatus.Created : BasicMemoryProjectCreationStatus.AlreadyExists;
        return new BasicMemoryProjectCreationResult(status, new BasicMemoryProjectInfo(memoryProjectId, returnedName), returnedPath);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string memoryProjectName, bool deleteNotes, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryProjectName);

        await _toolCatalog.InitializeAsync(cancellationToken).ConfigureAwait(false);
        if (!_toolCatalog.TryGetTool(DeleteProjectToolName, out var tool))
            throw new InvalidOperationException($"Basic Memory does not expose the required '{DeleteProjectToolName}' project lifecycle tool.");

        var result = await tool.CallAsync(new Dictionary<string, object?>
        {
            ["project_name"] = memoryProjectName,
            ["delete_notes"] = deleteNotes
        }, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.IsError is true)
        {
            var errorText = string.Join(Environment.NewLine, result.Content.OfType<TextContentBlock>().Select(block => block.Text));
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorText) ? $"Basic Memory '{DeleteProjectToolName}' returned an MCP tool error." : $"Basic Memory '{DeleteProjectToolName}' returned an MCP tool error: {errorText}");
        }
    }

    #endregion

    #region Private Methods

    private static JsonElement GetResultPayload(CallToolResult result)
    {
        if (result.IsError is true)
        {
            var errorText = string.Join(Environment.NewLine, result.Content.OfType<TextContentBlock>().Select(block => block.Text));
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorText) ?
                                                    $"Basic Memory '{CreateMemoryProjectToolName}' returned an MCP tool error." :
                                                    $"Basic Memory '{CreateMemoryProjectToolName}' returned an MCP tool error: {errorText}");
        }

        if (result.StructuredContent is not { } structuredContent)
            throw new InvalidOperationException($"Basic Memory '{CreateMemoryProjectToolName}' returned no structured content.");

        var payload = structuredContent;
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("result", out var wrappedResult))
            payload = wrappedResult;

        if (payload.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"Basic Memory '{CreateMemoryProjectToolName}' returned an unsupported structured result.");

        return payload;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
            throw new InvalidOperationException($"Basic Memory '{CreateMemoryProjectToolName}' returned no valid '{propertyName}' value.");

        return property.GetString()!;
    }

    private static bool GetRequiredBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidOperationException($"Basic Memory '{CreateMemoryProjectToolName}' returned no valid '{propertyName}' value.");

        return property.GetBoolean();
    }

    #endregion
}
