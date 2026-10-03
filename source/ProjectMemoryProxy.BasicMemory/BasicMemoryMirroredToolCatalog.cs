namespace ProjectMemoryProxy.BasicMemory;

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides the stable process-lifetime snapshot of Basic Memory tools that can be mirrored through the generic proxy path.
/// </summary>
internal sealed class BasicMemoryMirroredToolCatalog : IDisposable
{
    #region Static Fields

    #endregion

    #region Private Fields

    private readonly BasicMemoryToolCatalog _upstreamCatalog;
    private readonly ILogger<BasicMemoryMirroredToolCatalog> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    private readonly BasicMemoryToolClassifier _classifier = new();
    private readonly RoutingSchemaGuard _routingSchemaGuard = new();
    private readonly PublicToolInputSchemaRewriter _schemaRewriter = new();

    private CatalogSnapshot? _snapshot;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicMemoryMirroredToolCatalog"/> class.
    /// </summary>
    public BasicMemoryMirroredToolCatalog(BasicMemoryToolCatalog upstreamCatalog, ILogger<BasicMemoryMirroredToolCatalog> logger)
    {
        _upstreamCatalog = upstreamCatalog ?? throw new ArgumentNullException(nameof(upstreamCatalog));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether the mirrored tool catalog has been initialized.
    /// </summary>
    public bool IsInitialized => Volatile.Read(ref _snapshot) != null;

    /// <summary>
    /// Gets the stable snapshot of generically mirrored Basic Memory tools.
    /// </summary>
    public IReadOnlyList<BasicMemoryMirroredTool> Tools => GetSnapshot().Tools;

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public void Dispose()
    {
        _initializationLock.Dispose();
    }

    /// <summary>
    /// Builds the process-lifetime mirrored tool snapshot from the discovered Basic Memory tool catalog.
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

            await _upstreamCatalog.InitializeAsync(cancellationToken).ConfigureAwait(false);
            var tools = new List<BasicMemoryMirroredTool>();
            var toolsByName = new Dictionary<string, BasicMemoryMirroredTool>(StringComparer.Ordinal);
            foreach (var upstreamTool in _upstreamCatalog.Tools)
            {
                var protocolTool = upstreamTool.ProtocolTool;
                var classification = _classifier.Classify(protocolTool.Name, protocolTool.InputSchema);
                if (classification != ToolRoutingClassification.AutomaticallyRouted)
                    continue;

                var routingAnalysis = _routingSchemaGuard.Analyze(protocolTool.InputSchema);
                if (!routingAnalysis.IsAutomaticallyRoutable)
                    throw new InvalidOperationException($"Tool '{protocolTool.Name}' was classified as automatically routed but its routing analysis is not automatically routable.");


                if (!_schemaRewriter.TryRewrite(protocolTool.InputSchema, routingAnalysis, out var publicInputSchema))
                {
                    _logger.LogWarning("Basic Memory tool '{ToolName}' has safe project-routing semantics but its public input schema cannot currently be rewritten. The tool will not be mirrored.", protocolTool.Name);
                    continue;
                }

                var mirroredTool = new BasicMemoryMirroredTool(upstreamTool, routingAnalysis, publicInputSchema);
                if (!toolsByName.TryAdd(mirroredTool.Name, mirroredTool))
                    throw new InvalidOperationException($"Duplicate mirrored Basic Memory MCP tool name '{mirroredTool.Name}'.");

                tools.Add(mirroredTool);
            }

            var snapshot = new CatalogSnapshot(new ReadOnlyCollection<BasicMemoryMirroredTool>(tools), new ReadOnlyDictionary<string, BasicMemoryMirroredTool>(toolsByName));
            Volatile.Write(ref _snapshot, snapshot);
            _logger.LogInformation("Prepared {MirroredToolCount} of {UpstreamToolCount} discovered Basic Memory MCP tools for generic proxy mirroring.", snapshot.Tools.Count, _upstreamCatalog.Tools.Count);
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <summary>
    /// Attempts to get a mirrored Basic Memory tool by its exact canonical upstream MCP tool name.
    /// </summary>
    public bool TryGetTool(string name, [NotNullWhen(true)] out BasicMemoryMirroredTool? tool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetSnapshot().ToolsByName.TryGetValue(name, out tool);
    }

    #endregion

    #region Private Methods

    private CatalogSnapshot GetSnapshot()
    {
        return Volatile.Read(ref _snapshot) ??
               throw new InvalidOperationException("The mirrored Basic Memory tool catalog has not been initialized.");
    }

    #endregion

    #region Nested Classes

    private sealed record CatalogSnapshot(IReadOnlyList<BasicMemoryMirroredTool> Tools, IReadOnlyDictionary<string, BasicMemoryMirroredTool> ToolsByName);

    #endregion
}
