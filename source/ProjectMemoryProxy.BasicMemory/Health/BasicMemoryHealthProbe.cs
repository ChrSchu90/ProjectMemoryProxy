namespace ProjectMemoryProxy.BasicMemory.Health;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProjectMemoryProxy.BasicMemory.Client;
using ProjectMemoryProxy.BasicMemory.Discovery;

/// <summary>
/// Verifies Basic Memory availability through its read-only diagnostics MCP tool.
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
        var tool = await _toolCatalog.GetRequiredToolAsync(BasicMemoryToolNames.Diagnostics, cancellationToken).ConfigureAwait(false);
        var result = await tool.CallAsync(new Dictionary<string, object?>(), cancellationToken: cancellationToken).ConfigureAwait(false);
        BasicMemoryToolResultReader.ThrowIfError(result, BasicMemoryToolNames.Diagnostics);
    }

    #endregion

    #region Private Methods

    #endregion
}
