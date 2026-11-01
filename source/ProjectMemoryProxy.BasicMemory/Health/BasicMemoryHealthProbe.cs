namespace ProjectMemoryProxy.BasicMemory.Health;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ProjectMemoryProxy.BasicMemory.Client;
using ProjectMemoryProxy.BasicMemory.Discovery;

/// <summary>
/// Verifies Basic Memory availability through the read-only project inventory operation.
/// </summary>
internal sealed class BasicMemoryHealthProbe : IBasicMemoryHealthProbe
{
    #region Static Fields

    #endregion

    #region Private Fields

    private readonly BasicMemoryToolCatalog _toolCatalog;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicMemoryHealthProbe"/> class.
    /// </summary>
    public BasicMemoryHealthProbe(BasicMemoryToolCatalog toolCatalog)
    {
        _toolCatalog = toolCatalog ?? throw new ArgumentNullException(nameof(toolCatalog));
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        var tool = await _toolCatalog.GetRequiredToolAsync(BasicMemoryToolNames.Project, cancellationToken).ConfigureAwait(false);
        var result = await tool.CallAsync(new Dictionary<string, object?>
        {
            ["command"] = "list",
            ["output_format"] = "json"
        }, cancellationToken: cancellationToken).ConfigureAwait(false);

        var payload = BasicMemoryToolResultReader.GetObjectPayload(result, BasicMemoryToolNames.Project);
        if (!payload.TryGetProperty("projects", out var projects) || projects.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Basic Memory project inventory response is missing its projects array.");
    }

    #endregion

    #region Private Methods

    #endregion
}
