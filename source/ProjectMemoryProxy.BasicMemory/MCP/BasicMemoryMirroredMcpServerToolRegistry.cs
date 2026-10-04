namespace ProjectMemoryProxy.BasicMemory.MCP;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using ModelContextProtocol.Server;


/// <summary>
/// Stores the stable process-lifetime snapshot of mirrored Basic Memory MCP server tools.
/// </summary>
internal sealed class BasicMemoryMirroredMcpServerToolRegistry
{
    #region Static Fields
    private static readonly IReadOnlyList<McpServerTool> EmptyTools = [];

    #endregion

    #region Private Fields

    private IReadOnlyList<McpServerTool>? _tools;

    #endregion

    #region Constructors

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether the mirrored MCP server tool snapshot has been initialized.
    /// </summary>
    internal bool IsInitialized => Volatile.Read(ref _tools) != null;

    /// <summary>
    /// Gets the stable mirrored MCP server tool snapshot, or an empty collection before initialization.
    /// </summary>
    internal IReadOnlyList<McpServerTool> Tools => Volatile.Read(ref _tools) ?? EmptyTools;

    #endregion

    #region Public Methods

    /// <summary>
    /// Initializes the stable mirrored MCP server tool snapshot.
    /// </summary>
    internal void Initialize(IEnumerable<McpServerTool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var snapshot = new ReadOnlyCollection<McpServerTool>(tools.ToArray());
        if (Interlocked.CompareExchange(ref _tools, snapshot, null) != null)
            throw new InvalidOperationException("The mirrored Basic Memory MCP server tool registry has already been initialized.");

    }

    #endregion

    #region Private Methods

    #endregion
}
