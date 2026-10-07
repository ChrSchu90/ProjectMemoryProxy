namespace ProjectMemoryProxy.BasicMemory.Projects.Lifecycle;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ProjectMemoryProxy.BasicMemory;
using ProjectMemoryProxy.BasicMemory.Client;
using ProjectMemoryProxy.BasicMemory.Discovery;
using ProjectMemoryProxy.BasicMemory.Projects.Directory;

/// <summary>
/// Executes explicit Basic Memory project lifecycle operations.
/// </summary>
internal sealed class BasicMemoryProjectLifecycle : IBasicMemoryProjectLifecycle
{
    #region Static Fields

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

        var tool = await _toolCatalog.GetRequiredToolAsync(BasicMemoryToolNames.CreateMemoryProject, cancellationToken).ConfigureAwait(false);

        var result = await tool.CallAsync(new Dictionary<string, object?>
        {
            ["project_name"] = memoryProjectName,
            ["project_path"] = memoryProjectPath,
            ["set_default"] = false,
            ["output_format"] = "json"
        }, cancellationToken: cancellationToken).ConfigureAwait(false);

        var payload = BasicMemoryToolResultReader.GetObjectPayload(result, BasicMemoryToolNames.CreateMemoryProject);
        if (payload.TryGetProperty("error", out var errorElement) && errorElement.ValueKind == JsonValueKind.String)
            throw new InvalidOperationException($"Basic Memory '{BasicMemoryToolNames.CreateMemoryProject}' failed with error '{errorElement.GetString()}'.");

        var returnedName = BasicMemoryToolResultReader.GetRequiredString(payload, "name", BasicMemoryToolNames.CreateMemoryProject);
        var externalIdValue = BasicMemoryToolResultReader.GetRequiredString(payload, "external_id", BasicMemoryToolNames.CreateMemoryProject);
        var returnedPath = BasicMemoryToolResultReader.GetRequiredString(payload, "path", BasicMemoryToolNames.CreateMemoryProject);
        var created = BasicMemoryToolResultReader.GetRequiredBoolean(payload, "created", BasicMemoryToolNames.CreateMemoryProject);
        var alreadyExists = BasicMemoryToolResultReader.GetRequiredBoolean(payload, "already_exists", BasicMemoryToolNames.CreateMemoryProject);

        if (created == alreadyExists)
            throw new InvalidOperationException($"Basic Memory '{BasicMemoryToolNames.CreateMemoryProject}' returned inconsistent creation flags.");

        if (!Guid.TryParse(externalIdValue, out var memoryProjectId) || memoryProjectId == Guid.Empty)
            throw new InvalidOperationException($"Basic Memory '{BasicMemoryToolNames.CreateMemoryProject}' returned invalid external_id '{externalIdValue}'.");

        var status = created ? BasicMemoryProjectCreationStatus.Created : BasicMemoryProjectCreationStatus.AlreadyExists;
        return new BasicMemoryProjectCreationResult(status, new BasicMemoryProjectInfo(memoryProjectId, returnedName), returnedPath);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string memoryProjectName, bool deleteNotes, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryProjectName);

        var tool = await _toolCatalog.GetRequiredToolAsync(BasicMemoryToolNames.DeleteProject, cancellationToken).ConfigureAwait(false);

        var result = await tool.CallAsync(new Dictionary<string, object?>
        {
            ["project_name"] = memoryProjectName,
            ["delete_notes"] = deleteNotes
        }, cancellationToken: cancellationToken).ConfigureAwait(false);

        BasicMemoryToolResultReader.ThrowIfError(result, BasicMemoryToolNames.DeleteProject);
    }

    #endregion

    #region Private Methods


    #endregion
}
