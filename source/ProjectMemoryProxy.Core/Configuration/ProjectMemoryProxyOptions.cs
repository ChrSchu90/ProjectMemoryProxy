namespace ProjectMemoryProxy.Core.Configuration;

using System;
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

    /// <summary>
    /// Gets the default minimum Serilog event level.
    /// </summary>
    public const string DefaultLogLevel = "Warning";

    #endregion

    #region Private Fields

    #endregion

    #region Constructors

    #endregion

    #region Properties

    /// <summary>
    /// Gets or initializes the minimum Serilog event level.
    /// </summary>
    public string LogLevel { get; init; } = DefaultLogLevel;

    /// <summary>
    /// The directory where the server will store its data, including the SQLite database.
    /// </summary>
    public string DataDirectory { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the Basic Memory MCP endpoint.
    /// </summary>
    public Uri BasicMemoryEndpoint { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the connection timeout for the Basic Memory MCP endpoint <see cref="BasicMemoryEndpoint"/>.
    /// </summary>
    public TimeSpan BasicMemoryConnectionTimeout { get; init; } = TimeSpan.FromSeconds(30);

    #endregion

    #region Public Methods

    #endregion

    #region Private Methods

    #endregion
}
