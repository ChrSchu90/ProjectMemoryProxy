namespace ProjectMemoryProxy.BasicMemory;

using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using System;

/// <summary>
/// Adds the process-lifetime mirrored Basic Memory tools to every MCP server options instance.
/// </summary>
internal sealed class BasicMemoryMcpServerOptionsSetup : IPostConfigureOptions<McpServerOptions>
{
    #region Static Fields

    #endregion

    #region Private Fields

    private readonly BasicMemoryMirroredMcpServerToolRegistry _registry;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicMemoryMcpServerOptionsSetup"/> class.
    /// </summary>
    public BasicMemoryMcpServerOptionsSetup(BasicMemoryMirroredMcpServerToolRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    #endregion

    #region Properties

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public void PostConfigure(string? name, McpServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var mirroredTools = _registry.Tools;
        if (mirroredTools.Count == 0)
            return;

        var toolCollection = options.ToolCollection ??= new McpServerPrimitiveCollection<McpServerTool>(StringComparer.Ordinal);
        foreach (var tool in mirroredTools)
        {
            if (!toolCollection.TryAdd(tool))
                throw new InvalidOperationException($"Cannot register mirrored Basic Memory MCP tool '{tool.ProtocolTool.Name}' because another MCP tool with the same name is already registered.");
        }
    }

    #endregion

    #region Private Methods

    #endregion
}
