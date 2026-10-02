namespace ProjectMemoryProxy.Server.Configuration;

using System.Diagnostics;
using Serilog.Events;

/// <summary>
/// Contains the DevHatch server configuration.
/// </summary>
[DebuggerDisplay("ProjectMemoryProxyOptions")]
public sealed class ProjectMemoryProxyOptions
{
    #region Static Fields

    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "ProjectMemoryProxy";

    #endregion

    #region Private Fields

    #endregion

    #region Constructors

    #endregion

    #region Properties

    /// <summary>
    /// Gets or initializes the minimum Serilog event level.
    /// </summary>
    public string LogLevel { get; init; } = nameof(LogEventLevel.Information);

    #endregion

    #region Public Methods

    #endregion

    #region Private Methods

    #endregion
}
