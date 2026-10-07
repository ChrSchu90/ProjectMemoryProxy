namespace ProjectMemoryProxy.BasicMemory.Health;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Verifies that the configured Basic Memory MCP upstream is reachable and operational.
/// </summary>
public interface IBasicMemoryHealthProbe
{
    #region Events

    #endregion

    #region Properties

    #endregion

    #region Methods

    /// <summary>
    /// Verifies that Basic Memory accepts a diagnostic MCP tool call.
    /// </summary>
    Task CheckAsync(CancellationToken cancellationToken = default);

    #endregion
}
