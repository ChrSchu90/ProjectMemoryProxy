namespace ProjectMemoryProxy.Core.Configuration;

using System.Diagnostics;

/// <summary>
/// Contains the server configuration.
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
    public string LogLevel { get; init; } = "Information";

    /// <summary>
    /// The directory where the server will store its data, including the SQLite database.
    /// </summary>
    public string DataDirectory { get; init; } = null!;

    #endregion

    #region Public Methods

    #endregion

    #region Private Methods

    #endregion
}
