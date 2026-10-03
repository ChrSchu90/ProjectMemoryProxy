namespace ProjectMemoryProxy.BasicMemory;

using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides the stable Basic Memory tool catalog discovered for the current process lifetime.
/// </summary>
internal sealed class BasicMemoryToolCatalog : IDisposable
{
    #region Static Fields

    #endregion

    #region Private Fields

    private readonly IBasicMemoryClient _client;
    private readonly ILogger<BasicMemoryToolCatalog> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    private CatalogSnapshot? _snapshot;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicMemoryToolCatalog"/> class.
    /// </summary>
    public BasicMemoryToolCatalog(IBasicMemoryClient client, ILogger<BasicMemoryToolCatalog> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether the Basic Memory tool catalog has been initialized.
    /// </summary>
    public bool IsInitialized => Volatile.Read(ref _snapshot) != null;

    /// <summary>
    /// Gets the stable Basic Memory tool snapshot.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The catalog has not been initialized.
    /// </exception>
    public IReadOnlyList<McpClientTool> Tools => GetSnapshot().Tools;

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public void Dispose()
    {
        _initializationLock.Dispose();
    }

    /// <summary>
    /// Discovers and stores the Basic Memory tool catalog for the current process lifetime.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _snapshot) != null)
            return;

        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_snapshot != null)
                return;

            var discoveredTools = await _client.ListToolsAsync(cancellationToken).ConfigureAwait(false);
            var tools = new List<McpClientTool>(discoveredTools.Count);
            var toolsByName = new Dictionary<string, McpClientTool>(discoveredTools.Count, StringComparer.Ordinal);
            foreach (var tool in discoveredTools)
            {
                var toolName = tool.ProtocolTool.Name;
                if (!toolsByName.TryAdd(toolName, tool))
                    throw new InvalidOperationException($"Basic Memory exposed duplicate MCP tool name '{toolName}'.");

                tools.Add(tool);
            }

            var snapshot = new CatalogSnapshot(new ReadOnlyCollection<McpClientTool>(tools), new ReadOnlyDictionary<string, McpClientTool>(toolsByName));
            Volatile.Write(ref _snapshot, snapshot);
            _logger.LogInformation("Discovered {ToolCount} Basic Memory MCP tools.", snapshot.Tools.Count);
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <summary>
    /// Attempts to get a Basic Memory tool by its exact upstream MCP tool name.
    /// </summary>
    public bool TryGetTool(string name, [NotNullWhen(true)] out McpClientTool? tool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetSnapshot().ToolsByName.TryGetValue(name, out tool);
    }

    #endregion

    #region Private Methods

    private CatalogSnapshot GetSnapshot()
    {
        return Volatile.Read(ref _snapshot) ?? 
               throw new InvalidOperationException("The Basic Memory tool catalog has not been initialized.");
    }

    #endregion

    #region Netsted Classes

    private sealed record CatalogSnapshot(IReadOnlyList<McpClientTool> Tools, IReadOnlyDictionary<string, McpClientTool> ToolsByName);

    #endregion
}
