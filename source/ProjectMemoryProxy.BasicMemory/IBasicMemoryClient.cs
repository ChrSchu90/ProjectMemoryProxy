namespace ProjectMemoryProxy.BasicMemory;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Client;

/// <summary>
/// Provides access to the Basic Memory MCP upstream.
/// </summary>
public interface IBasicMemoryClient
{
    #region Events

    #endregion

    #region Properties

    #endregion

    #region Methods

    /// <summary>
    /// Discovers the tools currently exposed by Basic Memory.
    /// </summary>
    Task<IReadOnlyList<McpClientTool>> ListToolsAsync(CancellationToken cancellationToken = default);

    #endregion
}
